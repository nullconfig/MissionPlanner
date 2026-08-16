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
            // (or download fresh ones) that the real Mono app uses - see
            // .okf/config-tuning/parameters/overview.md.
            Settings.CustomUserDataDirectory =
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);

            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }

        public static AppBuilder BuildAvaloniaApp() =>
            AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace();
    }
}
