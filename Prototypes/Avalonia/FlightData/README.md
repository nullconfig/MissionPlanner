# Flight Data bundle

One of the per-tab bundles mirroring Mission Planner's own top-level screens - see
`../DemoApp/README.md`'s "Bundles" section for the full list and how they're hosted.
Real screen this maps to: `GCSViews/FlightData.cs` (6,758 lines) +
`GCSViews/FlightData.Designer.cs` (3,215 lines) - by far the largest single screen in the
real app.

## Status: real telemetry, no HUD widget or map yet

Live (not mocked) numeric readout - attitude, heading, altitude, ground/air speed,
battery voltage/remaining, current, GPS fix + satellite count, lat/lng, mode, armed state
- all read directly off a connected `MAVLinkInterface`'s `CurrentState` (`mav.MAV.cs`),
polled every 200ms. These are the exact same properties Mission Planner's own HUD control
data-binds to - see `GCSViews/FlightData.Designer.cs`, search `bindingSourceHud`, e.g.
`this.hud1.DataBindings.Add(new System.Windows.Forms.Binding("alt", this.bindingSourceHud,
"alt", true))`.

What's **not** here: the HUD's own artificial-horizon rendering (a custom-drawn GDI+
control - `hud1` in the Designer file) and the map (GMap.NET-based, shows vehicle
position/track). Both are substantial, semi-independent subsystems - see "What's next".

## How it's wired

`FlightDataViewModel(MAVLinkInterface mav)` has no Avalonia dependency (matches
`ConfigMotorTestViewModel`) - it polls on a plain `System.Threading.Timer` (`CurrentState`
has no "value changed" event to hook, same reason the real app's HUD refresh is
timer-driven too) and raises `PropertyChanged` from a threadpool thread. `FlightDataView`'s
code-behind marshals updates onto the UI thread via `Dispatcher.UIThread.Post` rather than
relying on Avalonia's data-binding to do it, consistent with how the other bundles handle
cross-thread updates (see `../Terminal/README.md`).

## What's next

1. An artificial-horizon widget (roll/pitch visualization) - achievable in Avalonia via a
   `Canvas` + rotated/translated shapes (horizon line, pitch ladder, roll indicator);
   doesn't need to port the real `hud1` control's GDI+ code, just reproduce its visual
   behavior against the same `CurrentState` data this bundle already reads.
2. A map showing vehicle position/track - the real screen's biggest remaining gap and
   the same missing dependency `../FlightPlan/README.md` flags; whichever bundle solves
   it first should probably expose it as something the other can reuse.
3. Quick-action buttons (the real screen's tab strip below the HUD: Actions, Status,
   Servo/Relay, etc. - `tabControlactions` in the Designer file).
