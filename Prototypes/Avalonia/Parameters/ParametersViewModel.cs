using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using MissionPlanner.Utilities;
using ReactiveUI;

namespace MissionPlanner.Prototypes.Avalonia.Parameters
{
    // One row per real vehicle parameter - column set and sourcing matches the real
    // ConfigRawParams.cs ("Full Parameter List") exactly, verified against
    // ConfigRawParams.Designer.cs/.resx, not guessed: Name/Value/Units/Description come
    // from ParameterMetaDataRepository (same lookups ConfigFriendlyParams.cs and
    // ConfigMotorTestViewModel already make); Default comes from a different source
    // entirely - MAVLinkParam's own MAVLink-reported default, not metadata; Options
    // combines *two* metadata lookups (Range + Values) into one field, matching
    // ConfigRawParams.cs's own `range + "\n" + options` concatenation. See
    // .okf/config-tuning/parameters/overview.md's "Next immediate step" section (now
    // done) for the real header-name/Designer-field/source table this was built from.
    // Plain fields, not a ReactiveObject - this chunk is read-only (see
    // ParametersViewModel's own comment), so there's nothing here that changes after
    // construction yet.
    public class ParameterRowViewModel
    {
        public string Name { get; init; }
        public string ValueText { get; init; }
        public string DefaultText { get; init; }
        public string Units { get; init; }
        public string Options { get; init; }
        public string Description { get; init; }
    }

    // One node in the left-side parameter-group tree (e.g. "ACRO", or a nested
    // "ACRO_BAL" if enough params share that longer prefix - see BuildGroupTree's own
    // comment for the exact algorithm, ported from ConfigRawParams.cs's BuildTree()).
    // Name holds the *full* accumulated prefix at this node, not just its own segment -
    // that's what the real WinForms TreeNode.Text does too (BuildTree adds
    // currentPrefix.Substring(0, ...), the whole prefix so far, as each node's Text),
    // and it's what ApplyFilter needs directly without re-walking the tree to
    // reconstruct a path.
    public class ParameterGroupNode
    {
        public string Name { get; init; }
        public List<ParameterGroupNode> Children { get; } = new();
    }

    // Chunk 1 of the "full parameter list" screen (GCSViews/ConfigurationView/
    // ConfigRawParams.cs / ConfigFriendlyParams.cs) - see
    // .okf/config-tuning/parameters/overview.md for the staged plan this is one piece
    // of. Deliberately read-only for now: lists every real parameter on the connected
    // vehicle with search/filter and the real parameter-group tree, but no in-place
    // editing/setParamAsync write-back yet - that's the next chunk, once this one's
    // reviewed. No Avalonia dependency here (matches every other bundle's ViewModel) -
    // constructor-injected MAVLinkInterface, same pattern as
    // ConfigMotorTestViewModel/FlightDataViewModel.
    public class ParametersViewModel : ReactiveObject
    {
        private readonly MAVLinkInterface _mav;
        private readonly List<ParameterRowViewModel> _allRows = new();
        private static readonly NaturalStringComparer NaturalSorter = new();

        public ObservableCollection<ParameterRowViewModel> Rows { get; } = new();

        // Single root, always "All" - mirrors ConfigRawParams.cs's own
        // treeView1.Nodes.Add("All") as the tree's sole top-level node, with every real
        // group nested under it. A ParametersView.axaml TreeView binds directly to this
        // (one item), not to the "All" node's own Children - keeps "All" visible and
        // selectable as a real tree row, same as the real screen, instead of hidden
        // scaffolding.
        public ObservableCollection<ParameterGroupNode> Groups { get; } = new();

