# Flight Data bundle

One of the per-tab bundles mirroring Mission Planner's own top-level screens - see
`../DemoApp/README.md`'s "Bundles" section for the full list and how they're hosted.
Real screen this maps to: `GCSViews/FlightData.cs` (6,758 lines) +
`GCSViews/FlightData.Designer.cs` (3,215 lines) - by far the largest single screen in the
real app.

## Status: real map + telemetry overlay, ArduDeck-style rather than a 1:1 port

Live (not mocked) numeric readout - attitude, heading, altitude, ground/air speed,
battery voltage/remaining, current, GPS fix + satellite count, lat/lng, mode, armed state
- all read directly off a connected `MAVLinkInterface`'s `CurrentState` (`mav.MAV.cs`),
polled every 200ms. These are the exact same properties Mission Planner's own HUD control
data-binds to - see `GCSViews/FlightData.Designer.cs`, search `bindingSourceHud`, e.g.
`this.hud1.DataBindings.Add(new System.Windows.Forms.Binding("alt", this.bindingSourceHud,
"alt", true))`.

**2026-08-16**: a real map (`Mapsui.Avalonia12`, an Avalonia-native map library - see
"Map library" below) now fills the tab, with a live vehicle marker that follows the
connected vehicle's real lat/lng. This is a deliberate design departure from the real
screen, not a misreading of it: the real `GCSViews/FlightData.cs` puts the HUD and map
side by side as separate panels (confirmed both by source and the official
ardupilot.org docs - see `.okf/flight-data/overview.md`'s "Official docs cross-check").
This bundle instead follows a direct user request to draw on other modern GCS apps
(a screenshot of ArduDeck was the concrete reference) that overlay live telemetry
directly on top of the map rather than beside it. Concretely: a top status bar (mode/
armed pills, HDG/ALT/SPD/BAT/SAT) sits above the map as its own row, and the remaining
detail values (attitude, air speed, current, GPS fix, position) - what the real screen's
"Gauges" tab covers - are condensed into one floating card in the map's bottom-left
corner, translucent so map tiles stay visible behind it, rather than a separate tab.
Real MP's "Messages" tab is deliberately not duplicated here - `DemoApp`'s persistent
bottom Terminal console (see `../DemoApp/README.md`) already covers that need
app-wide, not per-tab.

**2026-08-16**: an `AttitudeIndicator` (simplified artificial horizon - rotating/
translating sky-ground disk, fixed aircraft chevron, fixed roll pointer; no pitch-ladder
ticks yet) and a `CompassGauge` (N/E/S/W card that rotates opposite to heading behind a
fixed pointer, plus a numeric heading readout) now sit top-left on the map, matching the
real screen's own HUD position (see `.okf/flight-data/overview.md`'s "Official docs
cross-check" - this is the one part of this bundle's overlay design that does match the
real screen's layout, even though the rest deliberately doesn't). A `FilterChip`/
`FilterChipActive` toggle switches between them - the compass *replaces* the attitude
gauge, not a second widget shown alongside it, per direct user request. Both widgets are
plain UserControls with a public `SetAttitude(roll, pitch)` / `SetHeading(heading)`
method (same manual-push style as the rest of this bundle, not Avalonia bindings) and no
`UserControl.Resources` of their own - they rely on `FlightDataView`'s resource
dictionary (`TextPrimary`/`AccentBlue`/`AccentOrange`/`CardBorder`), since both are only
ever hosted inside it.

**Not yet confirmed against a real vehicle**: the pitch/roll sign conventions
(`AttitudeIndicator.SetAttitude`'s comment header) follow the standard real-world
artificial-horizon convention but haven't been checked against actual telemetry - this
sandbox has no live vehicle/SITL to connect. `FlightDataView` itself (and so both new
gauges) only ever gets constructed once a connection succeeds (same
`ConnectAsync`-gated pattern as Motor Test/Parameters), so a clean `DemoApp` launch only
proves the app shell starts - not that this specific view constructs without error. Confirm
by connecting and checking both toggle states before treating the sign conventions as
correct.

What's **not** here yet: pitch-ladder tick marks on the attitude indicator, intercardinal
(NE/SE/SW/NW) marks on the compass, an ArduDeck-style "Following" toggle for the map
(it always follows the vehicle for now, no way to pan away and stay put), and the rest
of the real screen's 14-tab "Control and Status" panel beyond Gauges/Messages (Actions,
PreFlight, Status, Servo/Relay, Aux Function, Scripts, Payload Control, Telemetry Logs,
DataFlash Logs, Simple Actions, Transponder).

## Map library

