using Avalonia;
using MissionPlanner.Utilities;

namespace MissionPlanner.Prototypes.Avalonia.DemoApp
{
    internal static class Program
    {
        [System.STAThread]
        public static void Main(string[] args)
        {
            // Settings.GetDataDirectory() only reaches the per-user directory for Mono
            // processes (the real WinForms app's runtime on Linux); a plain .NET process
            // like this one falls back to CommonApplicationData (/usr/share on Unix,
            // never writable by a normal user). Pointing CustomUserDataDirectory at the
            // same per-user location also sidesteps GetUserDataDirectory()'s own legacy
            // migration check, which would otherwise prefer a stale ~/Documents/Mission
            // Planner directory over the real cache if one happens to exist. This is what
            // lets ParameterMetaDataRepository find the same cached apm.pdef.xml files
            // (or download fresh ones) that the real Mono app uses.
            Settings.CustomUserDataDirectory =
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);

            // Global safety net, not just ConnectAsync's own try/catch - anything
            // thrown outside a try/catch this app already has (a Dispatcher.UIThread
            // callback, a fire-and-forget Task.Run continuation) would otherwise
            // crash silently or vanish with no record at all during a live test.
            System.AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                AppLog.WriteException("AppDomain.UnhandledException",
                    e.ExceptionObject as System.Exception ?? new System.Exception(e.ExceptionObject?.ToString()));
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                AppLog.WriteException("TaskScheduler.UnobservedTaskException", e.Exception);
                e.SetObserved();
            };
            AppLog.Write($"--- DemoApp starting, log at {AppLog.LogFilePath} ---");

            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }

        public static AppBuilder BuildAvaloniaApp() =>
            AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace();
    }
}
