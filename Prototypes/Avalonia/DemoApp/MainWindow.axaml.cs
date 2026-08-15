using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using MissionPlanner;
using MissionPlanner.Comms;
using MissionPlanner.Prototypes.Avalonia.ConfigMotorTest;
using MissionPlanner.Prototypes.Avalonia.FlightData;
using MissionPlanner.Prototypes.Avalonia.FlightPlan;
using MissionPlanner.Prototypes.Avalonia.Help;
using MissionPlanner.Prototypes.Avalonia.InitialSetup;
using MissionPlanner.Prototypes.Avalonia.Simulation;
using MissionPlanner.Prototypes.Avalonia.Terminal;

namespace MissionPlanner.Prototypes.Avalonia.DemoApp
{
    // App shell: owns the connection (banner) and hosts one View per bundle, one per nav
    // tab - see README.md's "Bundles" section for the full list, what's real in each, and
    // what a contributor would build out next. Bundles needing live vehicle data
    // (FlightData, InitialSetup, ConfigTuning/Motor Test) are only constructed after a
    // successful connect; FlightPlan/Simulation/Help don't need one and are constructed
    // once at startup. Terminal isn't a nav tab (verified against MainV2.Designer.cs
    // that it isn't one in the real app either); its read-only console is hosted as a
    // persistent bottom strip instead, visible on every tab, not just its own.
    public partial class MainWindow : Window
    {
        private readonly ComboBox _modeCombo;
        private readonly StackPanel _serialFields;
        private readonly StackPanel _udpFields;
        private readonly ComboBox _serialPortCombo;
        private readonly Button _refreshPortsButton;
        private readonly ComboBox _baudCombo;

        // Narrowed from the real app's full connect toolbar list
        // (Controls/ConnectionOptions.resx, CMB_baudrate.Items, 17 entries including rare
        // ones like 111100/625000/1000000) down to ArduPilot's commonly-used SERIALx_BAUD
        // rates - the ones an actual flight controller setup would realistically use.
        private static readonly int[] StandardBaudRates =
            { 4800, 9600, 19200, 38400, 57600, 115200, 230400, 460800, 921600 };
        private readonly TextBox _hostBox;
        private readonly TextBox _portBox;
        private readonly Button _connectButton;
        private readonly TextBlock _connectStatusText;

        private readonly Grid _flightDataPanel;
        private readonly ContentControl _flightDataHost;
        private readonly Grid _initialSetupPanel;
        private readonly ContentControl _initialSetupHost;
        private readonly Border _placeholderCard;
        private readonly ContentControl _motorTestHost;

        // Persistent, foldable, read-only troubleshooting console - not a nav tab, see
        // the constructor comment above. The fold toggle lives inside TerminalView's
        // own header (see Prototypes/Avalonia/Terminal/), not here.
        private readonly Border _bottomConsolePanel;
        private readonly ContentControl _bottomConsoleHost;

        // Nav tabs mirror the real app's own toolbar exactly (MainV2.Designer.cs:
        // MenuFlightData, MenuFlightPlanner, MenuInitConfig, MenuConfigTune,
        // MenuSimulation, MenuHelp - confirmed against its actual MainMenu.Items list).
        private Dictionary<string, Button> _navButtons;
        private Dictionary<string, Control> _sectionPanels;
        private string _activeSection = "ConfigTuning";

        private MAVLinkInterface _mav;
        private bool _connecting;
        private CancellationTokenSource _pumpCts;

        // Set only for a serial connection (null for UDP) - the packet pump polls this
        // port's continued presence in SerialPort.GetPortNames() to detect a physical
        // USB unplug. See StartPacketPump's comment for why this, not IsOpen or a caught
        // exception, is the signal that's actually reliable on Linux.
        private string _connectedSerialPortName;

