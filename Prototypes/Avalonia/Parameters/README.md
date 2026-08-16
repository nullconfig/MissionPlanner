# Parameters bundle

One of the per-tab bundles mirroring Mission Planner's own top-level screens - see
`../DemoApp/README.md`'s "Bundles" section for the full list and how they're hosted.
Structurally, this one is the real **"Full Parameter List" page inside Config** (the
tab's real button label is "CONFIG", not "Config/Tuning" - see
`.okf/config-tuning/overview.md`'s "Corrections" for that and how much smaller Config's
real page list turned out to be than this bundle originally assumed), corresponding
directly to `ConfigRawParams.cs`. `ConfigFriendlyParams.cs` (a different real Config
page, "Standard Params") shares this bundle for now too - see below for why.

## Status: chunk 1 of N - read-only, real data, no editing yet

Deliberately staged rather than a single big port - see
`.okf/config-tuning/parameters/overview.md` for the full plan. This chunk: a live,
searchable table of every real parameter on the connected vehicle, with the exact same
column set as the real `ConfigRawParams.cs` screen (verified against its `.Designer.cs`/
`.resx` 2026-08-16, after the user described the real screen from actually looking at
it): `Name`, `Value`, `Default` (the vehicle's own MAVLink-reported default - a
different data source than the rest, see `ParametersViewModel.cs`), `Units`, `Options`
(`Range` + `Values` metadata combined), `Desc`. Same `MAV.param` iteration and
`ParameterMetaDataRepository` lookups the real screen makes. Search is a client-side
substring match against every column (matching the real screen's `filterList()` in
spirit, not its regex/wildcard/param-group-prefix machinery), same reasoning as the real
screen: a real vehicle reports anywhere from a few hundred to ~1500 parameters, so
scrolling the whole list isn't how anyone actually finds one.

**Also done, 2026-08-16**: the real left-side parameter-group tree
(`ConfigRawParams.cs`'s `treeView1`/`BuildTree()`, revealed by a user screenshot -
ported the exact algorithm, see `ParametersViewModel.BuildGroupTree`). Selecting a node
narrows the grid to that group, combined with the search box.

**Not yet ported**: in-place value editing (`setParamAsync` write-back), the `Fav`
favorites column/toggle, param-compare, save/load `.param` files, reboot-required
warnings, bitmask/enum-aware editors, the `Modified`/`None Default` filter checkboxes,
and the rest of the real action panel (`Write Params`, `Refresh Params`, `Load
Presaved`, `Reset to Default`). All real features of `ConfigRawParams.cs` - tracked as
later chunks, not forgotten.

## How it's wired

`ParametersView(MAVLinkInterface mav)` takes an already-connected `MAVLinkInterface` (same
constructor-injection pattern as `ConfigMotorTestView`/`FlightDataView` - no `MainV2.comPort`
singleton anywhere in this bundle). By construction time `MAV.param` is already fully
populated - `DemoApp`'s `ConnectAsync` awaits `mav.Open(getparams: true, ...)` before
constructing any view, and `Open` with `getparams: true` blocks until the full parameter
list has downloaded - so building the row list isn't waiting on data that hasn't arrived
yet. It's still built from `AttachedToVisualTree`, not the constructor, purely to match
`ConfigMotorTestView`'s established `Activate()`-on-attach convention (itself mirroring the
WinForms original's `IActivate.Activate()`, called when a tab is actually shown), not
because of a timing dependency here.

Building the row list is the expensive part - up to ~1500 params x 3
`ParameterMetaDataRepository` lookups each - so it's split into `BuildRows()` (pure
computation, run on a background thread via `Task.Run`) and `SetRows(...)` (fast, touches
the bound `Rows` collection, must run on the UI thread - the `await` continuation in
`ParametersView`'s `AttachedToVisualTree` handler resumes there on its own, no manual
`Dispatcher.UIThread.Post` needed). Doing this synchronously on attach was tried first and
visibly froze the UI for however long the lookups took - fixed before this chunk was called
done, not left as a known issue.

`ParametersViewModel` has no Avalonia dependency (matches every other bundle's ViewModel) -
plain `ReactiveObject` holding the full parameter list plus a filtered `ObservableCollection`
the view binds to. `ParameterRowViewModel` itself is a plain POCO, not reactive - nothing in
it changes after construction yet, since this chunk is read-only; it'll need to become
reactive (or gain a paired edit-state) once write-back lands.

The parameter-group tree (`BuildGroupTree`) is a verbatim port of
`ConfigRawParams.cs`'s `BuildTree()` algorithm, including a copy of its own
`NaturalStringComparer` (so `BARO2` sorts before `BARO10`, matching the real tree's node
order) - copied rather than referenced since both live in `GCSViews/ConfigurationView/`,
part of the WinForms `MissionPlanner.csproj` this bundle has zero reference to. It runs
in the same `Task.Run` as `BuildRows` (same CPU-bound-off-thread reasoning), and
`SelectedGroup`'s setter re-runs `ApplyFilter` exactly like `SearchText`'s does - a row
has to pass both the selected group's prefix and the search text, same order the real
screen applies them.

Table is an Avalonia `DataGrid` (`Avalonia.Controls.DataGrid` package, pinned to the exact
same `12.1.1` version as the rest of the app's Avalonia packages) - the first use of it
anywhere in this initiative. Its default theme doesn't match this app's dark palette, so
`ParametersView.axaml`'s own `UserControl.Styles` overrides `DataGrid`/
`DataGridColumnHeader`/`DataGridCell`/row-hover, copied-in dark tokens per the
self-contained-per-bundle pattern every other bundle here follows.

## Why ConfigRawParams and ConfigFriendlyParams share one bundle

The real app has two full-list parameter screens: `ConfigRawParams.cs` (a `DataGridView`-
based grid, in-place cell editing, custom sort, param-compare, save/load) and
`ConfigFriendlyParams.cs` (a `FlowLayoutPanel` of one custom control per parameter, grouped
by category). They overlap almost entirely in *data* (`MAV.param` +
`ParameterMetaDataRepository`) and differ mainly in *presentation* (grid vs. per-param
controls) - this bundle's `DataGrid` approach is closer in shape to `ConfigRawParams`, but
nothing here is grid-specific enough yet to justify two separate bundles. Revisit this if a
later chunk's presentation genuinely diverges (e.g. `ConfigFriendlyParams`'s per-category
grouped-cards view turns out to want its own bundle).

## What's next

See `.okf/config-tuning/parameters/overview.md`'s staged plan. The column-set fix and
the parameter-group tree above were the two previous "next" items - both done
2026-08-16. Next up: in-place editing (`DataGrid` non-read-only `Value` column,
`setParamAsync` on commit, `RebootRequired` warning surfaced somewhere visible, plus a
real `Write Params` action) - the next real vehicle-facing feature, not yet started.
