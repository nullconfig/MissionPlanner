using System;
using System.Threading;
using ReactiveUI;

namespace MissionPlanner.Prototypes.Avalonia.FlightData
{
    // Real (non-mocked) telemetry readout from a connected MAVLinkInterface's
    // CurrentState (mav.MAV.cs) - the exact same properties Mission Planner's own HUD
    // control data-binds to (GCSViews/FlightData.Designer.cs, search "bindingSourceHud":
    // alt, groundspeed, airspeed, roll/pitch/yaw, battery_voltage, battery_remaining,
    // gpsstatus, mode, armed all come from there unmodified). No Avalonia dependency
    // here (matches ConfigMotorTestViewModel/TerminalViewModel) - polls on a plain
    // System.Threading.Timer since CurrentState has no "value changed" event to hook,
    // same as the real app's own timer-driven HUD refresh. ReactiveObject (ReactiveUI,
    // added 2026-08-15) replaces a hand-rolled INotifyPropertyChanged.
    public class FlightDataViewModel : ReactiveObject, IDisposable
    {
        private readonly MAVLinkInterface _mav;
        private readonly Timer _timer;

        private string _attitudeText = "roll 0.0° · pitch 0.0°";
        public string AttitudeText { get => _attitudeText; private set => this.RaiseAndSetIfChanged(ref _attitudeText, value); }

        private string _headingText = "0°";
        public string HeadingText { get => _headingText; private set => this.RaiseAndSetIfChanged(ref _headingText, value); }

        private string _altitudeText = "0.0 m";
        public string AltitudeText { get => _altitudeText; private set => this.RaiseAndSetIfChanged(ref _altitudeText, value); }

        private string _groundSpeedText = "0.0 m/s";
        public string GroundSpeedText { get => _groundSpeedText; private set => this.RaiseAndSetIfChanged(ref _groundSpeedText, value); }

        private string _airSpeedText = "0.0 m/s";
        public string AirSpeedText { get => _airSpeedText; private set => this.RaiseAndSetIfChanged(ref _airSpeedText, value); }

        private string _batteryText = "0.00 V · 0%";
        public string BatteryText { get => _batteryText; private set => this.RaiseAndSetIfChanged(ref _batteryText, value); }

        private string _currentText = "0.0 A";
        public string CurrentText { get => _currentText; private set => this.RaiseAndSetIfChanged(ref _currentText, value); }

        private string _gpsText = "no fix · 0 sats";
        public string GpsText { get => _gpsText; private set => this.RaiseAndSetIfChanged(ref _gpsText, value); }

        // Just the satellite count, separate from GpsText's combined "fix · sats"
        // string - the top status bar's compact SAT readout doesn't want the fix-type
        // text (that stays in GpsText, in the floating overlay card).
        private string _satCountText = "0";
        public string SatCountText { get => _satCountText; private set => this.RaiseAndSetIfChanged(ref _satCountText, value); }

        private string _positionText = "0.000000, 0.000000";
        public string PositionText { get => _positionText; private set => this.RaiseAndSetIfChanged(ref _positionText, value); }

        // Raw lat/lng (not just PositionText's formatted string) - the map's vehicle
        // marker needs actual doubles to feed SphericalMercator.FromLonLat, not
        // something to re-parse out of display text.
        private double _lat;
        public double Lat { get => _lat; private set => this.RaiseAndSetIfChanged(ref _lat, value); }

        private double _lng;
        public double Lng { get => _lng; private set => this.RaiseAndSetIfChanged(ref _lng, value); }

        // Raw heading in degrees - same reasoning as Lat/Lng, needed for marker
        // rotation (a later chunk; HeadingText alone can't drive a rotation transform).
        private double _headingDegrees;
        public double HeadingDegrees { get => _headingDegrees; private set => this.RaiseAndSetIfChanged(ref _headingDegrees, value); }

        // Raw roll/pitch in degrees - AttitudeText alone (a formatted string) can't
        // drive AttitudeIndicator's rotation/translation transforms.
        private double _rollDegrees;
        public double RollDegrees { get => _rollDegrees; private set => this.RaiseAndSetIfChanged(ref _rollDegrees, value); }

        private double _pitchDegrees;
        public double PitchDegrees { get => _pitchDegrees; private set => this.RaiseAndSetIfChanged(ref _pitchDegrees, value); }

        private string _modeText = "-";
        public string ModeText { get => _modeText; private set => this.RaiseAndSetIfChanged(ref _modeText, value); }

        private string _armedText = "DISARMED";
        public string ArmedText { get => _armedText; private set => this.RaiseAndSetIfChanged(ref _armedText, value); }

        private bool _armed;
        public bool Armed { get => _armed; private set => this.RaiseAndSetIfChanged(ref _armed, value); }

        public FlightDataViewModel(MAVLinkInterface mav)
        {
            _mav = mav;
            Refresh(null);
            _timer = new Timer(Refresh, null, 200, 200);
        }

        public void Dispose() => _timer.Dispose();

        private void Refresh(object state)
        {
            var cs = _mav.MAV.cs;

            AttitudeText = $"roll {cs.roll:0.0}° · pitch {cs.pitch:0.0}°";
            HeadingText = $"{cs.yaw:0}°";
            AltitudeText = $"{cs.alt:0.0} m";
            GroundSpeedText = $"{cs.groundspeed:0.0} m/s";
            AirSpeedText = $"{cs.airspeed:0.0} m/s";
            BatteryText = $"{cs.battery_voltage:0.00} V · {cs.battery_remaining}%";
            CurrentText = $"{cs.current:0.0} A";

            var fix = (MAVLink.GPS_FIX_TYPE)(int)cs.gpsstatus;
            GpsText = $"{fix} · {cs.satcount:0} sats";
            SatCountText = $"{cs.satcount:0}";
            PositionText = $"{cs.lat:0.000000}, {cs.lng:0.000000}";
            Lat = cs.lat;
            Lng = cs.lng;
            HeadingDegrees = cs.yaw;
            RollDegrees = cs.roll;
            PitchDegrees = cs.pitch;

            ModeText = cs.mode ?? "-";
            Armed = cs.armed;
            ArmedText = cs.armed ? "ARMED" : "DISARMED";
        }
    }
}
