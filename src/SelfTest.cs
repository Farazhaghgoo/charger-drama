using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Speech.Synthesis;
using System.Windows.Forms;
using MediaPlayer = System.Windows.Media.MediaPlayer;

namespace ChargerDrama
{
    // Silent checks, run with:  ChargerDrama.exe --selftest <folder>
    // Never touches the real install or settings (uses <folder>\home), never plays sound.
    internal static class SelfTest
    {
        private static StreamWriter log;
        private static int failures;

        public static int Run(string outDir)
        {
            outDir = Path.GetFullPath(outDir ?? "selftest");
            Directory.CreateDirectory(outDir);
            AppPaths.TestHome = Path.Combine(outDir, "home");
            if (Directory.Exists(AppPaths.TestHome)) Directory.Delete(AppPaths.TestHome, true);
            using (log = new StreamWriter(Path.Combine(outDir, "selftest.txt")))
            {
                Step("extract voices", delegate { CheckVoices(); });
                Step("audio files open", delegate { CheckAudioFiles(); });
                Step("sound effects", delegate { CheckSoundFx(); });
                Step("robot voice", delegate { CheckRobot(); });
                Step("settings", delegate { CheckSettings(); });
                Step("device watcher", delegate { CheckDevices(); });
                Step("mic / fullscreen", delegate { CheckPresence(); });
                Step("mascot pictures", delegate { DrawMascots(outDir); });
                Say("");
                Say(failures == 0 ? "ALL OK" : failures + " FAILURE(S)");
            }
            return failures == 0 ? 0 : 1;
        }

        private static void Step(string name, Action a)
        {
            Say("== " + name);
            var sw = Stopwatch.StartNew();
            try { a(); }
            catch (Exception ex) { Fail("exception: " + ex); }
            Say("   (" + sw.ElapsedMilliseconds + " ms)");
        }

        private static void Say(string s) { log.WriteLine(s); log.Flush(); }
        private static void Fail(string s) { failures++; Say("   FAIL " + s); }

        private static void CheckVoices()
        {
            SoundExtractor.Extract(true);
            var lib = new VoiceLibrary();
            foreach (var pack in lib.Packs)
            {
                var counts = string.Join(" ", VoiceCategory.All.Select(c => c.Key + ":" + pack.Count(c.Key)).ToArray());
                Say("   " + pack.Name.PadRight(32) + counts);
                if (pack.IsRobot) continue;
                foreach (var c in VoiceCategory.All)
                    if (!pack.Has(c.Key)) Fail(pack.Name + " has no '" + c.Key + "'");
                foreach (var file in pack.AllFiles)
                    if (!pack.HasCaption(file)) Fail(pack.Name + " " + Path.GetFileName(file) + " has no caption");
            }
            if (lib.Packs.Count(p => !p.IsRobot) < 5) Fail("expected 5 built-in voices");
            var pick = lib.PickClip(lib.Find("Mad Scientist"), new[] { "annoyed", "laugh" }, new Random(2));
            Say("   sample caption: " + pick.Caption);
            var fallback = lib.PickClip(null, new[] { "usb-in" }, new Random(3));
            if (fallback.Ssml == null) Fail("missing pack should fall back to the robot voice");
        }

        private static void CheckAudioFiles()
        {
            var lib = new VoiceLibrary();
            int n = 0;
            double min = 999, max = 0;
            var all = lib.Packs.Where(p => !p.IsRobot).SelectMany(p => p.AllFiles).ToList();
            for (int start = 0; start < all.Count; start += 16)
            {
                // open 16 at a time (muted, never played) and wait until each knows its length
                var batch = all.Skip(start).Take(16).Select(f => { var p = new MediaPlayer(); p.Volume = 0; p.Open(new Uri(f)); return p; }).ToList();
                var sw = Stopwatch.StartNew();
                while (batch.Any(p => !p.NaturalDuration.HasTimeSpan) && sw.ElapsedMilliseconds < 8000) { Application.DoEvents(); System.Threading.Thread.Sleep(10); }
                for (int i = 0; i < batch.Count; i++)
                {
                    var p = batch[i];
                    if (p.NaturalDuration.HasTimeSpan)
                    {
                        var s = p.NaturalDuration.TimeSpan.TotalSeconds;
                        min = Math.Min(min, s); max = Math.Max(max, s); n++;
                    }
                    else Fail("can't open " + all[start + i]);
                    p.Close();
                }
            }
            Say(string.Format("   {0} clips open fine, {1:0.0}-{2:0.0} s long", n, min, max));
        }

