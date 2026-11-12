using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace ChargerDrama
{
    // One thing to play: an audio file, or (Classic Robot) speech markup for the Windows voice.
    internal sealed class Clip
    {
        public string Key;
        public string File;
        public string Ssml;
        public string Caption;
    }

    internal sealed class VoicePack
    {
        private static readonly string[] AudioExtensions = { ".mp3", ".wav", ".m4a", ".wma", ".aac" };

        private readonly Dictionary<string, List<string>> files = new Dictionary<string, List<string>>();
        private readonly Dictionary<string, string> captions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> lastPick = new Dictionary<string, string>();

        public string Name { get; private set; }
        public string Folder { get; private set; }
        public bool IsUser { get; private set; }
        public bool IsRobot { get; private set; }

        private VoicePack() { }

        public static VoicePack Robot()
        {
            return new VoicePack { Name = VoiceLibrary.RobotName, IsRobot = true };
        }

        public static VoicePack FromFolder(string folder, bool isUser, string displayName)
        {
            var pack = new VoicePack { Name = displayName, Folder = folder, IsUser = isUser };
            foreach (var f in Directory.GetFiles(folder))
            {
                if (!AudioExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())) continue;
                var baseName = Path.GetFileNameWithoutExtension(f);
                foreach (var cat in VoiceCategory.All)
                {
                    if (!baseName.StartsWith(cat.Key, StringComparison.OrdinalIgnoreCase)) continue;
                    List<string> list;
                    if (!pack.files.TryGetValue(cat.Key, out list)) pack.files.Add(cat.Key, list = new List<string>());
                    list.Add(f);
                    break;
                }
            }
            var linesFile = Path.Combine(folder, "lines.json");
            if (File.Exists(linesFile))
            {
                try
                {
                    var d = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(linesFile, Encoding.UTF8));
                    foreach (var kv in d) pack.captions[kv.Key] = Convert.ToString(kv.Value);
                }
                catch (Exception ex) { AppPaths.LogError("lines.json " + folder, ex); }
            }
            return pack;
        }

        public bool IsEmpty { get { return !IsRobot && files.Count == 0; } }

        public bool Has(string key)
        {
            if (IsRobot) return RobotVoice.Has(key);
            List<string> list;
            return files.TryGetValue(key, out list) && list.Count > 0;
        }

        public int Count(string key)
        {
            if (IsRobot) return RobotVoice.Has(key) ? 1 : 0;
            List<string> list;
            return files.TryGetValue(key, out list) ? list.Count : 0;
        }

        public IEnumerable<string> AllFiles { get { return files.Values.SelectMany(l => l); } }

        public bool HasCaption(string file)
        {
            string c;
            return captions.TryGetValue(Path.GetFileName(file), out c) && !string.IsNullOrEmpty(c);
        }

        public Clip Pick(string key, Random rng)
        {
            if (IsRobot) return RobotVoice.Pick(key, rng);
            List<string> list;
            if (!files.TryGetValue(key, out list) || list.Count == 0) return null;
            string last;
            lastPick.TryGetValue(key, out last);
            var choices = list.Count > 1 ? list.Where(f => f != last).ToList() : list;
            var file = choices[rng.Next(choices.Count)];
            lastPick[key] = file;
            string caption;
            if (!captions.TryGetValue(Path.GetFileName(file), out caption) || string.IsNullOrEmpty(caption))
                caption = VoiceLibrary.DefaultCaption(key);
            return new Clip { Key = key, File = file, Caption = caption };
        }
    }

    internal sealed class VoiceLibrary
    {
        public const string RobotName = "Classic Robot (Windows voice)";
        public const string DefaultVoice = "Mad Scientist";

        public List<VoicePack> Packs { get; private set; }
        public VoicePack RobotPack { get; private set; }

        public VoiceLibrary()
        {
            RobotPack = VoicePack.Robot();
            Reload();
        }

        public void Reload()
        {
            var packs = new List<VoicePack>();
            AddFolders(packs, AppPaths.BuiltInSoundsDir, false);
            AddFolders(packs, AppPaths.UserVoicesDir, true);
            packs.Add(RobotPack);
            Packs = packs;
        }

        private static void AddFolders(List<VoicePack> packs, string root, bool isUser)
        {
            try
            {
                if (!Directory.Exists(root)) return;
                foreach (var dir in Directory.GetDirectories(root).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
                {
                    var name = Path.GetFileName(dir);
                    if (packs.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) || string.Equals(name, RobotName, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!isUser) continue;
                        name += " (mine)";   // your recording named like a built-in voice
                    }
                    var pack = VoicePack.FromFolder(dir, isUser, name);
                    if (!pack.IsEmpty) packs.Add(pack);
                }
            }
            catch (Exception ex) { AppPaths.LogError("Load voices " + root, ex); }
        }

        public VoicePack Find(string name)
        {
            return Packs.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        // First key the pack has wins; if it has none of them, Classic Robot says the first one.
        public Clip PickClip(VoicePack pack, string[] keys, Random rng)
        {
            if (pack != null)
                foreach (var k in keys)
                    if (pack.Has(k)) return pack.Pick(k, rng);
            return RobotPack.Pick(keys[0], rng);
        }

        public static string DefaultCaption(string key)
        {
            switch (key)
            {
                case "laugh": return "MWAHAHAHA!";
                case "nooo": return "NOOOOO! Please... no!";
                case "low20": return "20%... I'm getting weak...";
                case "low5": return "5%... this is the end...";
                case "annoyed": return "Are you PLAYING with me?!";
                case "relief": return "FINALLY!";
                case "usb-in": return "Ooh! A USB stick!";
                case "usb-out": return "Hey! Bring it back!";
                case "mouse-in": return "A mouse! Hello!";
                case "mouse-out": return "Where did my mouse go?";
                case "poke": return "Hee hee! That tickles!";
                default: return "!";
            }
        }
    }

    // Lines for the built-in Windows voice ("Classic Robot"), also the fallback for missing clips.
    internal static class RobotVoice
    {
        private sealed class Seg
        {
            public readonly string Text, Rate, Pitch;
            public Seg(string text, string rate, string pitch) { Text = text; Rate = rate; Pitch = pitch; }
        }

        private static Seg S(string text, string rate, string pitch) { return new Seg(text, rate, pitch); }

        private static readonly Dictionary<string, Seg[][]> Lines = new Dictionary<string, Seg[][]>
        {
            { "laugh", new[] {
                new[] { S("Mwa ha ha ha ha!", "fast", "+10%"), S("Ha ha ha ha ha ha ha!", "x-fast", "+35%"), S("HA HA HA HA HA HA!", "x-fast", "+60%"), S("hee hee hee hee hee!", "x-fast", "+90%"), S("Yesss. Sweet, sweet electricity.", "slow", "-15%") },
                new[] { S("Ha ha ha ha ha ha ha ha ha!", "x-fast", "+50%"), S("Mwa ha ha ha!", "fast", "-10%"), S("I am charging! I am unstoppable!", "medium", "+20%") } } },
            { "nooo", new[] {
                new[] { S("Noooooo!", "x-slow", "-10%"), S("No no no no no!", "fast", "+25%"), S("Please... no!", "slow", "+45%") },
                new[] { S("Wait wait wait!", "fast", "+30%"), S("Noooooo!", "x-slow", "-15%"), S("No no no no!", "fast", "+35%"), S("Please... no... it's so cold.", "slow", "+55%") } } },
            { "low20", new[] {
                new[] { S("Twenty percent.", "medium", "+0%"), S("Battery... getting... low.", "slow", "-10%"), S("Please find the charger.", "medium", "+20%") } } },
            { "low5", new[] {
                new[] { S("Five percent.", "slow", "-10%"), S("Systems... failing.", "x-slow", "-20%"), S("Goodbye... world.", "x-slow", "-30%") } } },
            { "annoyed", new[] {
                new[] { S("Are you PLAYING with me?", "fast", "+30%") },
                new[] { S("In. Out. In. Out.", "medium", "+10%"), S("Make up your mind!", "fast", "+40%") } } },
            { "relief", new[] {
                new[] { S("Finally!", "medium", "+40%"), S("Power restored. Ha ha ha!", "fast", "+20%") } } },
            { "usb-in", new[] {
                new[] { S("Ooh! What is that?", "medium", "+40%"), S("Is it for me?", "fast", "+50%") } } },
            { "usb-out", new[] {
                new[] { S("Hey! Bring it back!", "fast", "+30%") } } },
            { "mouse-in", new[] {
                new[] { S("A mouse! Hello, little friend!", "medium", "+30%") } } },
            { "mouse-out", new[] {
                new[] { S("Where did my mouse go?", "medium", "+20%") } } },
            { "poke", new[] {
                new[] { S("Hee hee! That tickles!", "fast", "+50%") },
                new[] { S("Hey! Stop poking me!", "fast", "+30%") } } },
        };

        public static string Language = "en-US";

        public static bool Has(string key) { return Lines.ContainsKey(key); }

        public static IEnumerable<string> AllSsml()
        {
            foreach (var kv in Lines)
                foreach (var line in kv.Value)
                    yield return ToSsml(line);
        }

        public static Clip Pick(string key, Random rng)
        {
            Seg[][] options;
            if (!Lines.TryGetValue(key, out options)) options = Lines["laugh"];
            var line = options[rng.Next(options.Length)];
            return new Clip { Key = key, Ssml = ToSsml(line), Caption = string.Join(" ", line.Select(s => s.Text).ToArray()) };
        }

        private static string ToSsml(Seg[] line)
        {
            var sb = new StringBuilder();
            sb.Append("<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='").Append(Language).Append("'>");
            foreach (var s in line)
            {
                sb.Append("<prosody rate='").Append(s.Rate).Append("' pitch='").Append(s.Pitch).Append("'>")
                  .Append(System.Security.SecurityElement.Escape(s.Text)).Append("</prosody><break time='120ms'/>");
            }
            return sb.Append("</speak>").ToString();
        }
    }
}
