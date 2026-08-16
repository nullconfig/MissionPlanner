using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace MissionPlanner.Prototypes.Avalonia.FlightData
{
    public partial class AttitudeIndicator : UserControl
    {
        private readonly TranslateTransform _pitchTransform;
        private readonly RotateTransform _rollTransform;

        public AttitudeIndicator()
        {
            InitializeComponent();

            // Named lookup via this.FindControl<T> is for the visual tree
            // (IControl/StyledElement); RotateTransform/TranslateTransform aren't
            // controls, so they're found by indexing into the TransformGroup on the
            // one control that carries them, not by giving the transforms their own
            // x:Name.
            var horizonDisk = this.FindControl<Grid>("HorizonDisk");
            var group = (TransformGroup)horizonDisk.RenderTransform;
            _pitchTransform = (TranslateTransform)group.Children[0];
            _rollTransform = (RotateTransform)group.Children[1];
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        // Pitch scale is 3 px/degree - chosen so the +-90deg full range this
        // MAVLink field can report doesn't fly the horizon disk off past its clipped
        // circle instantly, while still giving a visible shift for normal +-20deg
        // flight attitudes. Sign convention (nose-up moves the horizon down, i.e.
        // more sky visible) matches the standard real-world AI - not yet confirmed
        // against a real connected vehicle, see this bundle's own comment header.
        public void SetAttitude(double rollDegrees, double pitchDegrees)
        {
            _pitchTransform.Y = pitchDegrees * 3;
            _rollTransform.Angle = -rollDegrees;
        }
    }
}
