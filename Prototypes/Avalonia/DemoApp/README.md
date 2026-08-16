# DemoApp — app shell + real (non-mocked) native-Linux harness

Standalone Avalonia app that connects a real `MAVLinkInterface` (USB serial or UDP)
and hosts one View per bundle - one per tab in the top banner, mirroring Mission
Planner's own top-level screens. No mock data, no `MainV2` - see "The MainV2
decoupling" below for how that became possible. Deliberately has **no reference** to
`MissionPlanner.csproj`: that project targets `net472` and pulls in
`System.Windows.Forms`, which a `net8.0` app cannot load on Linux. Each bundle instead
references `ExtLibs/ArduPilot/MissionPlanner.ArduPilot.csproj` (`netstandard2.0`, zero
WinForms) directly where it needs live vehicle data.

## Bundles

Each tab in the banner is its own project (own `.csproj`, own `README.md`), so a
contributor can pick one up without needing to understand the whole app shell - this
mirrors Mission Planner's own top-level screens (`MainV2.Designer.cs`:
`MenuFlightData`..`MenuHelp`) one bundle at a time, deep on the ones that are real
rather than shallow across all of them.

| Tab | Bundle | Real screen | Status |
|---|---|---|---|
| Flight Data | [`../FlightData/`](../FlightData/README.md) | `GCSViews/FlightData.cs` | Live telemetry readout; no HUD widget or map yet |
| Flight Plan | [`../FlightPlan/`](../FlightPlan/README.md) | `GCSViews/FlightPlanner.cs` | Pure scaffold - needs a map dependency first |
| Initial Setup | [`../InitialSetup/`](../InitialSetup/README.md) | `GCSViews/InitialSetup.cs` | Vehicle identification only; no calibration wizard |
| Config/Tuning | [`../ConfigMotorTest/`](../ConfigMotorTest/README.md) | `GCSViews/ConfigurationView/ConfigMotorTest.cs` | **Complete** - real motor-test commands, verified against hardware |
| Simulation | [`../Simulation/`](../Simulation/README.md) | `GCSViews/SITL.cs` | Real SITL-on-PATH check; doesn't launch yet |
| Help | [`../Help/`](../Help/README.md) | `GCSViews/Help.cs` | Placeholder links only - see its README for what the real screen actually does |

**Not a tab**: [Terminal](../Terminal/README.md) - verified against `MainV2.Designer.cs`'s
actual toolbar that it isn't top-level in the real app either; its real class
(`ConfigTerminal.cs`) is a `Config/Tuning` sub-screen, and a command-*input* one, not a
read-only viewer. This project's `Terminal` bundle is a read-only `STATUSTEXT` viewer,
so it's hosted as a **persistent,
always-visible console strip at the bottom of the window** instead - useful for
troubleshooting regardless of which tab is active, which is arguably more useful than
one more tab to click into. **Complete** - live filtered console, verified against
hardware.

Bundles needing live vehicle data (Flight Data, Initial Setup, Config/Tuning, Terminal)
are only constructed after a successful connect (see `ConnectAsync` in
`MainWindow.axaml.cs`) and torn down on disconnect; Flight Plan, Simulation, and Help
don't need one and are constructed once at startup.

## Status: builds and runs

Needs the **.NET 10 SDK** (bumped from net8.0 2026-08-15, alongside adopting ReactiveUI
and FluentAvalonia across every bundle - see `.okf/log.md`'s "MVVM + FluentAvalonia"
entry for the full reasoning, including why FluentAvaloniaUI specifically forced the
net10.0 move). The .NET 8 SDK still installed alongside it is unrelated/unused by this
tree now.

```
export PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet"
cd Prototypes/Avalonia/DemoApp
dotnet build
dotnet run
```

Pick USB/Serial or UDP in the connect bar, point it at a real flight controller
or an ArduPilot SITL instance, and click Connect. Until then a placeholder card
is shown instead of the motor test screen.

## The MainV2 decoupling

