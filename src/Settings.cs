using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace ChargerDrama
{
    internal static class MascotModes
    {
        public const string Always = "always";
        public const string Reacting = "reacting";
        public const string Hidden = "hidden";
    }

    // %APPDATA%\ChargerDrama\settings.json (also reads the file written by the old PowerShell version)
    internal sealed class Settings
    {
        public string Voice = "Mad Scientist";
        public bool AutoMute = true;
        public string MascotMode = MascotModes.Always;
        public int MascotX = int.MinValue;
        public int MascotY = int.MinValue;

        public bool HasMascotPosition { get { return MascotX != int.MinValue && MascotY != int.MinValue; } }

        public static Settings Load()
        {
            var s = new Settings();
            try
            {
                if (!File.Exists(AppPaths.SettingsFile)) return s;
                var d = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(AppPaths.SettingsFile));
                if (d == null) return s;
                foreach (var kv in d)
                {
                    if (kv.Value == null) continue;
                    switch (kv.Key.ToLowerInvariant())
                    {
                        case "voice": s.Voice = Convert.ToString(kv.Value); break;
                        case "automute": s.AutoMute = Convert.ToBoolean(kv.Value); break;
                        case "mascotmode":
                            var m = Convert.ToString(kv.Value);
                            if (m == MascotModes.Always || m == MascotModes.Reacting || m == MascotModes.Hidden) s.MascotMode = m;
                            break;
                        case "mascotx": s.MascotX = Convert.ToInt32(kv.Value); break;
                        case "mascoty": s.MascotY = Convert.ToInt32(kv.Value); break;
                    }
                }
            }
            catch (Exception ex) { AppPaths.LogError("Settings.Load", ex); }
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(AppPaths.DataDir);
                var d = new Dictionary<string, object>
                {
                    { "voice", Voice },
                    { "autoMute", AutoMute },
                    { "mascotMode", MascotMode },
                };
                if (HasMascotPosition) { d.Add("mascotX", MascotX); d.Add("mascotY", MascotY); }
                File.WriteAllText(AppPaths.SettingsFile, new JavaScriptSerializer().Serialize(d));
            }
            catch (Exception ex) { AppPaths.LogError("Settings.Save", ex); }
        }
    }
}
