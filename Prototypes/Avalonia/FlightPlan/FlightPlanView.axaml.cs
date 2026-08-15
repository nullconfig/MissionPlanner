using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace MissionPlanner.Prototypes.Avalonia.FlightPlan
{
    public partial class FlightPlanView : UserControl
    {
        public FlightPlanView()
        {
            InitializeComponent();
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    }
}
