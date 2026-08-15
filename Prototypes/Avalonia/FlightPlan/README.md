# Flight Plan bundle

One of the per-tab bundles mirroring Mission Planner's own top-level screens - see
`../DemoApp/README.md`'s "Bundles" section for the full list and how they're hosted.
Real screen this maps to: `GCSViews/FlightPlanner.cs`.

## Status: pure scaffold

No real feature implemented - just a project, a placeholder view, and this README. The
real screen is a full mission editor: a GMap.NET map for placing/reordering waypoints,
geofences, and rally points, plus upload/download of `MISSION_ITEM` sequences over
MAVLink to the connected vehicle.

## What's next

1. A native map control for Avalonia - the first real dependency to solve here, and one
   `../FlightData/README.md`'s own "what's next" (vehicle position/track) would also
   benefit from; worth solving once and sharing rather than duplicating.
2. Mission upload/download using `MAVLinkInterface`'s existing mission-protocol methods
   (already implemented in `ExtLibs/ArduPilot`, unmodified and reusable, same as every
   other bundle's approach to vehicle-facing logic - see
   `../ConfigMotorTest/README.md`'s "What did NOT change" section for the precedent).
3. Waypoint list editing UI, geofence upload, rally points - roughly in that order of
   real-world usefulness.
