using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace MissionPlanner.Prototypes.Avalonia.Simulation
{
    public partial class SimulationView : UserControl
    {
        public SimulationView()
        {
            InitializeComponent();

            var viewModel = new SimulationViewModel();
            this.FindControl<TextBlock>("StatusHeading").Text = viewModel.SitlAvailable ? "SITL found" : "SITL not found";
            this.FindControl<TextBlock>("StatusText").Text = viewModel.StatusText;
            if (viewModel.SitlAvailable)
                this.FindControl<Border>("StatusDot").Background = (IBrush)this.FindResource("TerminalGreen");
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    }
}
