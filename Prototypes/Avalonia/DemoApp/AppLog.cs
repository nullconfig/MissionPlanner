using System;
using System.IO;
using MissionPlanner.Utilities;

namespace MissionPlanner.Prototypes.Avalonia.DemoApp
{
    // No Avalonia bundle has any logging at all yet, even though the real WinForms
    // app uses log4net everywhere (MainV2.cs/Program.cs, XmlConfigurator against an
    // App.config <log4net> section) - pulling that same XML-config machinery in for
    // one exception was more than this needed, so this is a small, focused file
    // logger instead: plain text, one line per event, append-only.
    //
    // Writes to Settings.GetDataDirectory() - the same per-user directory
    // DemoApp/Program.cs already points CustomUserDataDirectory at, so this lands
    // next to the real cached apm.pdef.xml files, not somewhere new to go looking
    // for. Exists specifically so a live connect-flow test (real hardware, not
    // reproducible in a sandbox with no vehicle attached) leaves a persistent record
    // of the real exception - not just ConnectAsync's truncated ex.Message in the
    // status bar - to read afterward or tail live during testing.
    public static class AppLog
    {
        // Named LogFilePath, not Path - a property named Path on this class would
        // shadow System.IO.Path within the class body, breaking Path.Combine below.
        private static readonly string LogPath = System.IO.Path.Combine(Settings.GetDataDirectory(), "avalonia-demoapp.log");

        public static string LogFilePath => LogPath;

        public static void Write(string message)
        {
            try
            {
                Directory.CreateDirectory(Settings.GetDataDirectory());
                File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
            }
            catch
            {
                // Logging must never itself crash the app - if this throws (disk
                // full, permissions), there's nowhere more useful to report it.
            }
        }

        public static void WriteException(string context, Exception ex) =>
            Write($"{context}: {ex}");
    }
}
