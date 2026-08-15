using System;
using System.IO;
using System.Linq;

namespace MissionPlanner.Prototypes.Avalonia.Simulation
{
    // Real (not faked) environment check: does this machine have ArduPilot SITL tooling
    // (sim_vehicle.py) on PATH? No MAVLinkInterface needed - this doesn't talk to a
    // vehicle, it's about launching one locally. Doesn't launch SITL yet - see
    // README.md for what a contributor would need to add for that (would shell out to
    // sim_vehicle.py similar to how the real app's simulation screen does).
    public class SimulationViewModel
    {
        public bool SitlAvailable { get; }
        public string StatusText { get; }
        public string SitlPath { get; }

        public SimulationViewModel()
        {
            SitlPath = FindOnPath("sim_vehicle.py");
            SitlAvailable = SitlPath != null;
            StatusText = SitlAvailable
                ? $"sim_vehicle.py found at {SitlPath}"
                : "sim_vehicle.py not found on PATH - install ArduPilot SITL tooling to enable launching a simulated vehicle from here";
        }

        private static string FindOnPath(string fileName)
        {
            var path = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(path)) return null;

            foreach (var dir in path.Split(Path.PathSeparator))
            {
                try
                {
                    var candidate = Path.Combine(dir, fileName);
                    if (File.Exists(candidate)) return candidate;
                }
                catch
                {
                    // malformed PATH entry, skip
                }
            }

            return null;
        }
    }
}