`Mapsui.Avalonia12` (v5.1.0) - not the plain `Mapsui.Avalonia` package, since Mapsui's
own docs are explicit that Avalonia 12 apps need the "12"-suffixed package (this app
targets Avalonia 12.1.1); XAML/C# API is otherwise identical between the two. Chosen
after a NuGet search turned up no other well-adopted Avalonia-native map control -
GMap.NET, what the real WinForms app uses, has no Avalonia support, and building a
custom tile renderer would duplicate what Mapsui already solves. This was also the
"first real dependency to solve" blocker `../FlightPlan/README.md`'s own "What's next"
already flagged as shared between the two bundles - FlightPlan (a pure scaffold, no map
of its own yet) can reuse the same package once it starts real work.

**2026-08-16 - Street/Satellite toggle added**: two base tile layers now coexist in
`Map.Layers`, switched via a `FilterChip` pair (top-right - the opposite corner from
the attitude/compass toggle) that flips each layer's `Enabled` property rather than
adding/removing it, so switching back doesn't re-fetch tiles already cached. Street is
`Mapsui.Tiling.OpenStreetMap.CreateTileLayer()`, the plain default OSM source with its
own Mapsui helper. Satellite has no equivalent helper - checked BruTile's own
`KnownTileSources.cs` source directly rather than guessing, and its built-in
`KnownTileSource` enum has no free satellite/aerial entry (`HereSatellite` and
`BingAerial` both require an API key) - so it's built directly from BruTile (Mapsui's
underlying tiling library) against Esri's public World_Imagery service, the same
`server.arcgisonline.com/.../MapServer/tile/{z}/{y}/{x}` pattern as Esri's other free
layers in that same source file (`World_Topo_Map` etc.) - no API key, just requires
attribution (the `BruTile.Attribution` passed to `HttpTileSource` provides it). An
initial pass used OpenTopoMap (terrain-style, not satellite imagery) before direct
user feedback asked for satellite specifically instead - swapped, not layered on top:

```csharp
new TileLayer(new HttpTileSource(
    new GlobalSphericalMercator(),
    "https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}",
    name: "Esri World Imagery",
    attribution: new Attribution(...)))
```

Note the `{z}/{y}/{x}` segment order (not `{z}/{x}/{y}`) and no `{s}` subdomain token -
Esri serves from a single host, unlike OSM's multi-subdomain round-robin.

`TileLayer` lives in `Mapsui.Tiling.Layers`, not `Mapsui.Layers` where the vehicle
marker's `MemoryLayer` lives - easy to reach for the wrong namespace first (the build
did, before the compiler said otherwise).

The vehicle marker is a `MemoryLayer` holding
a single `PointFeature`, reassigned (not mutated in place) and `DataHasChanged()`-ed on
every position update; `Mapsui.Projections.SphericalMercator.FromLonLat(lng, lat)`
converts the vehicle's WGS84 coordinates (what `CurrentState` reports) into Mapsui's
internal projected coordinate system - note the argument order is (longitude,
latitude), easy to get backwards.

## How it's wired

`FlightDataViewModel(MAVLinkInterface mav)` has no Avalonia dependency (matches
`ConfigMotorTestViewModel`) - it polls on a plain `System.Threading.Timer` (`CurrentState`
has no "value changed" event to hook, same reason the real app's HUD refresh is
timer-driven too) and raises `PropertyChanged` from a threadpool thread. `FlightDataView`'s
code-behind marshals updates onto the UI thread via `Dispatcher.UIThread.Post` rather than
relying on Avalonia's data-binding to do it, consistent with how the other bundles handle
cross-thread updates (see `../Terminal/README.md`).

## What's next

1. Confirm the attitude/compass gauges' pitch/roll/heading sign conventions against a
   real connected vehicle - see "Attitude/compass gauges" above, not yet checked.
2. Pitch-ladder tick marks (attitude indicator) and intercardinal N/E/S/W marks
   (compass) - both gauges are deliberately simplified for now.
3. A "Following" toggle - right now the map always re-centers on the vehicle every
   update, with no way to pan away and have it stay put (ArduDeck's own toggle is the
   concrete reference for this).
4. Vehicle track/trail rendering - a polyline layer following the same position updates
   the marker already gets, not a new data source.
5. Waypoint/mission overlay - once `../FlightPlan/README.md`'s own mission-editing work
   exists, showing the active mission on this map too (ArduDeck's right-side Waypoints
   panel is the reference, though as a floating map overlay rather than a separate
   panel, matching this bundle's own overlay-not-side-panel approach).
6. The rest of the real screen's 14-tab "Control and Status" panel (Actions, PreFlight,
   Status, Servo/Relay, Aux Function, Scripts, Payload Control, Telemetry Logs,
   DataFlash Logs, Simple Actions, Transponder) - Gauges and Messages are the only two
   covered so far (as the floating overlay card and the shared bottom Terminal console,
   respectively).
