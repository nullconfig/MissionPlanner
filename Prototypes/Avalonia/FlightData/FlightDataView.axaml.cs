using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;

namespace MissionPlanner.Prototypes.Avalonia.FlightData
{
    public partial class FlightDataView : UserControl
    {
        private readonly FlightDataViewModel _viewModel;

        private readonly TextBlock _attitudeText;
        private readonly TextBlock _headingText;
        private readonly TextBlock _altitudeText;
        private readonly TextBlock _groundSpeedText;
        private readonly TextBlock _airSpeedText;
        private readonly TextBlock _batteryText;
        private readonly TextBlock _currentText;
        private readonly TextBlock _gpsText;
        private readonly TextBlock _positionText;
        private readonly TextBlock _modeText;
        private readonly TextBlock _armedText;

        public FlightDataView(MAVLinkInterface mav)
        {
            InitializeComponent();

            _attitudeText = this.FindControl<TextBlock>("AttitudeText");
            _headingText = this.FindControl<TextBlock>("HeadingText");
            _altitudeText = this.FindControl<TextBlock>("AltitudeText");
            _groundSpeedText = this.FindControl<TextBlock>("GroundSpeedText");
            _airSpeedText = this.FindControl<TextBlock>("AirSpeedText");
            _batteryText = this.FindControl<TextBlock>("BatteryText");
            _currentText = this.FindControl<TextBlock>("CurrentText");
            _gpsText = this.FindControl<TextBlock>("GpsText");
            _positionText = this.FindControl<TextBlock>("PositionText");
            _modeText = this.FindControl<TextBlock>("ModeText");
            _armedText = this.FindControl<TextBlock>("ArmedText");

            _viewModel = new FlightDataViewModel(mav);
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            DetachedFromVisualTree += (_, __) => _viewModel.Dispose();
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        // FlightDataViewModel polls on a plain System.Threading.Timer (no Avalonia
        // dependency), so PropertyChanged fires on a threadpool thread - marshal to the
        // UI thread here rather than relying on Avalonia bindings to do it themselves.
        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e) =>
            Dispatcher.UIThread.Post(() =>
            {
                _attitudeText.Text = _viewModel.AttitudeText;
                _headingText.Text = _viewModel.HeadingText;
                _altitudeText.Text = _viewModel.AltitudeText;
                _groundSpeedText.Text = _viewModel.GroundSpeedText;
                _airSpeedText.Text = _viewModel.AirSpeedText;
                _batteryText.Text = _viewModel.BatteryText;
                _currentText.Text = _viewModel.CurrentText;
                _gpsText.Text = _viewModel.GpsText;
                _positionText.Text = _viewModel.PositionText;
                _modeText.Text = _viewModel.ModeText;
                _armedText.Text = _viewModel.ArmedText;
                _armedText.Foreground = _viewModel.Armed
                    ? (IBrush)this.FindResource("Danger")
                    : (IBrush)this.FindResource("TextSecondary");
            });
    }
}