        public MainWindow()
        {
            InitializeComponent();

            _modeCombo = this.FindControl<ComboBox>("ModeCombo");
            _serialFields = this.FindControl<StackPanel>("SerialFields");
            _udpFields = this.FindControl<StackPanel>("UdpFields");
            _serialPortCombo = this.FindControl<ComboBox>("SerialPortCombo");
            _refreshPortsButton = this.FindControl<Button>("RefreshPortsButton");
            _baudCombo = this.FindControl<ComboBox>("BaudCombo");
            _baudCombo.ItemsSource = StandardBaudRates;
            _baudCombo.SelectedItem = 115200;
            _hostBox = this.FindControl<TextBox>("HostBox");
            _portBox = this.FindControl<TextBox>("PortBox");
            _connectButton = this.FindControl<Button>("ConnectButton");
            _connectStatusText = this.FindControl<TextBlock>("ConnectStatusText");

            _flightDataPanel = this.FindControl<Grid>("FlightDataPanel");
            _flightDataHost = this.FindControl<ContentControl>("FlightDataHost");
            _initialSetupPanel = this.FindControl<Grid>("InitialSetupPanel");
            _initialSetupHost = this.FindControl<ContentControl>("InitialSetupHost");
            _placeholderCard = this.FindControl<Border>("PlaceholderCard");
            _motorTestHost = this.FindControl<ContentControl>("MotorTestHost");
            _bottomConsolePanel = this.FindControl<Border>("BottomConsolePanel");
            _bottomConsoleHost = this.FindControl<ContentControl>("BottomConsoleHost");

            _navButtons = new Dictionary<string, Button>
            {
                ["FlightData"] = this.FindControl<Button>("NavFlightData"),
                ["FlightPlan"] = this.FindControl<Button>("NavFlightPlan"),
                ["InitialSetup"] = this.FindControl<Button>("NavInitialSetup"),
                ["ConfigTuning"] = this.FindControl<Button>("NavConfigTuning"),
                ["Simulation"] = this.FindControl<Button>("NavSimulation"),
                ["Help"] = this.FindControl<Button>("NavHelp"),
            };
            _sectionPanels = new Dictionary<string, Control>
            {
                ["FlightData"] = _flightDataPanel,
                ["FlightPlan"] = this.FindControl<ContentControl>("FlightPlanHost"),
                ["InitialSetup"] = _initialSetupPanel,
                ["ConfigTuning"] = this.FindControl<Grid>("ConfigTuningPanel"),
                ["Simulation"] = this.FindControl<ContentControl>("SimulationHost"),
                ["Help"] = this.FindControl<ContentControl>("HelpHost"),
            };

            // No live MAVLinkInterface needed for these - host them once, up front.
            // IsVisible is NOT set here (it stays at the XAML default of False) - only
            // ShowSection() is allowed to flip visibility, since it's the one place that
            // also hides whatever was previously active. Setting it True here as well as
            // in ShowSection used to mean all three of these started visible
            // simultaneously, stacked on top of the real default tab (ConfigTuning),
            // until enough nav clicks had cycled through each of them to correct it.
            ((ContentControl)_sectionPanels["FlightPlan"]).Content = new FlightPlanView();
            ((ContentControl)_sectionPanels["Simulation"]).Content = new SimulationView();
            ((ContentControl)_sectionPanels["Help"]).Content = new HelpView();

            RefreshSerialPorts();
        }

        private void OnNavClick(object sender, RoutedEventArgs e) => ShowSection((string)((Button)sender).Tag);

        private void ShowSection(string key)
        {
            if (key == _activeSection) return;

            _navButtons[_activeSection].Classes.Remove("NavTabActive");
            _sectionPanels[_activeSection].IsVisible = false;

            _activeSection = key;
            _navButtons[key].Classes.Add("NavTabActive");
            _sectionPanels[key].IsVisible = true;
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        private bool IsSerialMode => _modeCombo.SelectedIndex == 0;

        private void OnModeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_serialFields == null || _udpFields == null) return; // fires once during InitializeComponent
            _serialFields.IsVisible = IsSerialMode;
            _udpFields.IsVisible = !IsSerialMode;
            _connectStatusText.Text = IsSerialMode
                ? "Select the USB port your flight controller enumerates as (e.g. /dev/ttyACM0) and connect"
                : "UDP: waiting for a vehicle or SITL to send to this port";
        }

        private void OnRefreshPortsClick(object sender, RoutedEventArgs e) => RefreshSerialPorts();

        private void RefreshSerialPorts()
        {
            var ports = SerialPort.GetPortNames();
            var previous = _serialPortCombo.SelectedItem as string;

            _serialPortCombo.ItemsSource = ports;
            if (previous != null && ports.Contains(previous))
                _serialPortCombo.SelectedItem = previous;
            else if (ports.Length > 0)
                _serialPortCombo.SelectedIndex = 0;
        }

        private void OnConnectClick(object sender, RoutedEventArgs e)
        {
            if (_connecting) return;

            if (_mav != null)
            {
                Disconnect();
                return;
            }

            _ = ConnectAsync();
        }

