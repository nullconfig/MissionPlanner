using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace MissionPlanner.Prototypes.Avalonia.ConfigMotorTest
{
    public partial class ConfigMotorTestView : UserControl
    {
        private readonly ConfigMotorTestViewModel _viewModel;

        // owner is the top-level Window this view is hosted in - needed to show modal
        // dialogs (AvaloniaDialogService) centered/owned correctly. mav is a real,
        // already-connected MAVLinkInterface supplied by the host (see ../DemoApp/) -
        // this view no longer mocks or self-connects.
        public ConfigMotorTestView(MAVLinkInterface mav, Window owner)
        {
            InitializeComponent();

            _viewModel = new ConfigMotorTestViewModel(mav, new AvaloniaDialogService(owner));
            DataContext = _viewModel;

            AttachedToVisualTree += (_, __) => _viewModel.Activate();
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        // Equivalent of IActivate in the WinForms original - MissionPlanner's tab host calls
        // Activate() when the tab is selected. Kept as a thin wrapper so this view can plug
        // into the same host contract without changing it.
        public void Activate() => _viewModel.Activate();
    }
}
