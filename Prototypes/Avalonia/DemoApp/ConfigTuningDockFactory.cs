using Dock.Model.Avalonia;
using Dock.Model.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;

namespace MissionPlanner.Prototypes.Avalonia.DemoApp
{
    // Replaces the old FilterChip-toggle/IsVisible-swap sub-nav (Motor Test XOR
    // Parameters, one visible at a time) with a real dockable layout - both panes
    // exist simultaneously now, draggable/resizable/floatable, matching what
    // researching ArduDeck (an Electron/React GCS) turned up: its distinctive
    // dockable-panel UX comes from "dockview" (a React docking library), and
    // Dock.Avalonia (wieslawsoltes/Dock) is the direct Avalonia equivalent, not
    // something that requires an Electron rewrite to get.
    //
    // MotorTestDocument/ParametersDocument are exposed so MainWindow.axaml.cs can set
    // their real Content once connected (new ConfigMotorTestView(...)/new
    // ParametersView(...), unchanged from before - just assigned to a Document's
    // Content instead of a ContentControl's), the same "content set post-connect,
    // cleared on disconnect" pattern every other bundle here already uses.
    public class ConfigTuningDockFactory : Factory
    {
        public IDocument MotorTestDocument { get; private set; }
        public IDocument ParametersDocument { get; private set; }

        public override IRootDock CreateLayout()
        {
            MotorTestDocument = new Document { Id = "MotorTest", Title = "Motor Test" };
            ParametersDocument = new Document { Id = "Parameters", Title = "Parameters" };

            var documentDock = new DocumentDock
            {
                IsCollapsable = false,
                CanCreateDocument = false,
                VisibleDockables = CreateList<IDockable>(MotorTestDocument, ParametersDocument),
                ActiveDockable = MotorTestDocument,
            };

            var root = CreateRootDock();
            root.VisibleDockables = CreateList<IDockable>(documentDock);
            root.ActiveDockable = documentDock;
            root.DefaultDockable = documentDock;
            return root;
        }
    }
}
