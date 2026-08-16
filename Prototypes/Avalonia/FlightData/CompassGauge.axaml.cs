using System.Globalization;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace MissionPlanner.Prototypes.Avalonia.FlightData
{
    public partial class CompassGauge : UserControl
    {
        private readonly RotateTransform _cardTransform;
        private readonly TextBlock _headingReadout;

        public CompassGauge()
        {
            InitializeComponent();

            var compassCard = this.FindControl<Grid>("CompassCard");
            _cardTransform = (RotateTransform)compassCard.RenderTransform;
            _headingReadout = this.FindControl<TextBlock>("HeadingReadout");
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        public void SetHeading(double headingDegrees)
        {
            _cardTransform.Angle = -headingDegrees;
            _headingReadout.Text = headingDegrees.ToString("0", CultureInfo.InvariantCulture) + "°";
        }
    }
}