        private ParameterGroupNode _selectedGroup;
        public ParameterGroupNode SelectedGroup
        {
            get => _selectedGroup;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedGroup, value);
                ApplyFilter();
            }
        }

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set
            {
                this.RaiseAndSetIfChanged(ref _searchText, value);
                ApplyFilter();
            }
        }

        private string _statusText = "Not connected.";
        public string StatusText
        {
            get => _statusText;
            private set => this.RaiseAndSetIfChanged(ref _statusText, value);
        }

        public ParametersViewModel(MAVLinkInterface mav)
        {
            _mav = mav;
        }

        // Equivalent of ConfigFriendlyParams.FilterParamList() building its param list
        // off MAV.param, split into two calls rather than one synchronous Activate():
        // building the row list can mean up to ~1500 params x 3
        // ParameterMetaDataRepository lookups each (~4500 calls, XML/pdef-parsing on a
        // cold cache) - run inline on the UI thread, that visibly freezes the app for
        // however long that takes, right when Config/Tuning's Parameters tab first
        // attaches after connect. BuildRows does only that CPU-bound work and touches
        // nothing UI-bound (safe to call from any thread); SetRows does the fast part
        // that touches the bound Rows collection, and must run on the UI thread. See
        // ParametersView's AttachedToVisualTree for how the two get stitched together
        // (Task.Run + await, not a manual Dispatcher.UIThread.Post - Avalonia's UI
        // SynchronizationContext resumes the continuation there on its own).
        public List<ParameterRowViewModel> BuildRows()
        {
            var firmware = _mav.MAV.cs.firmware.ToString();
            var rows = new List<ParameterRowViewModel>();

            foreach (var p in _mav.MAV.param)
            {
                // Real MAVLink-reported default, not a metadata-repository lookup -
                // ConfigRawParams.cs reads this straight off the param itself
                // (MAVLinkParam.default_value), falling back to "NaN" when the vehicle
                // didn't report one, same as here.
                var defaultText = p.default_value.HasValue ? p.default_value_to_string() : "NaN";

                var range = ParameterMetaDataRepository.GetParameterMetaData(
                    p.Name, ParameterMetaDataConstants.Range, firmware);
                var values = ParameterMetaDataRepository.GetParameterMetaData(
                    p.Name, ParameterMetaDataConstants.Values, firmware);
                var units = ParameterMetaDataRepository.GetParameterMetaData(
                    p.Name, ParameterMetaDataConstants.Units, firmware);
                var description = ParameterMetaDataRepository.GetParameterMetaData(
                    p.Name, ParameterMetaDataConstants.Description, firmware);

                rows.Add(new ParameterRowViewModel
                {
                    Name = p.Name,
                    ValueText = p.ToString(),
                    DefaultText = defaultText,
                    Units = units,
                    Options = (range + "\n" + values.Replace(",", "\n")).Trim(),
                    Description = description,
                });
            }

            rows.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            return rows;
        }

        // Ported from ConfigRawParams.cs's BuildTree(), algorithm unchanged - only the
        // WinForms TreeNode calls became a plain ParameterGroupNode tree. Call after
        // BuildRows (needs the same distinct, real param names) and, like BuildRows,
        // it's pure computation with no UI-bound state touched, so safe to run
        // off-thread alongside it.
        //
        // Walks the *naturally sorted* param names once, and for each adjacent pair,
        // keeps extending the current group prefix by one "_"-delimited segment for as
        // long as *both* this param and the next one still share that longer prefix -
        // that's what turns "ACRO_BAL_PITCH"/"ACRO_BAL_ROLL" into a nested "ACRO" ->
        // "ACRO_BAL" -> {PITCH, ROLL} branch instead of two unrelated leaves, without
        // any hardcoded knowledge of what the group names actually are.
        public ParameterGroupNode BuildGroupTree(List<string> paramNames)
        {
            var root = new ParameterGroupNode { Name = "All" };

            var commands = paramNames.Distinct().ToList();
            commands.Sort(NaturalSorter);

            var currentNode = root;
            var currentPrefix = "";

            for (var i = 0; i < commands.Count; i++)
            {
                var param = commands[i];

                // While param does not start with currentPrefix, step up a layer in the tree.
                while (!param.StartsWith(currentPrefix))
                {
                    currentPrefix = currentPrefix.RemoveFromEnd(currentNode.Name.Split('_').Last() + "_");
                    currentNode = FindParent(root, currentNode);
                }

                if (i == commands.Count - 1)
                {
                    currentNode.Children.Add(new ParameterGroupNode { Name = param });
                    break;
                }

                var nextParam = commands[i + 1];
                var nodeToAdd = param.Substring(currentPrefix.Length).Split('_')[0] + "_";
                while (nodeToAdd.Length > 1
                       && param.StartsWith(currentPrefix + nodeToAdd)
                       && nextParam.StartsWith(currentPrefix + nodeToAdd))
                {
                    currentPrefix += nodeToAdd;
                    var branch = new ParameterGroupNode { Name = currentPrefix.Substring(0, currentPrefix.Length - 1) };
                    currentNode.Children.Add(branch);
                    currentNode = branch;
                    nodeToAdd = param.Substring(currentPrefix.Length).Split('_')[0] + "_";
                }

                currentNode.Children.Add(new ParameterGroupNode { Name = param });
            }

            return root;
        }

        // ParameterGroupNode has no Parent back-reference (unlike WinForms' TreeNode,
        // which BuildTree relies on via currentNode.Parent) - a plain POCO tree doesn't
        // need one for anything else, so this walks down from root to find whichever
        // node's Children contains `child`, rather than adding a back-reference solely
        // for this one algorithm.
        private static ParameterGroupNode FindParent(ParameterGroupNode root, ParameterGroupNode child)
        {
            if (root.Children.Contains(child))
                return root;

            foreach (var c in root.Children)
            {
                var found = FindParent(c, child);
                if (found != null)
                    return found;
            }

            return null;
        }

        // Must be called on the UI thread - see BuildRows/BuildGroupTree above.
        public void SetRows(List<ParameterRowViewModel> rows, ParameterGroupNode groupTree)
        {
            _allRows.Clear();
            _allRows.AddRange(rows);

            Groups.Clear();
            Groups.Add(groupTree);
            SelectedGroup = groupTree; // "All" selected by default, same as no filter

            ApplyFilter();
        }

        // Client-side substring match against every column, not just Name - matches
        // the real ConfigRawParams.cs's filterList(), which checks every DataGridView
        // cell in a row, in spirit (a simple Contains here, not its regex/wildcard
        // machinery). Combined with the selected group's prefix, same order as the
        // real screen: a row must pass the prefix check *and* the text search, not
        // either alone.
        private void ApplyFilter()
        {
            Rows.Clear();

            // "" for the root "All" node (or nothing selected yet) - same as
            // ConfigRawParams.cs's treeView1_AfterSelect setting filterPrefix = ""
            // when the selected node's text is "All".
            var groupPrefix = (_selectedGroup == null || _selectedGroup.Name == "All")
                ? ""
                : _selectedGroup.Name + "_";

            var query = _searchText?.Trim();

            var matches = _allRows.Where(r =>
                PassesGroupFilter(r.Name, groupPrefix) && PassesSearchFilter(r, query));

            foreach (var row in matches)
                Rows.Add(row);

            StatusText = _allRows.Count == 0
                ? "Not connected."
                : $"{Rows.Count} of {_allRows.Count} parameters";
        }

        // Matches ConfigRawParams.cs's filterList(): a row passes if its name is
        // *exactly* the selected prefix (minus the trailing "_") - the case where the
        // selected tree node is itself a real param, not just a group label - or
        // starts with it.
        private static bool PassesGroupFilter(string name, string groupPrefix)
        {
            if (groupPrefix.Length == 0)
                return true;

            return name == groupPrefix.TrimEnd('_') || name.StartsWith(groupPrefix);
        }

        private static bool PassesSearchFilter(ParameterRowViewModel r, string query)
        {
            if (string.IsNullOrEmpty(query))
                return true;

            return r.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                   r.ValueText.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                   r.DefaultText.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                   (r.Units?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (r.Options?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (r.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        // Verbatim port of ConfigRawParams.cs's own NaturalStringComparer (same file,
        // a nested class there) - copied rather than referenced because it lives in
        // GCSViews/ConfigurationView/, part of the net472/WinForms MissionPlanner.csproj
        // this bundle deliberately has zero reference to (see every other bundle's own
        // "no MainV2.comPort" comments for why). Small and dependency-free enough that
        // copying verbatim was simpler than restructuring where it lives upstream.
        // Keeps BuildGroupTree's node ordering byte-identical to the real screen's
        // (numeric segments compare by value, so BARO2 sorts before BARO10).
        private sealed class NaturalStringComparer : IComparer<string>
        {
            public int Compare(string x, string y) => NaturalCompare(x, y);

            public int NaturalCompare(string x, string y)
            {
                var indexX = 0;
                var indexY = 0;
                while (true)
                {
                    if (indexX == x.Length)
                        return indexY == y.Length ? 0 : -1;
                    if (indexY == y.Length)
                        return 1;

                    var charX = x[indexX];
                    var charY = y[indexY];
                    if (char.IsDigit(charX) && char.IsDigit(charY))
                    {
                        while (indexX < x.Length && x[indexX] == '0') indexX++;
                        while (indexY < y.Length && y[indexY] == '0') indexY++;

                        var endNumberX = indexX;
                        var endNumberY = indexY;
                        while (endNumberX < x.Length && char.IsDigit(x[endNumberX])) endNumberX++;
                        while (endNumberY < y.Length && char.IsDigit(y[endNumberY])) endNumberY++;

                        var digitsLengthX = endNumberX - indexX;
                        var digitsLengthY = endNumberY - indexY;
                        if (digitsLengthX != digitsLengthY)
                            return digitsLengthX - digitsLengthY;

                        while (indexX < endNumberX)
                        {
                            if (x[indexX] != y[indexY])
                                return x[indexX] - y[indexY];
                            indexX++;
                            indexY++;
                        }
                    }
                    else
                    {
                        var compareResult = char.ToUpperInvariant(charX).CompareTo(char.ToUpperInvariant(charY));
                        if (compareResult != 0)
                            return compareResult;
                        indexX++;
                        indexY++;
                    }
                }
            }
        }
    }
}
