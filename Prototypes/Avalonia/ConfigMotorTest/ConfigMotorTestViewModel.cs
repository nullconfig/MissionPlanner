using System;
using System.Collections.ObjectModel;
using System.IO;
using ReactiveUI.Primitives;
using System.Reflection;
using System.Threading.Tasks;
using MissionPlanner.Utilities;
using Newtonsoft.Json;
using ReactiveUI;

namespace MissionPlanner.Prototypes.Avalonia.ConfigMotorTest
{
    // MVVM port of GCSViews/ConfigurationView/ConfigMotorTest.cs.
    //
    // Everything that talked to the vehicle in the original file (doCommand, setParamAsync,
    // MAV.param, ParameterMetaDataRepository) is called here completely unmodified - it
    // lives in ExtLibs/ArduPilot and ExtLibs/Mavlink, which have zero System.Windows.Forms
    // references, so it works as-is behind a new UI. Only the presentation - dynamic button
    // creation via groupBox1.Controls.Add(...), MessageBox popups, WinForms Point/Size
    // layout - needed to be rebuilt, because that was the part wired directly into
    // System.Windows.Forms.
    //
    // Talks to a MAVLinkInterface passed in by the host (constructor-injected) instead of
    // reaching through the WinForms-tied MainV2.comPort static singleton - MainV2 lives in
    // MissionPlanner.csproj (net472, references System.Windows.Forms), which a net8 app
    // cannot load on Linux. This ViewModel's own project (ConfigMotorTest.csproj) instead
    // references MissionPlanner.ArduPilot.csproj (netstandard2.0) directly, so this whole
    // file has zero WinForms in its dependency chain. See ../DemoApp/ for a host that
    // constructs and connects a real MAVLinkInterface (UDP or serial) and passes it in here.

    public class MotorButtonViewModel
    {
        public int Number { get; set; }
        public string Label { get; set; }
        public string DetailLabel { get; set; }
    }

    // Abstracts the one WinForms-specific thing this screen needs from its host (a modal
    // "enter a number" prompt and an error dialog) so the ViewModel has no Forms dependency
    // at all. The Avalonia view supplies a real implementation; a test host could supply a
    // fake one.
    public interface IDialogService
    {
        Task<int?> PromptForIntAsync(string title, string prompt, int defaultValue);
        Task ShowErrorAsync(string title, string message);
        void OpenUrl(string url);
    }

    public class ConfigMotorTestViewModel : ReactiveObject
    {
        private readonly MAVLinkInterface _mav;
        private readonly IDialogService _dialogs;

        public ObservableCollection<MotorButtonViewModel> Motors { get; } = new ObservableCollection<MotorButtonViewModel>();

        private int _throttlePercent = 5;
        public int ThrottlePercent
        {
            get => _throttlePercent;
            set => this.RaiseAndSetIfChanged(ref _throttlePercent, value);
        }

        private int _durationSeconds = 2;
        public int DurationSeconds
        {
            get => _durationSeconds;
            set => this.RaiseAndSetIfChanged(ref _durationSeconds, value);
        }

        private string _frameClassText = "";
        public string FrameClassText
        {
            get => _frameClassText;
            private set => this.RaiseAndSetIfChanged(ref _frameClassText, value);
        }

        private string _frameTypeText = "";
        public string FrameTypeText
        {
            get => _frameTypeText;
            private set => this.RaiseAndSetIfChanged(ref _frameTypeText, value);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set => this.RaiseAndSetIfChanged(ref _isBusy, value);
        }

        private int _motorMax;

        private struct MotorLayoutEntry
        {
            public int Number { get; set; }
            public int TestOrder { get; set; }
            public string Rotation { get; set; }
            public float Roll { get; set; }
            public float Pitch { get; set; }
        }

        private struct FrameLayout
        {
            public int Class { get; set; }
            public int Type { get; set; }
            public MotorLayoutEntry[] motors { get; set; }
        }

        private struct MotorLayoutFile
        {
            public string Version { get; set; }
            public FrameLayout[] layouts { get; set; }
        }

        private FrameLayout _frameLayout;

        // ReactiveCommand instead of the hand-rolled RelayCommand/AsyncRelayCommand
        // (removed 2026-08-15, see RelayCommand.cs's git history) - same ICommand
        // surface Avalonia's Command="{Binding ...}" needs, plus re-entrancy guarding
        // (a second click while one is still running is ignored) for free, which
        // AsyncRelayCommand had to hand-implement via its own _isExecuting flag.
        public ReactiveCommand<int, RxVoid> TestMotorCommand { get; }
        public ReactiveCommand<RxVoid, RxVoid> TestAllCommand { get; }
        public ReactiveCommand<RxVoid, RxVoid> TestAllSequenceCommand { get; }
        public ReactiveCommand<RxVoid, RxVoid> StopAllCommand { get; }
        public ReactiveCommand<RxVoid, RxVoid> SetSpinArmCommand { get; }
        public ReactiveCommand<RxVoid, RxVoid> SetSpinMinCommand { get; }
        public ReactiveCommand<RxVoid, RxVoid> OpenDocsCommand { get; }

