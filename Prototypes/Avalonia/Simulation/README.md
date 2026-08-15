# Simulation bundle

One of the per-tab bundles mirroring Mission Planner's own top-level screens - see
`../DemoApp/README.md`'s "Bundles" section for the full list and how they're hosted.
Real screen this maps to: whatever `MainV2.cs`'s `MyView.ShowScreen("Simulation")`
resolves to at runtime (`MenuSimulation_Click`) - the implementing class wasn't located
during a quick search of `GCSViews/`, likely because it's registered by name rather than
directly referenced; not chased down further since the SITL-launching behavior itself
is well-documented ArduPilot tooling regardless of which file implements it.

## Status: real environment check, no launch yet

Checks whether `sim_vehicle.py` (ArduPilot's SITL launcher) is on `PATH` - a real,
verifiable check, not a hardcoded/faked status. In this sandbox it reliably reports "not
found", since no SITL tooling is installed here (see `../DemoApp/README.md`'s "What's
verified vs. not" history - this project's own live-hardware verification happened
against a real flight controller instead, precisely because SITL wasn't available).

## What's next

1. Actually shell out to `sim_vehicle.py` when found (`Process.Start`, similar to how
   `../ConfigMotorTest/`'s `OpenDocs`/`../Help/`'s link-opening already does for URLs).
2. Wire the resulting SITL UDP endpoint into the connect bar's UDP fields (see
   `../DemoApp/README.md`'s "Connecting: USB/serial vs UDP" section) so a successful
   launch flows straight into a normal connect, rather than requiring the user to
   re-enter `127.0.0.1:14550` by hand.
3. Surface SITL's own console output (stdout/stderr) somewhere - possibly reusable
   plumbing from `../Terminal/`, though that bundle's current design is MAVLink-specific
   (`STATUSTEXT`), not a generic process-output console.
