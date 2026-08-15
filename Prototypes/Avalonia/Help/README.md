# Help bundle

One of the per-tab bundles mirroring Mission Planner's own top-level screens - see
`../DemoApp/README.md`'s "Bundles" section for the full list and how they're hosted.

## Status: complete (small by design)

Extracted from `../DemoApp/MainWindow.axaml`, where it originally lived inline, purely
for structural consistency with the other bundles - it's just two outbound links (the
ArduPilot docs, the Mission Planner GitHub repo), so there wasn't a functional reason to
split it out, only a "every tab is its own bundle" one. No `MAVLinkInterface` needed, and
none of the other bundles' patterns (ViewModel, polling, event subscriptions) apply here.

## What's next

The two outbound links here are placeholder content, not a port of the real screen.
Verified against `GCSViews/Help.cs`: the real Help screen is an embedded rich-text help
document
(`richTextBox1.Rtf = Resources.help_text`), a changelog link
(`https://firmware.ardupilot.org/Tools/MissionPlanner/upgrade/ChangeLog.txt`), a "show
console" checkbox, and stable/beta update-check buttons. An update-checker doesn't
obviously make sense for this project's own prototype (it isn't a released, updatable
app the way Mission Planner is), so the real scope to port is likely just the embedded
help text and changelog link - not a priority relative to the other bundles' functional
gaps, but easy pickings for a first contribution if someone wants one.