The original version of this harness (see git history) used mock data because
`MainV2.comPort` - the original access point to `MAVLinkInterface` - is a static
field on `MainV2`, which lives in `MissionPlanner.csproj` (`net472`,
`System.Windows.Forms`), unloadable on Linux. Investigating the fix (the
"Option 2" deferred in that earlier version) found it was more tractable than
expected: `ExtLibs/ArduPilot/MissionPlanner.ArduPilot.csproj` (which contains
`MAVLinkInterface`) and its whole dependency chain (`Comms`, `Mavlink`,
`GMap.NET.Core`, `Strings`, `Utilities`) already target `netstandard2.0` -
directly consumable from a `net8.0` project, confirmed by building
`MissionPlanner.ArduPilot.csproj` standalone with the net8 SDK (0 errors).

So the fix was: constructor-inject a `MAVLinkInterface` into
`ConfigMotorTestViewModel` instead of reaching through `MainV2.comPort` (see
`../ConfigMotorTest/ConfigMotorTestViewModel.cs`), give `ConfigMotorTest.csproj`
a real project reference to `MissionPlanner.ArduPilot.csproj`, and have this
harness construct and connect a real `MAVLinkInterface` itself. The mock
`DemoConfigMotorTestViewModel.cs` this project used to carry is gone -
`MainWindow` now hosts the actual `ConfigMotorTestView` from `../ConfigMotorTest/`.

Also promoted while doing this: `../ConfigMotorTest/`'s `IDialogService` stub
(previously a `Task.FromResult(defaultValue)`/`Debug.WriteLine` sketch) is now a
real implementation (`AvaloniaDialogService.cs` + `PromptDialog`/`MessageDialog`
windows) - needed because the real ViewModel's `SetSpinArmAsync`/`SetSpinMinAsync`
actually call it now.

## Connecting: USB/serial vs UDP