        private static void CheckSoundFx()
        {
            foreach (var kv in new Dictionary<string, byte[]> { { "power-up", SoundFx.PowerUpWav() }, { "power-down", SoundFx.PowerDownWav() } })
            {
                var b = kv.Value;
                bool ok = b.Length > 44 && b[0] == 'R' && b[1] == 'I' && b[8] == 'W' && BitConverter.ToInt32(b, 24) == 44100;
                Say(string.Format("   {0}: {1:0.00} s {2}", kv.Key, (b.Length - 44) / 88200.0, ok ? "ok" : "BAD HEADER"));
                if (!ok) Fail(kv.Key + " is not a 44.1 kHz WAV");
                using (var sp = new System.Media.SoundPlayer(new MemoryStream(b))) sp.Load();
            }
        }

        private static void CheckRobot()
        {
            var tmp = Path.Combine(Path.GetTempPath(), "chargerdrama-selftest.wav");
            using (var synth = new SpeechSynthesizer())
            {
                int n = 0;
                foreach (var ssml in RobotVoice.AllSsml())
                {
                    synth.SetOutputToWaveFile(tmp);
                    synth.SpeakSsml(ssml);
                    synth.SetOutputToNull();
                    n++;
                }
                Say("   " + n + " robot lines render (" + synth.Voice.Name + ")");
            }
            File.Delete(tmp);
        }

        private static void CheckSettings()
        {
            var s = new Settings { Voice = "Drama Queen", AutoMute = false, MascotMode = MascotModes.Reacting, MascotX = 10, MascotY = 20 };
            s.Save();
            var r = Settings.Load();
            if (r.Voice != "Drama Queen" || r.AutoMute || r.MascotMode != MascotModes.Reacting || r.MascotX != 10 || r.MascotY != 20)
                Fail("settings round trip");
            File.WriteAllText(AppPaths.SettingsFile, "{\"voice\":  \"Italian Drama\"}");   // old PowerShell format
            if (Settings.Load().Voice != "Italian Drama") Fail("old settings file not understood");
            Say("   round trip ok");
        }

        private static void CheckDevices()
        {
            var events = new List<string>();
            using (var w = new DeviceWatcher())
            {
                w.DeviceChanged += delegate(object s, DeviceEventArgs e) { events.Add(e.Kind + (e.Arrived ? " in " : " out ") + e.Detail); };
                w.SimulateVolume(true, 'E');
                w.SimulateVolume(false, 'E');
                w.SimulateMouse(true);
                w.SimulateMouse(true);
                w.SimulateMouse(false);
            }
            foreach (var e in events) Say("   " + e);
            if (events.Count != 4) Fail("expected 4 device events, got " + events.Count);
        }

        private static void CheckPresence()
        {
            string app;
            bool mic = MicWatcher.IsMicInUse(out app);
            Say("   mic in use: " + mic + (app != null ? " (" + app + ")" : "") + ", fullscreen/presenting: " + UserState.IsBusyFullscreen());
        }

