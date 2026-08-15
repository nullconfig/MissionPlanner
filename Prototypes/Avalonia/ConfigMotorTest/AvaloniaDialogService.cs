using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace MissionPlanner.Prototypes.Avalonia.ConfigMotorTest
{
    // Real (non-stub) Avalonia implementation of IDialogService - replaces WinForms'
    // InputBox.Show(...) and CustomMessageBox.Show(...) with small modal Windows
    // (PromptDialog / MessageDialog). Needs a Window to own the dialogs (for centering
    // and modality), so the host passes in the top-level window.
    public class AvaloniaDialogService : IDialogService
    {
        private readonly Window _owner;

        public AvaloniaDialogService(Window owner)
        {
            _owner = owner;
        }

        public async Task<int?> PromptForIntAsync(string title, string prompt, int defaultValue)
        {
            var dialog = new PromptDialog(title, prompt, defaultValue);
            await dialog.ShowDialog(_owner);
            return dialog.Result;
        }

        public async Task ShowErrorAsync(string title, string message)
        {
            var dialog = new MessageDialog(title, message);
            await dialog.ShowDialog(_owner);
        }

        public void OpenUrl(string url)
        {
            // Process.Start with UseShellExecute works the same way cross-platform on
            // modern .NET - this piece of the original needed no change.
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
    }
}
