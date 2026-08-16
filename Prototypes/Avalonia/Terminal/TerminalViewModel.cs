using System;
using System.Collections.Generic;
using System.Text;
using ReactiveUI;

namespace MissionPlanner.Prototypes.Avalonia.Terminal
{
    // Real feed: STATUSTEXT messages off a connected MAVLinkInterface - the same data
    // Mission Planner's own "Messages" tab shows. Logic moved here unchanged from
    // ../DemoApp/MainWindow.axaml.cs, where it originally lived before the per-tab bundle
    // split. No Avalonia dependency here (matches ConfigMotorTestViewModel's design) -
    // TerminalView owns the MAVLinkInterface subscription and UI-thread marshaling, this
    // class just holds/filters/renders the data. ReactiveObject (ReactiveUI, added
    // 2026-08-15) replaces a hand-rolled INotifyPropertyChanged - same bindings, no
    // boilerplate PropertyChanged event/OnPropertyChanged method to maintain per class.
    public class TerminalViewModel : ReactiveObject
    {
        // severity follows MAVLink's MAV_SEVERITY: 0=EMERGENCY ... 6=INFO, 7=DEBUG (lower
        // is more severe). Kept per-message (not just flattened text) so the All/Info/Error
        // filter can be applied retroactively, not just to new messages.
        private readonly List<(DateTime time, byte severity, string text, int count)> _messages = new();

        private string _filter = "All";
        public string Filter => _filter;

        private string _consoleText = "(no messages yet)";
        public string ConsoleText
        {
            get => _consoleText;
            private set => this.RaiseAndSetIfChanged(ref _consoleText, value);
        }

        private string _lineCountText = "0 messages";
        public string LineCountText
        {
            get => _lineCountText;
            private set => this.RaiseAndSetIfChanged(ref _lineCountText, value);
        }

        public void SetFilter(string filter)
        {
            _filter = filter;
            Render();
        }

        // How far back to look for a repeat of an incoming message. ArduPilot's startup
        // banner is 5 distinct lines sent as a burst, and the whole burst repeats 2-3
        // times within a couple seconds - each line's own repeat isn't adjacent to itself,
        // it's a few lines further back. A short window keeps genuinely recurring-but-
        // unrelated messages minutes apart (periodic PreArm checks) from being collapsed.
        private static readonly TimeSpan DedupWindow = TimeSpan.FromSeconds(5);

        public void Add(DateTime time, byte severity, string text)
        {
            for (var i = _messages.Count - 1; i >= 0 && (time - _messages[i].time) <= DedupWindow; i--)
            {
                var existing = _messages[i];
                if (existing.severity == severity && existing.text == text)
                {
                    _messages[i] = (existing.time, severity, text, existing.count + 1);
                    Render();
                    return;
                }
            }

            _messages.Add((time, severity, text, 1));
            if (_messages.Count > 1000)
                _messages.RemoveAt(0);

            Render();
        }

        public void Clear()
        {
            _messages.Clear();
            Render();
        }

        // MAV_SEVERITY: 0=EMERGENCY..3=ERROR..5=NOTICE, 6=INFO, 7=DEBUG - lower is more
        // severe, and each filter level shows that level and everything more severe
        // (standard log-level filter convention), not an exact match.
        private bool PassesFilter(byte severity) => _filter switch
        {
            "Info" => severity <= (byte)MAVLink.MAV_SEVERITY.INFO,
            "Error" => severity <= (byte)MAVLink.MAV_SEVERITY.ERROR,
            _ => true,
        };

        private void Render()
        {
            var buffer = new StringBuilder();
            var shown = 0;
            foreach (var (time, severity, text, count) in _messages)
            {
                if (!PassesFilter(severity)) continue;
                buffer.Append('[').Append(time.ToString("HH:mm:ss")).Append("] ").Append(text);
                if (count > 1) buffer.Append(" (x").Append(count).Append(')');
                buffer.Append('\n');
                shown++;
            }

            ConsoleText = shown > 0 ? buffer.ToString() : "(no messages yet)";
            LineCountText = _messages.Count == 0
                ? "0 messages"
                : $"{shown} of {_messages.Count} message{(_messages.Count == 1 ? "" : "s")}";
        }
    }
}
