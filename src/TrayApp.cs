using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace ChargerDrama
{
    // The app: watches the charger, battery, USB drives and mice, keeps a mood, and drives the
    // voice, the mascot and the tray icon.
    internal sealed class TrayApp : ApplicationContext
    {
        private const int AnnoyedToggles = 3;         // this many plug/unplugs...
        private const int AnnoyedWindowSeconds = 45;  // ...within this many seconds = annoyed
        private const int LongWaitMinutes = 30;       // on battery this long, then plugged in = relief

        private readonly Settings settings = Settings.Load();
        private readonly VoiceLibrary library;
        private readonly AudioPlayer audio = new AudioPlayer();
        private readonly Random rng = new Random();
        private readonly NotifyIcon tray = new NotifyIcon();
        private readonly ContextMenuStrip menu = new ContextMenuStrip();
        private readonly Mascot mascot;
        private readonly DeviceWatcher devices;
        private readonly EventWaitHandle quitSignal;
        private readonly Timer timer = new Timer();
        private readonly Stopwatch clock = Stopwatch.StartNew();

        private PowerLineStatus line = PowerLineStatus.Unknown;
        private int percent = -1;
        private bool hasBattery;
        private bool fired20, fired5;
        private DateTime? onBatterySince;
        private readonly Queue<DateTime> toggles = new Queue<DateTime>();
        private DateTime annoyedUntil = DateTime.MinValue;
        private DateTime lastPoke = DateTime.MinValue;
        private bool micBusy;
        private string micApp;
        private int ticks;
        private bool quitting;
        private DateTime quitAt;
        private RecorderForm recorder;

        private ToolStripMenuItem statusItem, voiceMenu, autoMuteItem, startupItem;
        private ToolStripMenuItem mascotAlways, mascotReacting, mascotHidden;
        private IntPtr trayIconHandle = IntPtr.Zero;
        private string trayIconKey;

        public TrayApp(bool quietStart, bool welcome)
        {
            SoundExtractor.Extract(false);
            library = new VoiceLibrary();
            if (library.Find(settings.Voice) == null)
                settings.Voice = library.Find(VoiceLibrary.DefaultVoice) != null ? VoiceLibrary.DefaultVoice : VoiceLibrary.RobotName;

            quitSignal = new EventWaitHandle(false, EventResetMode.AutoReset, AppPaths.QuitEventName);

            BuildMenu();
            tray.ContextMenuStrip = menu;
            tray.MouseClick += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) mascot.Say(StatusText(), 3.5f); };
            tray.Visible = true;

            mascot = new Mascot(settings, delegate { return audio.IsTalking; }, OnPoke, menu);
            devices = new DeviceWatcher();
            devices.DeviceChanged += OnDevice;

            ReadPower(true);
            CheckPresence();
            UpdateMascotContext();
            UpdateTray();

            mascot.PowerOn();
            if (!quietStart && !micBusy) audio.PlayFx(SoundFx.PowerUpWav());
            if (welcome) mascot.Say("Hi, I'm Volty!\nCtrl+Alt+D turns me on and off.\nRight-click me for options.", 9f);

            timer.Interval = 40;
            timer.Tick += OnTick;
            timer.Start();
        }

        // ---- main loop (25 times a second) ------------------------------------------------
        private void OnTick(object sender, EventArgs e)
        {
            try
            {
                if (quitting)
                {
                    mascot.Tick();
                    if (DateTime.UtcNow >= quitAt) ExitThread();
                    return;
                }
                if (quitSignal.WaitOne(0)) { BeginQuit(); return; }

                ticks++;
                if (ticks % 12 == 0) { ReadPower(false); UpdateMascotContext(); UpdateTray(); }
                if (ticks % 50 == 0) CheckPresence();
                mascot.Tick();
            }
            catch (Exception ex) { AppPaths.LogError("Tick", ex); }
        }

        // ---- charger and battery ----------------------------------------------------------
        private void ReadPower(bool initial)
        {
            var ps = SystemInformation.PowerStatus;
            hasBattery = (ps.BatteryChargeStatus & BatteryChargeStatus.NoSystemBattery) == 0 && ps.BatteryChargeStatus != BatteryChargeStatus.Unknown;
            float lp = ps.BatteryLifePercent;
            percent = lp >= 0f && lp <= 1f ? (int)Math.Round(lp * 100f) : -1;
            var now = ps.PowerLineStatus;

            if (initial)
            {
                line = now;
                if (now == PowerLineStatus.Offline) onBatterySince = DateTime.UtcNow;
                return;
            }
            if (now != PowerLineStatus.Unknown && now != line)
            {
                var previous = line;
                line = now;
                if (previous != PowerLineStatus.Unknown) OnChargerChanged(now == PowerLineStatus.Online);
                else if (now == PowerLineStatus.Offline) onBatterySince = DateTime.UtcNow;
            }

            // battery warnings fire once per discharge, and re-arm after charging above them
            if (percent > 22) fired20 = false;
            if (percent > 7) fired5 = false;
            if (line == PowerLineStatus.Offline && hasBattery && percent >= 0 && clock.Elapsed.TotalSeconds > 3)
            {
                if (!fired5 && percent <= 5) { fired5 = fired20 = true; React(new[] { "low5", "low20" }, Mood.Dying); }
                else if (!fired20 && percent <= 20) { fired20 = true; React(new[] { "low20" }, Mood.Weak); }
            }
        }

        private void OnChargerChanged(bool plugged)
        {
            var now = DateTime.UtcNow;
            toggles.Enqueue(now);
            while (toggles.Count > 0 && (now - toggles.Peek()).TotalSeconds > AnnoyedWindowSeconds) toggles.Dequeue();
            bool annoyed = toggles.Count >= AnnoyedToggles;
            if (annoyed) annoyedUntil = now.AddSeconds(25);

            if (plugged)
            {
                bool longWait = onBatterySince.HasValue && (now - onBatterySince.Value).TotalMinutes >= LongWaitMinutes;
                bool wasLow = hasBattery && percent >= 0 && percent <= 20;
                onBatterySince = null;
                if (annoyed) React(new[] { "annoyed", "laugh" }, Mood.Angry);
                else if (longWait || wasLow) React(new[] { "relief", "laugh" }, Mood.Relieved);
                else React(new[] { "laugh" }, Mood.Laughing);
            }
            else
            {
                onBatterySince = now;
                if (annoyed) React(new[] { "annoyed", "nooo" }, Mood.Angry);
                else React(new[] { "nooo" }, Mood.Crying);
            }
            UpdateMascotContext();
            UpdateTray();
        }

        private Mood BaseMood()
        {
            if (DateTime.UtcNow < annoyedUntil) return Mood.Grumpy;
            if (line == PowerLineStatus.Online) return Mood.Happy;
            if (line == PowerLineStatus.Offline && hasBattery && percent >= 0)
            {
                if (percent <= 5) return Mood.Dying;
                if (percent <= 20) return Mood.Weak;
            }
            return Mood.Neutral;
        }

        private void UpdateMascotContext()
        {
            float level = hasBattery && percent >= 0 ? percent / 100f : 1f;
            mascot.SetContext(level, line == PowerLineStatus.Online, BaseMood(), micBusy);
        }

        // ---- calls (auto-mute) and fullscreen apps ----------------------------------------
        private void CheckPresence()
        {
            string app = null;
            micBusy = settings.AutoMute && MicWatcher.IsMicInUse(out app);
            micApp = app;
            mascot.SetSuppressed(UserState.IsBusyFullscreen());
        }

        // ---- reacting ---------------------------------------------------------------------
        private void React(string[] keys, Mood mood)
        {
            if (quitting) return;
            var clip = library.PickClip(library.Find(settings.Voice), keys, rng);
            string app = null;
            bool silent = settings.AutoMute && MicWatcher.IsMicInUse(out app);
            micBusy = silent;
            if (silent) micApp = app;
            if (!silent) audio.Play(clip);
            mascot.React(mood, clip.Caption, silent);
        }

        private static Mood MoodFor(string key)
        {
            switch (key)
            {
                case "laugh": return Mood.Laughing;
                case "nooo": return Mood.Crying;
                case "low20": return Mood.Weak;
                case "low5": return Mood.Dying;
                case "annoyed": return Mood.Angry;
                case "relief": return Mood.Relieved;
                case "usb-in": case "mouse-in": return Mood.Surprised;
                case "usb-out": case "mouse-out": return Mood.Sad;
                case "poke": return Mood.Giggle;
                default: return Mood.Happy;
            }
        }

        private void OnDevice(object sender, DeviceEventArgs e)
        {
            string key = e.Kind == DeviceKind.UsbDrive ? (e.Arrived ? "usb-in" : "usb-out") : (e.Arrived ? "mouse-in" : "mouse-out");
            React(new[] { key }, MoodFor(key));
        }

        private void OnPoke()
        {
            var now = DateTime.UtcNow;
            if ((now - lastPoke).TotalSeconds < 0.8) return;
            lastPoke = now;
            React(new[] { "poke" }, Mood.Giggle);
        }

        // ---- switching off ----------------------------------------------------------------
        private void BeginQuit()
        {
            if (quitting) return;
            quitting = true;
            audio.StopVoice();
            string app;
            if (!(settings.AutoMute && MicWatcher.IsMicInUse(out app))) audio.PlayFx(SoundFx.PowerDownWav());
            mascot.PowerOff();
            quitAt = DateTime.UtcNow.AddMilliseconds(1150);
        }

        protected override void ExitThreadCore()
        {
            timer.Stop();
            tray.Visible = false;
            tray.Dispose();
            if (recorder != null && !recorder.IsDisposed) recorder.Close();
            mascot.Dispose();
            devices.Dispose();
            audio.Dispose();
            quitSignal.Dispose();
            if (trayIconHandle != IntPtr.Zero) DestroyIcon(trayIconHandle);
            base.ExitThreadCore();
        }

        // ---- tray icon and menu -----------------------------------------------------------
        private string StatusText()
        {
            string s;
            if (line == PowerLineStatus.Online) s = "Plugged in" + (percent >= 0 && hasBattery ? " - " + percent + "%" : "") + ". Life is good!";
            else if (!hasBattery || percent < 0) s = "On battery.";
            else if (percent <= 5) s = "On battery - " + percent + "%... I'm dying here!";
            else if (percent <= 20) s = "On battery - " + percent + "%... getting weak...";
            else s = "On battery - " + percent + "%.";
            if (micBusy) s += "\nMuted: you're in a call" + (micApp != null ? " (" + micApp + ")" : "") + ".";
            return s;
        }

        private void UpdateTray()
        {
            statusItem.Text = StatusText().Replace("\n", "  ");
            string tip = AppPaths.AppName + " - " + settings.Voice + "\n" + StatusText();
            tray.Text = tip.Length > 63 ? tip.Substring(0, 63) : tip;

            string key = line == PowerLineStatus.Online ? "charging"
                       : hasBattery && percent >= 0 && percent <= 20 ? "low" : "battery";
            if (key == trayIconKey) return;
            trayIconKey = key;
            Color fill = key == "charging" ? Color.FromArgb(255, 204, 0) : key == "low" ? Color.FromArgb(255, 69, 58) : Color.FromArgb(235, 235, 240);
            int size = SystemInformation.SmallIconSize.Width;
            using (var bmp = new Bitmap(size, size))
            {
                using (var g = Graphics.FromImage(bmp))
                using (var bolt = MascotRenderer.BoltPath(size / 2f, size / 2f, size / 32f))
                using (var b = new SolidBrush(fill))
                using (var p = new Pen(Color.FromArgb(43, 45, 66), Math.Max(1f, size / 16f)))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.FillPath(b, bolt);
                    g.DrawPath(p, bolt);
                }
                var old = trayIconHandle;
                trayIconHandle = bmp.GetHicon();
                tray.Icon = Icon.FromHandle(trayIconHandle);
                if (old != IntPtr.Zero) DestroyIcon(old);
            }
        }

        private void BuildMenu()
        {
            statusItem = new ToolStripMenuItem("Charger Drama") { Enabled = false };
            menu.Items.Add(statusItem);
            menu.Items.Add(new ToolStripSeparator());

            var tryMenu = new ToolStripMenuItem("Try a reaction");
            foreach (var cat in VoiceCategory.All)
            {
                var key = cat.Key;
                tryMenu.DropDownItems.Add(cat.Title, null, delegate { React(new[] { key }, MoodFor(key)); });
            }
            menu.Items.Add(tryMenu);

            voiceMenu = new ToolStripMenuItem("Voice");
            voiceMenu.DropDownItems.Add("(loading)");
            voiceMenu.DropDownOpening += delegate { BuildVoiceMenu(); };
            menu.Items.Add(voiceMenu);
            menu.Items.Add("Record my own voice...", null, delegate { OpenRecorder(); });

            var mascotMenu = new ToolStripMenuItem("Mascot");
            mascotAlways = new ToolStripMenuItem("Always visible", null, delegate { SetMascotMode(MascotModes.Always); });
            mascotReacting = new ToolStripMenuItem("Only when something happens", null, delegate { SetMascotMode(MascotModes.Reacting); });
            mascotHidden = new ToolStripMenuItem("Hidden", null, delegate { SetMascotMode(MascotModes.Hidden); });
            mascotMenu.DropDownItems.AddRange(new ToolStripItem[] { mascotAlways, mascotReacting, mascotHidden, new ToolStripSeparator() });
            mascotMenu.DropDownItems.Add("Move back to the corner", null, delegate { mascot.ResetPosition(); });
            mascotMenu.DropDownOpening += delegate
            {
                mascotAlways.Checked = settings.MascotMode == MascotModes.Always;
                mascotReacting.Checked = settings.MascotMode == MascotModes.Reacting;
                mascotHidden.Checked = settings.MascotMode == MascotModes.Hidden;
            };
            menu.Items.Add(mascotMenu);

            autoMuteItem = new ToolStripMenuItem("Stay quiet during calls (mic in use)", null, delegate
            {
                settings.AutoMute = !settings.AutoMute;
                settings.Save();
                CheckPresence();
                UpdateMascotContext();
                UpdateTray();
            });
            startupItem = new ToolStripMenuItem("Start with Windows", null, delegate
            {
                Installer.SetStartWithWindows(!Installer.StartsWithWindows, Application.ExecutablePath);
            });
            menu.Items.Add(autoMuteItem);
            menu.Items.Add(startupItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Turn off   (Ctrl+Alt+D)", null, delegate { BeginQuit(); });
            if (AppPaths.IsRunningInstalledCopy)
                menu.Items.Add("Uninstall Charger Drama...", null, delegate { AskUninstall(); });

            menu.Opening += delegate
            {
                autoMuteItem.Checked = settings.AutoMute;
                startupItem.Checked = Installer.StartsWithWindows;
                statusItem.Text = StatusText().Replace("\n", "  ");
            };
        }

        private void BuildVoiceMenu()
        {
            library.Reload();
            voiceMenu.DropDownItems.Clear();
            bool userSection = false;
            foreach (var pack in library.Packs)
            {
                if (pack.IsUser && !userSection) { voiceMenu.DropDownItems.Add(new ToolStripSeparator()); userSection = true; }
                if (pack.IsRobot) voiceMenu.DropDownItems.Add(new ToolStripSeparator());
                var name = pack.Name;
                var item = new ToolStripMenuItem(pack.IsUser ? name + "   (your recording)" : name, null, delegate { SelectVoice(name); });
                item.Checked = string.Equals(name, settings.Voice, StringComparison.OrdinalIgnoreCase);
                voiceMenu.DropDownItems.Add(item);
            }
            voiceMenu.DropDownItems.Add(new ToolStripSeparator());
            voiceMenu.DropDownItems.Add("Record my own voice...", null, delegate { OpenRecorder(); });
            voiceMenu.DropDownItems.Add("Open my voices folder", null, delegate
            {
                System.IO.Directory.CreateDirectory(AppPaths.UserVoicesDir);
                Process.Start("explorer.exe", "\"" + AppPaths.UserVoicesDir + "\"");
            });
        }

        private void SelectVoice(string name)
        {
            settings.Voice = name;
            settings.Save();
            UpdateTray();
            React(new[] { "laugh" }, Mood.Laughing);   // preview
        }

        private void SetMascotMode(string mode)
        {
            settings.MascotMode = mode;
            settings.Save();
            if (mode != MascotModes.Hidden) mascot.Say(mode == MascotModes.Always ? "I'll stay right here!" : "I'll only pop up when something happens.", 3f);
        }

        private void OpenRecorder()
        {
            if (recorder != null && !recorder.IsDisposed) { recorder.Activate(); return; }
            var current = library.Find(settings.Voice);
            string initial = current != null && current.IsUser ? current.Name : "My Voice";
            recorder = new RecorderForm(AppPaths.UserVoicesDir, initial, VoiceCategory.All, delegate(string path) { audio.PlayFile(path); }, delegate { audio.StopVoice(); });
            recorder.FormClosed += delegate
            {
                var r = recorder;
                recorder = null;
                if (r != null && r.AnythingRecorded)
                {
                    library.Reload();
                    if (library.Find(r.VoiceName) != null)
                    {
                        settings.Voice = r.VoiceName;
                        settings.Save();
                        UpdateTray();
                        mascot.Say("Ooh! I'm using YOUR voice now!", 4f);
                    }
                }
            };
            recorder.Show();
        }

        private void AskUninstall()
        {
            if (MessageBox.Show("Remove Charger Drama from this PC?", AppPaths.AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            bool deleteData = MessageBox.Show("Also delete your settings and the voices you recorded?\n\nChoose No to keep them (they're in " + AppPaths.DataDir + ").",
                AppPaths.AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
            Installer.Uninstall(deleteData);
            BeginQuit();
        }

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr handle);
    }
}
