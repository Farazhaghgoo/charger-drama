using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace ChargerDrama
{
    internal static class AppPaths
    {
        public const string AppName = "Charger Drama";
        public const string MutexName = @"Local\ChargerDrama";
        public const string QuitEventName = @"Local\ChargerDrama.Quit";

        // set by the self-test so it never touches the real install or settings
        public static string TestHome;

        // the installed copy of the program (and its extracted built-in voices)
        public static string InstallDir
        {
            get
            {
                if (TestHome != null) return Path.Combine(TestHome, "install");
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChargerDrama");
            }
        }
        public static string InstalledExe { get { return Path.Combine(InstallDir, "ChargerDrama.exe"); } }
        public static string BuiltInSoundsDir { get { return Path.Combine(InstallDir, "sounds"); } }

        // the user's own stuff: settings and recorded voices (kept when updating)
        public static string DataDir
        {
            get
            {
                if (TestHome != null) return Path.Combine(TestHome, "data");
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ChargerDrama");
            }
        }
        public static string SettingsFile { get { return Path.Combine(DataDir, "settings.json"); } }
        public static string UserVoicesDir { get { return Path.Combine(DataDir, "voices"); } }
        public static string ErrorLog { get { return Path.Combine(DataDir, "error.log"); } }

        public static string DesktopShortcut { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), AppName + ".lnk"); } }
        public static string StartMenuShortcut { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppName + ".lnk"); } }
        public static string StartupShortcut { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), AppName + ".lnk"); } }

        public static bool IsRunningInstalledCopy
        {
            get { return string.Equals(Path.GetFullPath(Application.ExecutablePath), Path.GetFullPath(InstalledExe), StringComparison.OrdinalIgnoreCase); }
        }

        // Optional diagnostics: put an (empty) trace.log next to the exe, or set CHARGERDRAMA_TRACE
        // to a file path, to log every launch and why the app switches off.
        public static string TracePath
        {
            get
            {
                var path = Environment.GetEnvironmentVariable("CHARGERDRAMA_TRACE");
                if (!string.IsNullOrEmpty(path)) return path;
                path = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "trace.log");
                return File.Exists(path) ? path : null;
            }
        }

        public static void Trace(string message)
        {
            var path = TracePath;
            if (path == null) return;
            try { File.AppendAllText(path, DateTime.Now.ToString("HH:mm:ss.fff") + "  " + message + Environment.NewLine); }
            catch { }
        }

        public static void LogError(string where, Exception ex)
        {
            try
            {
                Directory.CreateDirectory(DataDir);
                File.AppendAllText(ErrorLog, DateTime.Now.ToString("s") + "  " + where + ": " + ex + Environment.NewLine);
            }
            catch { }
        }
    }

    // Talks to an already-running copy of the app.
    internal static class Instance
    {
        public static bool IsRunning()
        {
            try { using (Mutex.OpenExisting(AppPaths.MutexName)) { return true; } }
            catch { return false; }
        }

        // Asks the running copy to switch off (it plays its power-down sound and exits).
        public static bool SignalQuit()
        {
            try
            {
                using (var e = EventWaitHandle.OpenExisting(AppPaths.QuitEventName)) { e.Set(); }
                return true;
            }
            catch { return false; }
        }

        public static bool WaitUntilStopped(int timeoutMs)
        {
            var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < until)
            {
                if (!IsRunning()) return true;
                Thread.Sleep(100);
            }
            return !IsRunning();
        }
    }
}
