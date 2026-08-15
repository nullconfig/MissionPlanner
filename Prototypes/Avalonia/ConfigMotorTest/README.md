# Avalonia prototype: ConfigMotorTest

Real (non-mocked) MVVM port of `GCSViews/ConfigurationView/ConfigMotorTest.cs`
(WinForms) to Avalonia, scoped as the first slice of a possible GUI
modernization / native-Linux effort. Now a real, buildable `net8.0` class
library (`ConfigMotorTest.csproj`) - **not yet wired into `MissionPlanner.csproj`
or `MissionPlanner.sln`**, but no longer just a sketch: see `../DemoApp/` for a
runnable host that connects a real `MAVLinkInterface` (USB serial or UDP) and
hosts this view against it.

## Why this view

- Small (397 LOC + 186-line Designer file) and mostly self-contained: its only coupling
  to the rest of the app is the `MainV2.comPort` singleton, which is how nearly every
  screen in Mission Planner talks to the vehicle - so it's representative, not a special
  case.
- It exercises real functionality: live parameter reads (`MAV.param`), a live frame-type
  lookup, and real MAVLink commands (`doCommand(DO_MOTOR_TEST)`, `setParamAsync`) - not
  just static layout.
- Its dependency chain (`ExtLibs/ArduPilot/Mavlink/MAVLinkInterface.cs`) was checked and
  contains no Windows-only APIs (no `DllImport`, `System.Management`, registry access,
  COM interop) - a clean candidate for proving real Linux support, not just Linux-via-Mono.

## What changed vs. the original

| Original (WinForms) | Prototype (Avalonia) |
|---|---|
| Logic and UI mixed in one code-behind file | Split into `ConfigMotorTestViewModel.cs` (no UI framework reference) and `ConfigMotorTestView.axaml(.cs)` (view only) |
| Motor buttons built imperatively via `groupBox1.Controls.Add(new MyButton())` at absolute `Point(x, y)` coordinates | `Motors` is an `ObservableCollection<MotorButtonViewModel>` bound to an `ItemsControl`; layout is declarative and DPI/scale-independent |
| `CustomMessageBox.Show(...)`, `InputBox.Show(...)` (WinForms modal dialogs) | `IDialogService`, with a real Avalonia implementation - `AvaloniaDialogService.cs` + `PromptDialog`/`MessageDialog` windows |
| `Process.Start(url)` | Unchanged - `Process.Start` with `UseShellExecute = true` already works identically cross-platform on modern .NET |
| `MainV2.comPort.X` (static singleton) | `MAVLinkInterface` constructor-injected into `ConfigMotorTestViewModel` - see "The MainV2 decoupling" below |

## What did NOT change

Everything that talks to the vehicle: `MAV.param`, `MAV.aptype`,
`doCommand(...)`, `setParamAsync(...)`, `ParameterMetaDataRepository.GetParameterOptionsInt(...)`,
and the `APMotorLayout.json` frame-layout lookup are called on the injected
`MAVLinkInterface` **verbatim** from the original (just no longer reached
through `MainV2.comPort`). This is the concrete evidence for the "core is
separable" claim: the same `ExtLibs` code that powers the WinForms screen
powers this one, with zero modification to the vehicle-facing logic itself.

## The MainV2 decoupling

`ConfigMotorTestViewModel` used to reach the vehicle through `MainV2.comPort` -
a static field on `MainV2`, which lives in `MissionPlanner.csproj` (`net472`,
references `System.Windows.Forms`). A `net8.0` app can't load that assembly on
Linux at all, mock or not. The fix (previously flagged and deferred as
"Option 2"): `ExtLibs/ArduPilot/MissionPlanner.ArduPilot.csproj` (which contains
`MAVLinkInterface`) and its whole dependency chain already target
`netstandard2.0` - directly referenceable from a `net8.0` project with zero
WinForms in the load path (confirmed by building it standalone with the net8
SDK: 0 errors). So `ConfigMotorTestViewModel`'s constructor now takes a
`MAVLinkInterface mav` parameter instead of reading `MainV2.comPort`, and
`ConfigMotorTest.csproj` references `MissionPlanner.ArduPilot.csproj` directly.
See `../DemoApp/README.md` for how a real `MAVLinkInterface` gets constructed
and connected (USB serial or UDP) and passed in here.

## Fixed since the initial sketch

- The AXAML originally bound `Command="{Binding TestAllCommand}"` etc. directly to plain
  `async Task` methods on the ViewModel. Avalonia's `Command` binding requires an
  `ICommand`-typed property, so those bindings would have silently done nothing. Fixed by
  adding `RelayCommand.cs` (a minimal `ICommand`/async-command implementation) and exposing
  `ICommand` properties (`TestMotorCommand`, `TestAllCommand`, etc.) from the constructor
  instead.
- `ConfigMotorTestView.axaml.cs` was missing an `InitializeComponent()` method entirely -
  never caught because this project had no `.csproj` and was never actually compiled until
  now. Added (`AvaloniaXamlLoader.Load(this)`, matching the pattern elsewhere).
- Named elements (`x:Name="X"`) don't get code-behind fields auto-generated in this
  project (a generator-configuration quirk, not chased down further) - code-behind
  (`PromptDialog.axaml.cs`, `MessageDialog.axaml.cs`) uses `this.FindControl<T>("X")`
  instead, which is unaffected and stable across Avalonia versions.

## Visual polish

Restyled from a bare label/button Grid to a dark, card-based UI (numbered
motor badges, parameters/actions cards, styled primary/outline/danger/ghost
buttons, an `IsBusy`-driven "TESTING…" pill) to look like a real product
rather than a wireframe. This file keeps its own self-contained copy of the
`Card`/`Pill`/`NumberBadge`/button style classes (in `UserControl.Resources`/
`UserControl.Styles`) since, unlike `../DemoApp/`, it has no `App.axaml` of its
own once hosted elsewhere - `../DemoApp/App.axaml` hand-keeps the same palette
in sync at the Application level so its `PromptDialog`/`MessageDialog` windows
match too.

## What's still missing to make this real

1. ~~Add the Avalonia NuGet packages and a standalone `.csproj`~~ — done
   (`ConfigMotorTest.csproj`), and it's a real project reference from
   `../DemoApp/` now, not just a mocked-data harness.
2. ~~A real `IDialogService` implementation~~ — done (`AvaloniaDialogService.cs`).
3. ~~`MainV2.comPort` decoupling~~ — done, see above.
4. A host bridge so `MainV2`'s tab-switching logic can activate either the WinForms or the
   Avalonia version of a given screen side-by-side, for an incremental, screen-by-screen
   rollout rather than a big-bang rewrite. This is the main remaining gap before this
   could plug into the actual `MissionPlanner.sln`/shipping app.
5. Wire up `ThemeManager`-equivalent styling (Avalonia has its own theming/styles system;
   the WinForms `ThemeManager.ApplyThemeTo(this)` call has no direct analog) - relevant
   once/if this stops being a standalone dark-themed harness and needs to match whatever
   the rest of a mixed WinForms/Avalonia app looks like.
6. ~~Verify against real hardware~~ — done: connected over USB to a real
   ArduPilot flight controller mid-session, real sysid/frame/params, real
   motor-test order shown - see `../DemoApp/README.md`.
