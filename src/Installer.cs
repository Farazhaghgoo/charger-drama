using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace ChargerDrama
{
    // Installs for the current user only (no admin): %LOCALAPPDATA%\ChargerDrama\ChargerDrama.exe,
    // a Desktop on/off shortcut with Ctrl+Alt+D, and a Start menu shortcut.
    internal static class Installer
    {
        public const string Hotkey = "CTRL+ALT+D";

        public static void Install(bool interactive)
        {
            try
            {
                StopRunningCopies();

                Directory.CreateDirectory(AppPaths.InstallDir);
                var source = Path.GetFullPath(Application.ExecutablePath);
                if (!string.Equals(source, Path.GetFullPath(AppPaths.InstalledExe), StringComparison.OrdinalIgnoreCase))
                    CopyWithRetry(source, AppPaths.InstalledExe);

                // leftovers from the old PowerShell version
                foreach (var old in new[] { "charger-drama.ps1", "toggle.ps1", "ChargerDrama.ico" })
                    TryDelete(Path.Combine(AppPaths.InstallDir, old));
                SoundExtractor.Extract(true);

                CreateShortcut(AppPaths.DesktopShortcut, AppPaths.InstalledExe, "", Hotkey, "Turn Charger Drama on/off (Ctrl+Alt+D)");
                CreateShortcut(AppPaths.StartMenuShortcut, AppPaths.InstalledExe, "", null, "Turn Charger Drama on/off");
                if (File.Exists(AppPaths.StartupShortcut)) SetStartWithWindows(true, AppPaths.InstalledExe);

                Process.Start(new ProcessStartInfo(AppPaths.InstalledExe, "--on --welcome") { UseShellExecute = false });
            }
            catch (Exception ex)
            {
                AppPaths.LogError("Install", ex);
                if (interactive)
                    MessageBox.Show("Sorry, installing failed:\n\n" + ex.Message, AppPaths.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Called from the tray menu of the installed copy, which then exits.
        public static void Uninstall(bool deleteUserData)
        {
            TryDelete(AppPaths.DesktopShortcut);
            TryDelete(AppPaths.StartMenuShortcut);
            TryDelete(AppPaths.StartupShortcut);
            if (deleteUserData)
            {
                try { Directory.Delete(AppPaths.DataDir, true); } catch (Exception ex) { AppPaths.LogError("Uninstall data", ex); }
            }
            // the running exe can't delete its own folder: let a hidden cmd do it after we exit
            try
            {
                var psi = new ProcessStartInfo("cmd.exe", "/c ping -n 4 127.0.0.1 >nul & rmdir /s /q \"" + AppPaths.InstallDir + "\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden,
                };
                Process.Start(psi);
            }
            catch (Exception ex) { AppPaths.LogError("Uninstall folder", ex); }
        }

        public static bool StartsWithWindows { get { return File.Exists(AppPaths.StartupShortcut); } }

        public static void SetStartWithWindows(bool on, string exePath)
        {
            if (on) CreateShortcut(AppPaths.StartupShortcut, exePath, "--startup", null, "Start Charger Drama");
            else TryDelete(AppPaths.StartupShortcut);
        }

        // Old PowerShell version: stop it silently (it would otherwise say goodbye out loud).
        // New version: ask it to switch off, then make sure.
        private static void StopRunningCopies()
        {
            try
            {
                using (var q = new ManagementObjectSearcher("SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name = 'powershell.exe'"))
                {
                    foreach (ManagementObject p in q.Get())
                    {
                        var cmd = Convert.ToString(p["CommandLine"]);
                        if (cmd != null && cmd.IndexOf("charger-drama.ps1", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            try { Process.GetProcessById(Convert.ToInt32(p["ProcessId"])).Kill(); } catch { }
                        }
                    }
                }
            }
            catch (Exception ex) { AppPaths.LogError("Stop legacy", ex); }

            if (Instance.IsRunning())
            {
                Instance.SignalQuit();
                Instance.WaitUntilStopped(4000);
            }
            var me = Process.GetCurrentProcess().Id;
            foreach (var p in Process.GetProcessesByName("ChargerDrama"))
            {
                if (p.Id == me) continue;
                try { p.Kill(); p.WaitForExit(2000); } catch { }
            }
            Thread.Sleep(200);
        }

        private static void CopyWithRetry(string from, string to)
        {
            for (int i = 0; ; i++)
            {
                try { File.Copy(from, to, true); return; }
                catch (IOException)
                {
                    if (i >= 10) throw;
                    Thread.Sleep(300);
                }
            }
        }

        private static void CreateShortcut(string lnkPath, string target, string args, string hotkey, string description)
        {
            object shell = null, lnk = null;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(lnkPath));
                shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                dynamic sh = shell;
                lnk = sh.CreateShortcut(lnkPath);
                dynamic l = lnk;
                l.TargetPath = target;
                l.Arguments = args;
                l.WorkingDirectory = Path.GetDirectoryName(target);
                l.IconLocation = target + ",0";
                l.Description = description;
                if (hotkey != null) l.Hotkey = hotkey;
                l.Save();
            }
            catch (Exception ex) { AppPaths.LogError("Shortcut " + lnkPath, ex); }
            finally
            {
                if (lnk != null) Marshal.FinalReleaseComObject(lnk);
                if (shell != null) Marshal.FinalReleaseComObject(shell);
            }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }

    // The built-in voice packs live inside the exe; they are unpacked next to the installed program.
    internal static class SoundExtractor
    {
        public static void Extract(bool force)
        {
            var dir = AppPaths.BuiltInSoundsDir;
            var stampFile = Path.Combine(dir, ".build");
            var asm = Assembly.GetExecutingAssembly();
            var stamp = asm.ManifestModule.ModuleVersionId.ToString();
            try
            {
                if (!force && File.Exists(stampFile) && File.ReadAllText(stampFile) == stamp) return;
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
                foreach (var name in asm.GetManifestResourceNames())
                {
                    if (!name.StartsWith("sounds/", StringComparison.Ordinal)) continue;
                    var target = Path.Combine(AppPaths.InstallDir, name.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    using (var src = asm.GetManifestResourceStream(name))
                    using (var dst = File.Create(target))
                        src.CopyTo(dst);
                }
                File.WriteAllText(stampFile, stamp);
            }
            catch (Exception ex) { AppPaths.LogError("Extract sounds", ex); }
        }
    }
}
