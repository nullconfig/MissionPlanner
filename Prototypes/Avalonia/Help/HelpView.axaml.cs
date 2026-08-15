using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace MissionPlanner.Prototypes.Avalonia.Help
{
    public partial class HelpView : UserControl
    {
        public HelpView()
        {
            InitializeComponent();
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        private static void OpenUrl(string url) =>
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

        private void OnOpenArduPilotDocsClick(object sender, RoutedEventArgs e) =>
            OpenUrl("https://ardupilot.org/copter/");

        private void OnOpenGitHubClick(object sender, RoutedEventArgs e) =>
            OpenUrl("https://github.com/ArduPilot/MissionPlanner");
    }
}
