using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace MissionPlanner.Prototypes.Avalonia.Parameters
{
    public partial class ParametersView : UserControl
    {
        private readonly ParametersViewModel _viewModel;

        // mav must already be connected - this view doesn't open/close it. MAV.param is
        // already fully populated by construction time (DemoApp's ConnectAsync awaits
        // mav.Open(getparams: true, ...) first), so building the row list on attach
        // rather than here is just matching ConfigMotorTestView's Activate()-on-attach
        // convention, not waiting on data that hasn't arrived yet - see README.md.
        public ParametersView(MAVLinkInterface mav)
        {
            InitializeComponent();

            _viewModel = new ParametersViewModel(mav);
            DataContext = _viewModel;

            // BuildRows/BuildGroupTree are the CPU-bound parts (up to ~1500
            // ParameterMetaDataRepository lookups, plus walking that same list to build
            // the group tree) - run on a background thread so it doesn't freeze the UI
            // while this tab first renders; SetRows touches the bound Rows/Groups
            // collections, so it has to run back on the UI thread, which is exactly
            // where this continuation resumes after await (Avalonia's UI
            // SynchronizationContext, no manual Dispatcher.UIThread.Post needed).
            // This is an async void-shaped event handler (event handlers can't be
            // async Task) - any exception after the first await here bypasses normal
            // Task exception propagation and becomes fatal to the whole process, not
            // just this view. Confirmed the hard way: a live-hardware-only race in
            // BuildRows() (MAVLinkParamList mutated by the packet thread while this
            // iterated it - see BuildRows' own comment) took down the entire app,
            // including unrelated tabs like FlightData, not just this one. The
            // try/catch here isn't defensive boilerplate - it's the fix for that
            // blast radius, independent of whatever the underlying bug turns out to
            // be next time.
            AttachedToVisualTree += async (_, __) =>
            {
                try
                {
                    var (rows, groupTree) = await Task.Run(() =>
                    {
                        var builtRows = _viewModel.BuildRows();
                        var builtTree = _viewModel.BuildGroupTree(builtRows.Select(r => r.Name).ToList());
                        return (builtRows, builtTree);
                    });
                    _viewModel.SetRows(rows, groupTree);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"ParametersView: failed to build parameter list: {ex}");
                    _viewModel.SetLoadError(ex.Message);
                }
            };
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    }
}
