using System.Collections.Generic;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using BruTile;
using BruTile.Predefined;
using BruTile.Web;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Projections;
using Mapsui.Providers;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.Tiling.Layers;
using Mapsui.UI.Avalonia;

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
        private readonly TextBlock _satText;
        private readonly TextBlock _positionText;
        private readonly TextBlock _modeText;
        private readonly TextBlock _armedText;

        private readonly MapControl _mapControl;
        private readonly MemoryLayer _vehicleLayer;
        private bool _hasCenteredOnce;

        private readonly AttitudeIndicator _attitudeGauge;
        private readonly CompassGauge _compassWidget;
        private readonly Button _attitudeToggle;
        private readonly Button _compassToggle;

        private readonly TileLayer _streetLayer;
        private readonly TileLayer _satelliteLayer;
        private readonly Button _streetToggle;
        private readonly Button _satelliteToggle;

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
            _satText = this.FindControl<TextBlock>("SatText");
            _positionText = this.FindControl<TextBlock>("PositionText");
            _modeText = this.FindControl<TextBlock>("ModeText");
            _armedText = this.FindControl<TextBlock>("ArmedText");

            _attitudeGauge = this.FindControl<AttitudeIndicator>("AttitudeGauge");
            _compassWidget = this.FindControl<CompassGauge>("CompassWidget");
            _attitudeToggle = this.FindControl<Button>("AttitudeToggle");
            _compassToggle = this.FindControl<Button>("CompassToggle");

            // Base OSM tile layer plus one MemoryLayer holding a single point feature
            // (the vehicle marker) - MemoryLayer.Features is reassigned and
            // DataHasChanged() called on every position update rather than mutating
            // the connected vehicle's real MAVLink data in any way; this is purely a
            // map-display concern. SphericalMercator.FromLonLat converts the vehicle's
            // WGS84 lat/lng (what CurrentState reports) into the map's internal
            // projected coordinate system - the args are (longitude, latitude), not
            // (lat, lng), easy to get backwards.
            _streetToggle = this.FindControl<Button>("StreetToggle");
            _satelliteToggle = this.FindControl<Button>("SatelliteToggle");

            _mapControl = this.FindControl<MapControl>("Map");

            // Two base tile layers, toggled via Enabled rather than added/removed -
            // OpenStreetMap.CreateTileLayer() has its own built-in helper; satellite
            // imagery doesn't (Mapsui's built-in KnownTileSource enum has no
            // no-API-key satellite/aerial entry - HereSatellite and BingAerial both
            // require a key), so it's built directly from BruTile's HttpTileSource
            // against Esri's public World_Imagery service - no API key, same
            // server.arcgisonline.com/.../MapServer/tile/{z}/{y}/{x} pattern as
            // Esri's other free layers (World_Topo_Map etc., confirmed against
            // BruTile's own KnownTileSources.cs source before using it) - note the
            // {z}/{y}/{x} segment order, not {z}/{x}/{y},  and no {s} subdomain
            // token, unlike OSM - Esri serves from one host, not several.
            _streetLayer = OpenStreetMap.CreateTileLayer();
            _satelliteLayer = new TileLayer(new HttpTileSource(
                new GlobalSphericalMercator(),
                "https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}",
                name: "Esri World Imagery",
                attribution: new Attribution(
                    "© Esri, Maxar, Earthstar Geographics, and the GIS User Community",
                    "https://www.esri.com")))
            {
                Name = "Esri World Imagery",
                Enabled = false,
            };
            _mapControl.Map.Layers.Add(_streetLayer);
            _mapControl.Map.Layers.Add(_satelliteLayer);

            _vehicleLayer = new MemoryLayer
            {
                Name = "Vehicle",
                Features = new MemoryProvider(new List<IFeature>()).Features,
                // AccentBlue (#4C8DFF) as a Mapsui Color, not an Avalonia one - the two
                // Color types aren't interchangeable across these two libraries.
                Style = new SymbolStyle
                {
                    SymbolType = SymbolType.Ellipse,
                    Fill = new Mapsui.Styles.Brush(new Mapsui.Styles.Color(76, 141, 255)),
                    Outline = new Mapsui.Styles.Pen(Mapsui.Styles.Color.White, 1.5),
                    SymbolScale = 0.7,
                },
            };
            _mapControl.Map.Layers.Add(_vehicleLayer);

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
                _satText.Text = _viewModel.SatCountText;
                _positionText.Text = _viewModel.PositionText;
                _modeText.Text = _viewModel.ModeText;
                _armedText.Text = _viewModel.ArmedText;
                _armedText.Foreground = _viewModel.Armed
                    ? (IBrush)this.FindResource("Danger")
                    : (IBrush)this.FindResource("TextSecondary");

                UpdateVehicleMarker(_viewModel.Lat, _viewModel.Lng);

                // Both gauges stay updated regardless of which is visible - simpler
                // than only updating the one currently shown, and the cost of
                // updating an invisible UserControl's transforms is negligible.
                _attitudeGauge.SetAttitude(_viewModel.RollDegrees, _viewModel.PitchDegrees);
                _compassWidget.SetHeading(_viewModel.HeadingDegrees);
            });

        // Attitude/compass toggle - the compass *replaces* the attitude gauge, not a
        // second gauge shown alongside it, per the user's own request. Same
        // FilterChip/FilterChipActive toggle pattern as Terminal's message filters and
        // Config/Tuning's Motor Test/Parameters sub-nav (DemoApp/MainWindow.axaml.cs's
        // OnConfigTuningSubNavClick) - FilterChip stays applied on both buttons always,
        // only FilterChipActive toggles, matching the fix for the "unselected buttons
        // oval" bug found earlier this session (removing the base class on activate
        // was the actual bug there, not anything about which one starts active).
        private void OnGaugeToggleClick(object sender, RoutedEventArgs e)
        {
            var showAttitude = ReferenceEquals(sender, _attitudeToggle);

            _attitudeGauge.IsVisible = showAttitude;
            _compassWidget.IsVisible = !showAttitude;

            if (showAttitude)
                _attitudeToggle.Classes.Add("FilterChipActive");
            else
                _attitudeToggle.Classes.Remove("FilterChipActive");

            if (!showAttitude)
                _compassToggle.Classes.Add("FilterChipActive");
            else
                _compassToggle.Classes.Remove("FilterChipActive");
        }

        // Street/Satellite map-style toggle - same FilterChip/FilterChipActive
        // pattern and same reasoning as the attitude/compass toggle above. Toggling
        // Enabled (not adding/removing the layer from Map.Layers) keeps both tile
        // sources alive so switching back doesn't re-fetch/re-cache tiles it already
        // has.
        private void OnMapStyleToggleClick(object sender, RoutedEventArgs e)
        {
            var showStreet = ReferenceEquals(sender, _streetToggle);

            _streetLayer.Enabled = showStreet;
            _satelliteLayer.Enabled = !showStreet;
            _mapControl.Refresh();

            if (showStreet)
                _streetToggle.Classes.Add("FilterChipActive");
            else
                _streetToggle.Classes.Remove("FilterChipActive");

            if (!showStreet)
                _satelliteToggle.Classes.Add("FilterChipActive");
            else
                _satelliteToggle.Classes.Remove("FilterChipActive");
        }

        // A (0, 0) lat/lng is "no GPS fix yet", not a real position off the coast of
        // Africa - matches CurrentState's own default/uninitialized value, so skip
        // placing (and especially centering on) a marker there.
        private void UpdateVehicleMarker(double lat, double lng)
        {
            if (lat == 0 && lng == 0)
                return;

            var (x, y) = SphericalMercator.FromLonLat(lng, lat);
            var point = new MPoint(x, y);
            _vehicleLayer.Features = new List<IFeature> { new PointFeature(point) };
            _vehicleLayer.DataHasChanged();

            // Always-follow for this chunk - no "Following" toggle yet (see the
            // ArduDeck reference screenshot's own toggle, a later chunk). Zooms in
            // once on the first real fix rather than re-zooming on every update, which
            // would fight any manual zoom the user does afterward.
            if (!_hasCenteredOnce)
            {
                // Resolutions is ordered zoomed-out (index 0, whole world) to
                // zoomed-in (last index) - meters/pixel shrinks as the index grows.
                // Near the end of the list, not near the start, gives a reasonable
                // street-level initial view instead of a whole-continent one.
                var resolutions = _mapControl.Map.Navigator.Resolutions;
                var zoomIndex = System.Math.Max(0, resolutions.Count - 6);
                _mapControl.Map.Navigator.CenterOnAndZoomTo(point, resolutions[zoomIndex]);
                _hasCenteredOnce = true;
            }
            else
            {
                _mapControl.Map.Navigator.CenterOn(point.X, point.Y);
            }
        }
    }
}
