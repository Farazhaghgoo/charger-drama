using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("Charger Drama")]
[assembly: AssemblyProduct("Charger Drama")]
[assembly: AssemblyDescription("Laughs when you plug in the charger, begs when you pull it out.")]
[assembly: AssemblyVersion("2.0.0.0")]
[assembly: AssemblyFileVersion("2.0.0.0")]

namespace ChargerDrama
{
    // ChargerDrama.exe              -> toggle: switch on, or switch off if it's already running
    //                  --on           switch on only (never off)
    //                  --startup      used by "Start with Windows" (on only, no power-up sound)
    //                  --install      install silently and start
    //                  --portable     run from here without offering to install
    //                  --selftest <dir>   silent checks + mascot pictures, results in <dir>\selftest.txt
    //                  --make-icon <file.ico>
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (Has(args, "--selftest")) return SelfTest.Run(Value(args, "--selftest"));
            if (Has(args, "--make-icon")) { IconMaker.Write(Value(args, "--make-icon")); return 0; }

            SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { AppPaths.LogError("UI thread", e.Exception); };
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e) { AppPaths.LogError("Unhandled", e.ExceptionObject as Exception); };

            if (Has(args, "--install")) { Installer.Install(false); return 0; }

            bool onOnly = Has(args, "--on") || Has(args, "--startup");
            if (!AppPaths.IsRunningInstalledCopy && !Has(args, "--portable") && !onOnly)
            {
                var answer = MessageBox.Show(
                    "Install Charger Drama on this PC?\n\n" +
                    "It puts a shortcut on your desktop that turns it on and off (also Ctrl+Alt+D).\n\n" +
                    "Choose No to just run it from here this time.",
                    AppPaths.AppName, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (answer == DialogResult.Yes) { Installer.Install(true); return 0; }
                if (answer != DialogResult.No) return 0;
            }

            using (var mutex = new Mutex(false, AppPaths.MutexName))
            {
                bool mine;
                try { mine = mutex.WaitOne(0); }
                catch (AbandonedMutexException) { mine = true; }

                if (!mine)
                {
                    // already running: the shortcut / hotkey means "switch off"
                    if (!onOnly) Instance.SignalQuit();
                    return 0;
                }
                try
                {
                    Application.Run(new TrayApp(Has(args, "--startup"), Has(args, "--welcome")));
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
            }
            return 0;
        }

        private static bool Has(string[] args, string name)
        {
            return args.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
        }

        private static string Value(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();
    }
}
