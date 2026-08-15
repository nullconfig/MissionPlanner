using System;
using System.ComponentModel;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace MissionPlanner.Prototypes.Avalonia.Terminal
{
    public partial class TerminalView : UserControl
    {
        private readonly TerminalViewModel _viewModel = new();
        private readonly MAVLinkInterface _mav;

        private readonly TextBlock _foldToggleIcon;
        private readonly Border _consoleContent;
        private readonly Button _filterAllButton;
        private readonly Button _filterInfoButton;
        private readonly Button _filterErrorButton;
        private readonly TextBlock _lineCountText;
        private readonly TextBox _consoleTextBox;

        // mav must already be connected (or about to be) - this view doesn't open/close
        // it, only subscribes to OnPacketReceived for as long as the view is alive. The
        // host is still responsible for running a packet pump against the same
        // MAVLinkInterface (see ../DemoApp/README.md) - without one, nothing ever arrives
        // here, the same gap that was found and fixed while this code still lived in
        // DemoApp directly.
        public TerminalView(MAVLinkInterface mav)
        {
            InitializeComponent();

            _mav = mav;
            _foldToggleIcon = this.FindControl<TextBlock>("FoldToggleIcon");
            _consoleContent = this.FindControl<Border>("ConsoleContent");
            _filterAllButton = this.FindControl<Button>("FilterAllButton");
            _filterInfoButton = this.FindControl<Button>("FilterInfoButton");
            _filterErrorButton = this.FindControl<Button>("FilterErrorButton");
            _lineCountText = this.FindControl<TextBlock>("LineCountText");
            _consoleTextBox = this.FindControl<TextBox>("ConsoleTextBox");

            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _mav.OnPacketReceived += OnMavPacketReceived;
            DetachedFromVisualTree += (_, __) => _mav.OnPacketReceived -= OnMavPacketReceived;
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            _consoleTextBox.Text = _viewModel.ConsoleText;
            _consoleTextBox.CaretIndex = _consoleTextBox.Text.Length; // auto-scroll to newest
            _lineCountText.Text = _viewModel.LineCountText;
        }

        private void OnFoldToggleClick(object sender, RoutedEventArgs e)
        {
            _consoleContent.IsVisible = !_consoleContent.IsVisible;
            _foldToggleIcon.Text = _consoleContent.IsVisible ? "▼" : "▶";
        }

        private void OnFilterClick(object sender, RoutedEventArgs e)
        {
            var filter = (string)((Button)sender).Tag;
            _viewModel.SetFilter(filter);

            foreach (var (button, tag) in new[] { (_filterAllButton, "All"), (_filterInfoButton, "Info"), (_filterErrorButton, "Error") })
            {
                var active = tag == filter;
                button.Classes.Remove(active ? "FilterChip" : "FilterChipActive");
                button.Classes.Add(active ? "FilterChipActive" : "FilterChip");
            }
        }

        // Fires on a background thread (the host's packet pump) - only STATUSTEXT (the
        // vehicle's boot/status/prearm messages, same as Mission Planner's own "Messages"
        // tab) gets appended to the console, marshaled onto the UI thread.
        private void OnMavPacketReceived(object sender, MAVLink.MAVLinkMessage message)
        {
            if (message.msgid != (byte)MAVLink.MAVLINK_MSG_ID.STATUSTEXT) return;

            var msg = message.ToStructure<MAVLink.mavlink_statustext_t>();
            var text = Encoding.UTF8.GetString(msg.text);
            var nul = text.IndexOf('\0');
            if (nul != -1) text = text.Substring(0, nul);
            var severity = msg.severity;
            var time = DateTime.Now;

            Dispatcher.UIThread.Post(() => _viewModel.Add(time, severity, text));
        }
    }
}
