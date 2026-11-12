using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ChargerDrama
{
    internal static class MicWatcher
    {
        private const string ConsentPath =
            @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone";

        private static string ownKeyName;

        public static bool IncludeOwnProcess { get; set; }

        public static bool IsMicInUse(out string appName)
        {
            appName = null;
            try
            {
                string own = null;
                if (!IncludeOwnProcess) own = GetOwnKeyName();

                using (RegistryKey root = Registry.CurrentUser.OpenSubKey(ConsentPath, false))
                {
                    if (root == null) return false;
                    foreach (string name in root.GetSubKeyNames())
                    {
                        if (string.Equals(name, "NonPackaged", StringComparison.OrdinalIgnoreCase))
                        {
                            using (RegistryKey np = root.OpenSubKey(name, false))
                            {
                                if (np == null) continue;
                                foreach (string sub in np.GetSubKeyNames())
                                {
                                    if (own != null && string.Equals(sub, own, StringComparison.OrdinalIgnoreCase)) continue;
                                    using (RegistryKey k = np.OpenSubKey(sub, false))
                                    {
                                        if (InUse(k))
                                        {
                                            appName = FriendlyNonPackaged(sub);
                                            return true;
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            using (RegistryKey k = root.OpenSubKey(name, false))
                            {
                                if (InUse(k))
                                {
                                    int us = name.IndexOf('_');
                                    appName = us > 0 ? name.Substring(0, us) : name;
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                appName = null;
                return false;
            }
            return false;
        }

        private static bool InUse(RegistryKey k)
        {
            if (k == null) return false;
            long start = ToLong(k.GetValue("LastUsedTimeStart"));
            long stop = ToLong(k.GetValue("LastUsedTimeStop"));
            return start > 0 && stop == 0;
        }

        private static long ToLong(object v)
        {
            if (v == null) return 0;
            try { return Convert.ToInt64(v); }
            catch (Exception) { return 0; }
        }

        private static string FriendlyNonPackaged(string keyName)
        {
            try
            {
                string path = keyName.Replace('#', '\\');
                string f = Path.GetFileName(path);
                return string.IsNullOrEmpty(f) ? keyName : f;
            }
            catch (Exception) { return keyName; }
        }

        private static string GetOwnKeyName()
        {
            if (ownKeyName == null)
            {
                try
                {
                    using (Process p = Process.GetCurrentProcess())
                    {
                        ownKeyName = p.MainModule.FileName.Replace('\\', '#');
                    }
                }
                catch (Exception)
                {
                    ownKeyName = "";
                }
            }
            return ownKeyName.Length == 0 ? null : ownKeyName;
        }
    }

    internal static class UserState
    {
        private const int QUNS_BUSY = 2;
        private const int QUNS_RUNNING_D3D_FULL_SCREEN = 3;
        private const int QUNS_PRESENTATION_MODE = 4;

        [DllImport("shell32.dll")]
        private static extern int SHQueryUserNotificationState(out int state);

        public static bool IsBusyFullscreen()
        {
            try
            {
                int state;
                if (SHQueryUserNotificationState(out state) != 0) return false;
                return state == QUNS_BUSY || state == QUNS_RUNNING_D3D_FULL_SCREEN || state == QUNS_PRESENTATION_MODE;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