        private static void DrawMascots(string outDir)
        {
            var shots = new[]
            {
                new { Mood = Mood.Neutral, Level = 0.62f, Charging = false, Bubble = (string)null },
                new { Mood = Mood.Happy, Level = 0.85f, Charging = true, Bubble = (string)null },
                new { Mood = Mood.Laughing, Level = 0.85f, Charging = true, Bubble = "Mwahahahaha! Sweet, sweet electricity!" },
                new { Mood = Mood.Crying, Level = 0.7f, Charging = false, Bubble = "Noooooooo! No no no no no! Please... no!" },
                new { Mood = Mood.Weak, Level = 0.18f, Charging = false, Bubble = "Twenty percent?! My power... it's fading..." },
                new { Mood = Mood.Dying, Level = 0.04f, Charging = false, Bubble = "Tell my experiments... I loved them..." },
                new { Mood = Mood.Angry, Level = 0.6f, Charging = true, Bubble = "Are you PLAYING with me?!" },
                new { Mood = Mood.Relieved, Level = 0.08f, Charging = true, Bubble = "FINALLY!" },
                new { Mood = Mood.Surprised, Level = 0.6f, Charging = false, Bubble = "Ooh! What's that? Is it for ME?" },
                new { Mood = Mood.Sad, Level = 0.6f, Charging = false, Bubble = "Hey! Bring it back!" },
                new { Mood = Mood.Giggle, Level = 0.6f, Charging = false, Bubble = "Hee hee! Stop it! That tickles!" },
                new { Mood = Mood.Grumpy, Level = 0.6f, Charging = true, Bubble = (string)null },
            };
            const int cols = 4;
            int w = MascotRenderer.Width, h = MascotRenderer.Height;
            using (var sheet = new Bitmap(w * cols, h * ((shots.Length + cols - 1) / cols)))
            using (var g = Graphics.FromImage(sheet))
            using (var font = new Font("Segoe UI", 11f, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel))
            {
                g.Clear(System.Drawing.Color.FromArgb(70, 110, 160));   // desktop-ish background to check transparency
                for (int i = 0; i < shots.Length; i++)
                {
                    var s = shots[i];
                    using (var bmp = Mascot.RenderStill(s.Mood, s.Level, s.Charging, s.Bubble, 1.3f, 1f))
                    {
                        g.DrawImage(bmp, (i % cols) * w, (i / cols) * h);
                        g.DrawString(s.Mood.ToString(), font, System.Drawing.Brushes.White, (i % cols) * w + 6, (i / cols) * h + h - 18);
                    }
                }
                sheet.Save(Path.Combine(outDir, "mascot-moods.png"), ImageFormat.Png);
            }
            using (var big = Mascot.RenderStill(Mood.Laughing, 0.85f, true, "Mwahahahaha!", 0.9f, 2f))
                big.Save(Path.Combine(outDir, "mascot-laughing-2x.png"), ImageFormat.Png);
            Say("   wrote mascot-moods.png and mascot-laughing-2x.png");
        }
    }

    // Writes the app icon (the mascot's happy face) as a multi-size .ico.
    internal static class IconMaker
    {
        public static void Write(string path)
        {
            int[] sizes = { 16, 24, 32, 48, 64, 256 };
            var pngs = new List<byte[]>();
            using (var full = Mascot.RenderStill(Mood.Happy, 0.85f, false, null, 0.3f, 4f))
            {
                // square around the battery body (logical coordinates)
                var crop = new RectangleF((MascotRenderer.AX - 66f) * 4f, (MascotRenderer.AY - 130f) * 4f, 132f * 4f, 132f * 4f);
                foreach (var n in sizes)
                    using (var bmp = new Bitmap(n, n, PixelFormat.Format32bppArgb))
                    {
                        using (var g = Graphics.FromImage(bmp))
                        {
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            g.DrawImage(full, new RectangleF(0, 0, n, n), crop, GraphicsUnit.Pixel);
                        }
                        using (var ms = new MemoryStream()) { bmp.Save(ms, ImageFormat.Png); pngs.Add(ms.ToArray()); }
                    }
            }
            using (var fs = File.Create(path))
            using (var w = new BinaryWriter(fs))
            {
                w.Write((ushort)0); w.Write((ushort)1); w.Write((ushort)sizes.Length);
                int offset = 6 + 16 * sizes.Length;
                for (int i = 0; i < sizes.Length; i++)
                {
                    byte b = (byte)(sizes[i] >= 256 ? 0 : sizes[i]);
                    w.Write(b); w.Write(b); w.Write((byte)0); w.Write((byte)0);
                    w.Write((ushort)1); w.Write((ushort)32);
                    w.Write(pngs[i].Length); w.Write(offset);
                    offset += pngs[i].Length;
                }
                foreach (var p in pngs) w.Write(p);
            }
        }
    }
}
