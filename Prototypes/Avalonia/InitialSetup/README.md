# Initial Setup bundle

One of the per-tab bundles mirroring Mission Planner's own top-level screens - see
`../DemoApp/README.md`'s "Bundles" section for the full list and how they're hosted.
Real screen this maps to: `GCSViews/InitialSetup.cs` / `.Designer.cs`.

## Status: vehicle identification only

Real (not mocked) firmware string, detected vehicle type, and `FRAME_CLASS`/`FRAME_TYPE`
param values, read once from a connected `MAVLinkInterface` at construction time - the
same fields `../ConfigMotorTest/`'s real frame-layout lookup already depends on.

## What's next

The real screen is a first-time setup wizard covering, in order: frame type selection,
accelerometer calibration, compass calibration, radio calibration, ESC calibration, and
flight mode assignment. None of that is implemented - each of those steps involves its
own MAVLink request/response flow (e.g. compass calibration streams
`MAG_CAL_PROGRESS`/`MAG_CAL_REPORT`) and would reasonably be its own sub-view within this
bundle, picked up one at a time by a contributor rather than all at once.