        public ConfigMotorTestViewModel(MAVLinkInterface mav, IDialogService dialogs)
        {
            _mav = mav;
            _dialogs = dialogs;

            TestMotorCommand = ReactiveCommand.CreateFromTask<int>(TestMotorAsync);
            TestAllCommand = ReactiveCommand.CreateFromTask(TestAllAsync);
            TestAllSequenceCommand = ReactiveCommand.CreateFromTask(TestAllSequenceAsync);
            StopAllCommand = ReactiveCommand.CreateFromTask(StopAllAsync);
            SetSpinArmCommand = ReactiveCommand.CreateFromTask(SetSpinArmAsync);
            SetSpinMinCommand = ReactiveCommand.CreateFromTask(SetSpinMinAsync);
            OpenDocsCommand = ReactiveCommand.Create(OpenDocs);
        }

        // Equivalent of Activate() in the WinForms version - reads live vehicle state and
        // builds the motor button list. Same MAV.param calls as the original.
        public void Activate()
        {
            _motorMax = GetMotorMax();

            Motors.Clear();
            for (var a = 1; a <= _motorMax; a++)
            {
                var motor = new MotorButtonViewModel
                {
                    Number = a,
                    Label = "Test motor " + (char)((a - 1) + 'A'),
                };

                if (_frameLayout.motors != null)
                {
                    foreach (var m in _frameLayout.motors)
                    {
                        if (m.TestOrder == a)
                        {
                            motor.DetailLabel = "Motor Number: " + m.Number +
                                                 (m.Rotation != "?" ? ", " + m.Rotation : "");
                        }
                    }
                }

                Motors.Add(motor);
            }
        }

        private int GetMotorMax()
        {
            var motorMax = 8;

            if (_mav.MAV.aptype == MAVLink.MAV_TYPE.GROUND_ROVER ||
                _mav.MAV.aptype == MAVLink.MAV_TYPE.SURFACE_BOAT)
            {
                return 4;
            }

            var enable = _mav.MAV.param.ContainsKey("FRAME") ||
                         _mav.MAV.param.ContainsKey("Q_FRAME_TYPE") ||
                         _mav.MAV.param.ContainsKey("FRAME_TYPE");

            if (!enable)
            {
                return motorMax;
            }

            if (SetFrameClassAndType("FRAME_CLASS", "FRAME_TYPE") ||
                SetFrameClassAndType("Q_FRAME_CLASS", "Q_FRAME_TYPE"))
            {
                if (_frameLayout.motors != null)
                {
                    return _frameLayout.motors.Length;
                }
            }

            var type = MAVLink.MAV_TYPE.QUADROTOR;

            if (_mav.MAV.param.ContainsKey("Q_FRAME_CLASS"))
            {
                var value = (int)_mav.MAV.param["Q_FRAME_CLASS"].Value;
                switch (value)
                {
                    case 0:
                    case 1: type = MAVLink.MAV_TYPE.QUADROTOR; break;
                    case 2:
                    case 5: type = MAVLink.MAV_TYPE.HEXAROTOR; break;
                    case 3:
                    case 4: type = MAVLink.MAV_TYPE.OCTOROTOR; break;
                    case 6: type = MAVLink.MAV_TYPE.HELICOPTER; break;
                    case 7: type = MAVLink.MAV_TYPE.TRICOPTER; break;
                }
            }
            else if (_mav.MAV.param.ContainsKey("FRAME") ||
                     _mav.MAV.param.ContainsKey("FRAME_TYPE"))
            {
                type = _mav.MAV.aptype;
            }

            motorMax = type switch
            {
                MAVLink.MAV_TYPE.TRICOPTER => 4,
                MAVLink.MAV_TYPE.QUADROTOR => 4,
                MAVLink.MAV_TYPE.HEXAROTOR => 6,
                MAVLink.MAV_TYPE.OCTOROTOR => 8,
                MAVLink.MAV_TYPE.HELICOPTER => 0,
                MAVLink.MAV_TYPE.DODECAROTOR => 12,
                _ => motorMax,
            };

            return motorMax;
        }