- **USB/Serial** (default) - lists ports via `MissionPlanner.Comms.SerialPort.GetPortNames()`
  (the same cross-platform, `/dev/`-scanning code the real WinForms app uses). Baud is a
  dropdown, not free text: `{4800, 9600, 19200, 38400, 57600, 115200, 230400, 460800,
  921600}`, defaulting to 115200 - narrowed from the real app's full connect-toolbar list
  (`Controls/ConnectionOptions.resx`, `CMB_baudrate.Items`, 17 entries including rarely-used
  ones like 111100/625000/1000000) down to ArduPilot's commonly-used `SERIALx_BAUD` rates.
  In this sandboxed dev environment there's no real USB device, and the port list picks up
  the container's virtual TTYs (`/dev/tty0`, etc.) since nothing filters those out -
  harmless (they'll just fail to connect) but noisy; a real desktop won't have that
  clutter, but filtering to `ttyACM*`/`ttyUSB*` in this harness might be worth doing later.
- **UDP** - for ArduPilot SITL (`sim_vehicle.py --out=udpclient:127.0.0.1:14550`
  or similar). Uses `MissionPlanner.Comms.UdpSerialConnect.Open(host, port)`
  (connects out, not listen-on - matters if pointing this at a SITL instance
  configured to push to a listening port instead).

## Verifying "did it actually connect" - a real bug found here

`MAVLinkInterface.Open(getparams, skipconnectedcheck, showui)` with `showui:false`
runs headless (`NoUIReporter` instead of a WinForms progress dialog) - this is
what makes calling it from Avalonia/Linux possible at all without wiring up
`CreateIProgressReporterDialogue`. But it swallows exceptions internally
(`NoUIReporter.RunBackgroundOperationAsync`'s `catch` block is empty), so a
timed-out connect attempt does not throw - and `BaseStream.IsOpen` is **not** a
reliable signal either: verified via a standalone headless test (`MAVLinkInterface`
+ `UdpSerialConnect` against an unreachable `127.0.0.1:19191`, with
`CONNECT_TIMEOUT_SECONDS` set to `3` for speed) that `udp.IsOpen` stays `true`
even after the timeout, since UDP has no real "closed" state to observe. The
connect code in `MainWindow.axaml.cs` instead checks `mav.sysidcurrent != 0`,
which only gets set once a real heartbeat has actually been selected
(`MAVLinkInterface.SetupMavConnect`) - confirmed `0` in that same failed-connect
test.

## Verified against real hardware

Connected over USB to a real flight controller (ArduPilot-branded CDC-ACM
device, product string `CORVON743V1`, a CORVO N7 43 V1 board) running actual
ArduCopter firmware: `sysid 1, compid 1`, `aptype=QUADROTOR`,
`firmware=ArduCopter2`, 1224 parameters downloaded, and the Motor Test screen
showed the real per-vehicle motor test order/rotation (`Test motor A → Motor
Number 1, CCW`, `B → 4, CW`, `C → 2, CCW`, `D → 3, CW` - the genuine
`FRAME_CLASS`/`FRAME_TYPE`-driven `APMotorLayout.json` lookup, not placeholder
text). No exceptions in the log. This is full proof the decoupling works
end-to-end against real ArduPilot hardware, not just against mocks or a
timeout path.

## A second real bug found connecting to real hardware: DTR reset loop

The failure-path testing above (unreachable UDP) didn't catch this one -
it only showed up against a real board. Symptom: `dotnet run`'s connect
attempt against a real, known-good, already-booted flight controller
(confirmed alive by successfully binding its RC transmitter) still reported
"no heartbeat received", and a **raw OS-level read** of the port
(`head -c /dev/ttyACM0`, no .NET involved at all) also got zero bytes -
ruling out a bug in our C# code at first glance. Checked `dmesg`/
`journalctl -k`: the board was repeatedly cycling
`CORVON743V1-BL` (bootloader) → disconnect → `CORVON743V1` (app) on every
connection attempt - something was resetting it.

Root cause: opening a serial port from .NET on Linux asserts DTR/RTS as
part of the `open()` transition unless told not to, and this board (like
many flight controllers, Arduino-derived reset-on-DTR convention) resets
on that transition. `MainV2.cs` already sets `comPort.BaseStream.DtrEnable
= false` before connecting in the real WinForms app - this harness didn't,
because nothing surfaced the need for it until real hardware was available
to test against. Fixed by setting `serial.DtrEnable = false;
serial.RtsEnable = false;` **before** `MAVLinkInterface.Open()` is called
(see `MainWindow.axaml.cs`). A second, related finding: manually
pre-calling `serial.Open()` ourselves before handing the stream to
`MAVLinkInterface.Open()` (which also opens it internally via `OpenBg`)
reliably prevented heartbeat detection even with DTR/RTS already false, for
reasons not fully root-caused - letting `MAVLinkInterface.Open()` own the
`BaseStream.Open()` call entirely, exactly like `MainV2` does, fixed it.

## Disconnect

The Connect button becomes a Disconnect button (red) once connected, calling
`MAVLinkInterface.Close()` (which also closes `BaseStream`) and stopping the
packet pump (below). Connection settings (mode, port, baud, host) are locked
while connected and re-enabled after disconnect, so they can't be changed out
from under a live `MAVLinkInterface`.

## The background packet pump

`MainWindow.axaml.cs`'s `StartPacketPump`/`StopPacketPump` run a loop calling
`mav.readPacketAsync()` whenever `BaseStream.BytesToRead > 10` (mirroring
`MainV2.cs`'s own `serialThread` threshold), started on connect and stopped on
disconnect. This is the app shell's job, not any individual bundle's - `MAVLinkInterface`
has no background reader of its own (`MainV2.cs` runs a continuous thread for exactly
this reason), so without this pump, nothing arrives at any bundle that depends on live
data after the initial connect handshake (the Terminal bundle's console, the Flight Data
bundle's telemetry). Discovered building the console, when it originally lived inline
here rather than in its own bundle - see `../Terminal/README.md` for that and two more
related bugs found the same way (the missed startup banner, repeated-banner dedup).

## USB unplug detection

The same packet pump loop also detects a physical USB unplug and auto-resets to the
disconnected state - previously, unplugging the board mid-session left the UI stuck
"Connected" indefinitely. `SerialPort.IsOpen` doesn't help here: on Linux it stays
`true` until `Close()`/`Dispose()` is called explicitly, regardless of whether the
device is still physically present - confirmed this is exactly as unreliable in
`MainV2.cs` as it would have been here (see its `giveComport` handling: an empty
`catch` around `comPort.Close()` with the comment *"i get alot of these errors, the
port is still open, but not valid - user has unpluged usb"*, polled via that same
`IsOpen`). No exception type is thrown reliably on unplug either - this codebase never
targets a specific one (`IOException`, etc.), only generic catches everywhere, `MainV2.cs`
included.

The one conclusive signal: the device node disappearing from `SerialPort.GetPortNames()`
entirely. `StartPacketPump` captures the connected port name (`_connectedSerialPortName`,
null for a UDP connection - nothing to watch there) and checks it against
`GetPortNames()` roughly once a second (every 50 idle-loop iterations, not every 20ms -
`GetPortNames()` enumerates `/dev`, no need to hammer it). When the port's gone,
`HandleUnexpectedDisconnect` marshals onto the UI thread and runs the same teardown as a
manual Disconnect click, with a status message explaining why
(`"Disconnected: /dev/ttyACM0 is no longer present (USB unplugged?)"`).

**Verified against real hardware**: unplugged the CORVO N7 43 V1 mid-session with the
app connected over USB - detected and reset to disconnected immediately.

**Known residual risk, not yet hit in practice**: `readPacketAsync()` does a
synchronous, non-cancellation-aware blocking read internally
(`MAVLinkInterface.cs`'s `BaseStream.Read()` calls). If the device is unplugged while a
read is actually in flight (rather than idling between reads, which is what the real
test above happened to catch), that read isn't guaranteed to return on Linux CDC-ACM
drivers, and the pump loop would never reach the port-presence check for that
connection. Fixing this would mean touching `MAVLinkInterface`'s blocking I/O itself -
shared core library code `MainV2.cs` also depends on - so it's left as a known gap
rather than patched here. Separately, the port-presence check itself is wrapped in its
own try/catch (not just the read path above it): it runs inside a fire-and-forget
`Task.Run` with nothing observing its result, so an uncaught exception from
`GetPortNames()` (its own implementation notes historical Linux "too many open files"
flakiness) would otherwise silently kill the pump permanently - worse than not having
unplug detection at all.

## Visual polish

Dark, card-based UI. Top banner: logo badge, one nav tab per top-level screen (icon +
label, underlined when active - six of them, matching the real app's toolbar exactly),
and the connection controls (mode, port/host, baud, Connect) anchored flush to the far
right edge of the window. Layout is a `Grid` with `Auto,*,Auto` columns - badge, nav
tabs, connect controls - where only the nav tabs column has its own `ScrollViewer`
(clipping/scrolling within its column at narrow widths); the connect controls column is
plain `Auto`, so it's never squeezed or scrolled, always sized to its content and pinned
right. (An earlier version put everything - badge, tabs, and connect controls - inside
one shared `ScrollViewer` so they'd scroll together as a unit; that meant the connect
controls just trailed after the last tab instead of reaching the actual right edge, so
it was reverted in favor of the anchored layout above.) Below that: the active tab's
content, and below *that*, a persistent read-only console strip visible regardless of
which tab is active (see "The background packet pump" section above for why it's not a
nav tab). Colors and `Button`/`Border` style classes (`Card`, `Pill`,
`NumberBadge`, `Primary`, `Outline`, `Danger`, `Ghost`, `MotorTest`, `FilterChip`,
`NavTab`) live in `App.axaml`'s `Application.Resources`/`Application.Styles` so every
bundle hosted here picks them up; each bundle also keeps its own self-contained copy
(see e.g. `../ConfigMotorTest/ConfigMotorTestView.axaml`) since none of them have an
`App.axaml` of their own once reused/hosted elsewhere.

**Keep each bundle's copy byte-identical to `DemoApp/App.axaml`'s.** This is manual -
nothing enforces it at build time - and it has drifted before: `Border.Card` ended up
with three different variants across bundles (missing padding/shadow in some), plus
smaller drift in `Border.Pill`'s padding and a `TextBlock.StatValue` class that wasn't
even in the canonical file yet. All since fixed. If you're touching bundle styling and
want to check for this kind of drift, ask whoever's coordinating the project - there's
an internal audit script for it that isn't part of this public checkout.

## Environment notes for whoever resumes this

- .NET SDK 8.0.424 was installed user-locally to `~/.dotnet` via
  `dotnet-install.sh` (not a system package, no `sudo` used, removable with
  `rm -rf ~/.dotnet`). It is **not** on `PATH` by default in new shells — export
  `PATH="$HOME/.dotnet:$PATH"` and `DOTNET_ROOT="$HOME/.dotnet"` first.
- A real display is available in this environment (`DISPLAY=:0`,
  `WAYLAND_DISPLAY=wayland-0`).
- NuGet restore has network access confirmed (`api.nuget.org` reachable).
- Screenshot tool: `spectacle -b -n -f -o <file>` works; `ImageMagick import`
  does not (unrelated pre-existing issue in this sandbox).
