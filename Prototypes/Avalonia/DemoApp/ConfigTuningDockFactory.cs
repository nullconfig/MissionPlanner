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
        // Concrete Document, not the IDocument interface - IDocument does NOT
        // include IDocumentContent (confirmed via metadata inspection: IDocument's
        // own interface list is IDockable/IControlRecyclingIdProvider/ILocalTarget,
        // no IDocumentContent), so Content is invisible through that interface even
        // though the concrete Document class implements IDocumentContent and really
        // does have it. This is what the CS1061 "IDocument does not contain a
        // definition for Content" error was actually telling us the first time -
        // not that Content doesn't exist, just that it wasn't reachable through the
        // interface these properties were typed as.
        public Document MotorTestDocument { get; private set; }
        public Document ParametersDocument { get; private set; }

        public override IRootDock CreateLayout()
        {
            // CanClose defaults to true on a Document - discovered the hard way: the
            // real screen has no notion of "close the Parameters pane forever",
            // these are fixed feature panes, not user documents. Without this,
            // clicking a Document's default close (x) button removes it with no way
            // back (CanCreateDocument=false below blocks creating a replacement, and
            // there's no other reopen mechanism).
            MotorTestDocument = new Document { Id = "MotorTest", Title = "Motor Test", CanClose = false };
            ParametersDocument = new Document { Id = "Parameters", Title = "Parameters", CanClose = false };

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
