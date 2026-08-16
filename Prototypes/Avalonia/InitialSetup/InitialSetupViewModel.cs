using ReactiveUI;

namespace MissionPlanner.Prototypes.Avalonia.InitialSetup
{
    // Real (non-mocked) vehicle identification from a connected MAVLinkInterface -
    // firmware string and detected frame class/type, read the same way
    // ConfigMotorTestViewModel does (see its GetMotorMax/SetFrameClassAndType). Everything
    // past identification (the actual calibration wizard steps) is not implemented - see
    // README.md for the real GCSViews/InitialSetup.cs feature list this would need to
    // grow into for closer 1:1 parity. ReactiveObject (ReactiveUI, added 2026-08-15) for
    // consistency with the other bundles' ViewModels, even though every property here is
    // get-only/set-once - see ConfigMotorTestViewModel for one that actually mutates.
    public class InitialSetupViewModel : ReactiveObject
    {
        public string FirmwareText { get; }
        public string VehicleTypeText { get; }
        public string FrameClassText { get; }
        public string FrameTypeText { get; }

        public InitialSetupViewModel(MAVLinkInterface mav)
        {
            FirmwareText = mav.MAV.cs.firmware.ToString();
            VehicleTypeText = mav.MAV.aptype.ToString();

            FrameClassText = mav.MAV.param.ContainsKey("FRAME_CLASS")
                ? ((int)mav.MAV.param["FRAME_CLASS"].Value).ToString()
                : "-";
            FrameTypeText = mav.MAV.param.ContainsKey("FRAME_TYPE")
                ? ((int)mav.MAV.param["FRAME_TYPE"].Value).ToString()
                : "-";
        }
    }
}
