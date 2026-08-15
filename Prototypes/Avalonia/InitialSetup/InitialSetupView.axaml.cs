using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace MissionPlanner.Prototypes.Avalonia.InitialSetup
{
    public partial class InitialSetupView : UserControl
    {
        public InitialSetupView(MAVLinkInterface mav)
        {
            InitializeComponent();

            var viewModel = new InitialSetupViewModel(mav);
            this.FindControl<TextBlock>("FirmwareText").Text = viewModel.FirmwareText;
            this.FindControl<TextBlock>("VehicleTypeText").Text = viewModel.VehicleTypeText;
            this.FindControl<TextBlock>("FrameClassText").Text = viewModel.FrameClassText;
            this.FindControl<TextBlock>("FrameTypeText").Text = viewModel.FrameTypeText;
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    }
}
