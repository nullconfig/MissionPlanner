# Terminal bundle

One of the per-tab bundles mirroring Mission Planner's own top-level screens - see
`../DemoApp/README.md`'s "Bundles" section for the full list and how they're hosted.

## Status: complete

A live console showing `STATUSTEXT` messages from a connected `MAVLinkInterface` - the
same feed Mission Planner's own "Messages" tab shows (boot banner, PreArm checks, GPS
status, etc.), with All/Info/Error severity filters and duplicate-message collapsing.
Extracted unmodified from `../DemoApp/MainWindow.axaml(.cs)`, where it originally lived
before the per-bundle split - see `../DemoApp/README.md`'s "Console" section for the full
history of bugs found building this (the missing packet pump, the missed startup banner,
the repeated-banner dedup).

## How it's wired

`TerminalView(MAVLinkInterface mav)` subscribes to `mav.OnPacketReceived` itself and
unsubscribes when detached from the visual tree. It does **not** run its own packet pump
- the host is responsible for one running against the same `MAVLinkInterface` (see
`../DemoApp/MainWindow.axaml.cs`'s `StartPacketPump`), otherwise nothing ever arrives here.

The header row (`>_ Terminal` label, All/Info/Error filters, message count) doubles as a
fold toggle - clicking the label expands/collapses the log area below it, so a host
mounting this as a persistent strip (see `../DemoApp/`, which hosts it at the bottom of
the window rather than behind a nav tab) doesn't have it permanently eating screen
space. **Starts collapsed** (`ConsoleContent`'s `IsVisible="False"` in the XAML, icon
defaults to `▶`) - a fresh connection shouldn't shove the console open in front of the
user before they've asked for it. The log area itself is height-capped (`MaxHeight` on
`ConsoleContent`) when expanded, for the same reason.

`TerminalViewModel` has no Avalonia dependency (matches `ConfigMotorTestViewModel`) - it's
plain data + filtering + rendering logic. `TerminalView`'s code-behind owns the
MAVLinkInterface subscription, decodes `STATUSTEXT`, and marshals onto the UI thread via
`Dispatcher.UIThread.Post` before calling into the ViewModel.

## What's next

Nothing required for parity with Mission Planner's own Messages tab at a basic level.
Possible future work: color-code lines by severity (would need to switch from a single
`TextBox` to an `ItemsControl` of per-line `TextBlock`s, since `TextBox` can't do
per-line rich text), or a "copy to clipboard" / "save log" action.