        private bool SetFrameClassAndType(string classParamName, string typeParamName)
        {
            if (!_mav.MAV.param.ContainsKey(classParamName) ||
                !_mav.MAV.param.ContainsKey(typeParamName))
            {
                return false;
            }

            var frameClass = (int)_mav.MAV.param[classParamName].Value;
            foreach (var item in ParameterMetaDataRepository.GetParameterOptionsInt(
                         classParamName, _mav.MAV.cs.firmware.ToString()))
            {
                if (item.Key == frameClass)
                {
                    FrameClassText = "Class: " + item.Value;
                    break;
                }
            }

            var frameType = (int)_mav.MAV.param[typeParamName].Value;
            foreach (var item in ParameterMetaDataRepository.GetParameterOptionsInt(
                         typeParamName, _mav.MAV.cs.firmware.ToString()))
            {
                if (item.Key == frameType)
                {
                    FrameTypeText = "Type: " + item.Value;
                    break;
                }
            }

            LookupFrameLayout(frameClass, frameType);
            return true;
        }

        private void LookupFrameLayout(int frameClass, int frameType)
        {
            try
            {
                var file = Path.Combine(
                    Path.GetDirectoryName(Path.GetFullPath(Assembly.GetExecutingAssembly().Location)),
                    "APMotorLayout.json");

                using var reader = new StreamReader(file);
                var all = JsonConvert.DeserializeObject<MotorLayoutFile>(reader.ReadToEnd());
                if (all.Version == "AP_Motors library test ver 1.2")
                {
                    foreach (var layout in all.layouts)
                    {
                        if (layout.Class == frameClass && layout.Type == frameType)
                        {
                            _frameLayout = layout;
                            break;
                        }
                    }
                }
            }
            catch
            {
                // best-effort layout lookup, same as the WinForms original
            }
        }

        private async Task TestMotorAsync(int motor)
        {
            await TestMotor(motor, ThrottlePercent, DurationSeconds);
        }

        private async Task TestAllAsync()
        {
            for (var i = 1; i <= _motorMax; i++)
            {
                await TestMotor(i, ThrottlePercent, DurationSeconds);
            }
        }

        private async Task TestAllSequenceAsync()
        {
            await TestMotor(1, ThrottlePercent, DurationSeconds, _motorMax);
        }

        private async Task StopAllAsync()
        {
            for (var i = 1; i <= _motorMax; i++)
            {
                await TestMotor(i, 0, 0);
            }
        }

        private async Task TestMotor(int motor, int speed, int time, int motorCount = 0)
        {
            try
            {
                if (!_mav.doCommand((byte)_mav.sysidcurrent,
                        (byte)_mav.compidcurrent,
                        MAVLink.MAV_CMD.DO_MOTOR_TEST,
                        motor,
                        (float)(byte)MAVLink.MOTOR_TEST_THROTTLE_TYPE.MOTOR_TEST_THROTTLE_PERCENT,
                        speed,
                        time,
                        motorCount,
                        0, 0))
                {
                    await _dialogs.ShowErrorAsync("Error", "Command was denied by the autopilot");
                }
            }
            catch
            {
                await _dialogs.ShowErrorAsync("Error", $"Error communicating\nMotor: {motor}");
            }
        }

        private async Task SetSpinArmAsync()
        {
            if (!_mav.MAV.param.ContainsKey("MOT_SPIN_ARM"))
            {
                await _dialogs.ShowErrorAsync("Error", "param MOT_SPIN_ARM missing");
                return;
            }

            if (ThrottlePercent >= 20)
            {
                await _dialogs.ShowErrorAsync("Error", "Throttle percent above 20, too high");
                return;
            }

            IsBusy = true;
            try
            {
                var proposed = ThrottlePercent + 2;
                var result = await _dialogs.PromptForIntAsync(
                    "Set arm throttle", "Enter arm throttle % (deadzone + 2%)", proposed);
                if (result is int value)
                {
                    await _mav.setParamAsync((byte)_mav.sysidcurrent,
                        (byte)_mav.compidcurrent, "MOT_SPIN_ARM",
                        value / 100.0f);
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task SetSpinMinAsync()
        {
            if (!_mav.MAV.param.ContainsKey("MOT_SPIN_MIN"))
            {
                await _dialogs.ShowErrorAsync("Error", "param MOT_SPIN_MIN missing");
                return;
            }

            if (ThrottlePercent >= 20)
            {
                await _dialogs.ShowErrorAsync("Error", "Throttle percent above 20, too high");
                return;
            }

            IsBusy = true;
            try
            {
                var proposed = (int)_mav.MAV.param["MOT_SPIN_MIN"].Value + 3;
                var result = await _dialogs.PromptForIntAsync(
                    "Set min spin throttle", "Enter min spin throttle % (arm min + 3%)", proposed);
                if (result is int value)
                {
                    await _mav.setParamAsync((byte)_mav.sysidcurrent,
                        (byte)_mav.compidcurrent, "MOT_SPIN_MIN",
                        value / 100.0f);
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void OpenDocs()
        {
            try
            {
                _dialogs.OpenUrl("https://ardupilot.org/copter/docs/connect-escs-and-motors.html#motor-order-diagrams");
            }
            catch
            {
                // best effort, same as the WinForms original
            }
        }
    }
}