        // MAVLinkInterface has no background reader of its own - MainV2.cs runs a
        // continuous thread that calls readPacketAsync() whenever bytes are waiting
        // (MainV2.cs, search "serialThread"); without an equivalent here, nothing gets
        // read after the initial connect handshake completes, so events like STATUSTEXT
        // (the Terminal bundle) or CurrentState updates (the FlightData bundle) never
        // arrive. minBytes=10 mirrors MainV2's own threshold.
        //
        // This loop also owns unplug detection. .NET's SerialPort.IsOpen does NOT reflect
        // a physical USB unplug on Linux - it stays true until Close()/Dispose() is called
        // explicitly, confirmed by MainV2.cs's own handling of this (MainV2.cs's
        // giveComport check: "i get alot of these errors, the port is still open, but not
        // valid - user has unpluged usb", wrapped in an empty catch around Close() and
        // polled via the same unreliable IsOpen). A caught read exception isn't conclusive
        // either - MainV2.cs sees plenty of those on a healthy connection too, hence its
        // own catch-and-continue. The one signal that's actually conclusive on Linux is the
        // device node disappearing from SerialPort.GetPortNames() entirely, so that's
        // polled periodically (not every 20ms - GetPortNames() enumerates /dev, no need to
        // hammer it) independent of whether a read exception happened this iteration.
        private void StartPacketPump(MAVLinkInterface mav)
        {
            _pumpCts = new CancellationTokenSource();
            var token = _pumpCts.Token;
            var portToWatch = _connectedSerialPortName; // null for a UDP connection - nothing to watch

            _ = Task.Run(async () =>
            {
                const int minBytes = 10;
                const int portCheckEveryNIterations = 50; // ~1s at the 20ms idle delay below
                var iterationsSincePortCheck = 0;

                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        if (mav.BaseStream != null && mav.BaseStream.IsOpen && !mav.giveComport &&
                            mav.BaseStream.BytesToRead > minBytes)
                        {
                            await mav.readPacketAsync().ConfigureAwait(false);
                        }
                        else
                        {
                            await Task.Delay(20, token).ConfigureAwait(false);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch
                    {
                        // best-effort, matches MainV2's own catch-and-continue pump loop -
                        // not conclusive by itself, see the port-presence check below
                    }

                    if (portToWatch != null && ++iterationsSincePortCheck >= portCheckEveryNIterations)
                    {
                        iterationsSincePortCheck = 0;
                        try
                        {
                            // Deliberately its own try/catch, separate from the read try/catch
                            // above: this loop is a fire-and-forget Task.Run with nothing
                            // observing its result, so an uncaught exception here (e.g.
                            // GetPortNames() throwing - its own implementation notes historical
                            // Linux "too many open files" flakiness) would otherwise silently
                            // kill the pump for good - no more telemetry, no unplug detection,
                            // no visible error. Worse than not having this check at all, so a
                            // failed check here just gets retried next cycle instead.
                            if (!SerialPort.GetPortNames().Contains(portToWatch))
                            {
                                var lostPort = portToWatch;
                                // global:: needed twice over: MainWindow (via Window) exposes an
                                // instance member named Dispatcher that shadows the static
                                // Dispatcher type, and this file's own namespace
                                // (MissionPlanner.Prototypes.Avalonia.DemoApp) has an "Avalonia"
                                // segment that shadows the real top-level Avalonia namespace.
                                global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                                    HandleUnexpectedDisconnect($"Disconnected: {lostPort} is no longer present (USB unplugged?)"));
                                break;
                            }
                        }
                        catch
                        {
                            // best-effort - retry on the next ~1s cycle rather than let this
                            // kill the whole (unobserved) background loop
                        }
                    }
                }
            }, token);
        }

        // Called from the packet pump (background thread, marshaled via Dispatcher.UIThread.Post)
        // when the connected serial port's device node vanishes - resets to the same
        // disconnected state a manual Disconnect() click produces, just with a status
        // message explaining why. Guarded against a race with a manual Disconnect() that
        // already ran first (_mav would already be null).
        private void HandleUnexpectedDisconnect(string reason)
        {
            if (_mav == null) return;
            Disconnect();
            _connectStatusText.Text = reason;
        }

        private void StopPacketPump()
        {
            _pumpCts?.Cancel();
            _pumpCts = null;
        }

        private void Disconnect()
        {
            StopPacketPump();

            try
            {
                // Closes BaseStream too (if open) - see MAVLinkInterface.Close().
                _mav?.Close();
            }
            catch
            {
                // best-effort, same as the WinForms original's disconnect handling
            }

            _mav = null;
            _connectedSerialPortName = null;

            // Tear down every bundle that needed the live MAVLinkInterface - each View's
            // DetachedFromVisualTree handler (where it has one, e.g. TerminalView) unhooks
            // its own event subscriptions when Content is cleared.
            _flightDataHost.Content = null;
            _flightDataHost.IsVisible = false;
            _initialSetupHost.Content = null;
            _initialSetupHost.IsVisible = false;
            _motorTestHost.Content = null;
            _motorTestHost.IsVisible = false;
            // Discarding the old TerminalView and building a fresh one on the next
            // connect is what resets its fold state back to expanded - no manual reset
            // needed here.
            _bottomConsoleHost.Content = null;
            _bottomConsolePanel.IsVisible = false;
            _placeholderCard.IsVisible = true;

            SetConnectedUiState(false);

            _connectStatusText.Text = IsSerialMode
                ? "Select the USB port your flight controller enumerates as (e.g. /dev/ttyACM0) and connect"
                : "UDP: waiting for a vehicle or SITL to send to this port";
        }

        private void SetConnectedUiState(bool connected)
        {
            _connectButton.Content = connected ? "Disconnect" : "Connect";
            _connectButton.Classes.Remove(connected ? "Primary" : "Danger");
            _connectButton.Classes.Add(connected ? "Danger" : "Primary");

            // Lock connection settings while connected so they can't be changed out from
            // under a live MAVLinkInterface.
            _modeCombo.IsEnabled = !connected;
            _serialPortCombo.IsEnabled = !connected;
            _refreshPortsButton.IsEnabled = !connected;
            _baudCombo.IsEnabled = !connected;
            _hostBox.IsEnabled = !connected;
            _portBox.IsEnabled = !connected;
        }

        private async Task ConnectAsync()
        {
            _connecting = true;

            _connectButton.IsEnabled = false;
            _connectStatusText.Text = "Connecting… (waits for 2 heartbeats, times out after ~30s)";
            _connectedSerialPortName = null; // set below only if this is a serial connect

            try
            {
                var mav = new MAVLinkInterface();

                if (IsSerialMode)
                {
                    var portName = _serialPortCombo.SelectedItem as string;
                    if (string.IsNullOrEmpty(portName))
                    {
                        _connectStatusText.Text = "Failed: no serial port selected";
                        return;
                    }

                    var baud = _baudCombo.SelectedItem is int selected ? selected : 115200;

                    var serial = new SerialPort { PortName = portName, BaudRate = baud };
                    // Must be set before Open() - on Linux, .NET's SerialPort otherwise
                    // asserts DTR/RTS as part of the open() transition, which trips this
                    // board's auto-reset-on-DTR circuit and knocks it back into a
                    // bootloader/app reset loop (confirmed via dmesg while debugging this
                    // live against real hardware - a CORVO N7 43 V1). MainV2.cs does the
                    // same thing before connecting (search DtrEnable).
                    serial.DtrEnable = false;
                    serial.RtsEnable = false;
                    mav.BaseStream = serial;
                    _connectedSerialPortName = portName;

                    // Deliberately NOT calling serial.Open() here - MAVLinkInterface.Open's
                    // OpenBg does that itself. Pre-opening ourselves first (even with DTR/RTS
                    // already false) was tried and reliably failed to detect a heartbeat
                    // against real hardware, for reasons not fully root-caused; letting
                    // MAVLinkInterface own the open, exactly like MainV2 does, works.
                    await Task.Run(() =>
                    {
                        // getparams:true, skipconnectedcheck:false, showui:false - showui=false
                        // is what makes this run headless (NoUIReporter instead of a WinForms
                        // progress dialog), which is what makes this usable from Avalonia/Linux
                        // at all. Exceptions on timeout are swallowed internally by
                        // MAVLinkInterface.
                        mav.Open(true, false, false);
                    });
                }
                else
                {
                    var host = _hostBox.Text?.Trim();
                    var port = _portBox.Text?.Trim();
                    var udp = new UdpSerialConnect();
                    mav.BaseStream = udp;

                    await Task.Run(() =>
                    {
                        udp.Open(host, port);
                        mav.Open(true, false, false);
                    });
                }

                // BaseStream.IsOpen is NOT a reliable "did we actually connect" signal here:
                // UdpSerialConnect's underlying socket stays open (IsOpen=true) even after a
                // timed-out connect attempt, since UDP has no real "closed" state to observe.
                // sysidcurrent only becomes non-zero once MAVLinkInterface.Open has actually
                // selected a heartbeat sender - verified against an unreachable UDP port
                // (sysidcurrent stayed 0, IsOpen stayed true) before relying on it here.
                if (mav.sysidcurrent == 0)
                {
                    _connectStatusText.Text = "Failed: no heartbeat received (timed out)";
                    return;
                }

                _mav = mav;
                StartPacketPump(_mav);

                _motorTestHost.Content = new ConfigMotorTestView(_mav, this);
                _motorTestHost.IsVisible = true;
                _placeholderCard.IsVisible = false;

                _flightDataHost.Content = new FlightDataView(_mav);
                _flightDataHost.IsVisible = true;

                _initialSetupHost.Content = new InitialSetupView(_mav);
                _initialSetupHost.IsVisible = true;

                _bottomConsoleHost.Content = new TerminalView(_mav);
                _bottomConsolePanel.IsVisible = true;

                _connectStatusText.Text = $"Connected (sysid {_mav.sysidcurrent}, compid {_mav.compidcurrent})";
                SetConnectedUiState(true);
            }
            catch (Exception ex)
            {
                _connectStatusText.Text = "Failed: " + ex.Message;
            }
            finally
            {
                _connectButton.IsEnabled = true;
                _connecting = false;
            }
        }
    }
}
