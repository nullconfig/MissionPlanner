using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace MissionPlanner.Prototypes.Avalonia.ConfigMotorTest
{
    public partial class PromptDialog : Window
    {
        private readonly TextBlock _promptTextBlock;
        private readonly NumericUpDown _valueInput;

        public int? Result { get; private set; }

        public PromptDialog()
        {
            InitializeComponent();
            _promptTextBlock = this.FindControl<TextBlock>("PromptTextBlock");
            _valueInput = this.FindControl<NumericUpDown>("ValueInput");
        }

        public PromptDialog(string title, string prompt, int defaultValue) : this()
        {
            Title = title;
            _promptTextBlock.Text = prompt;
            _valueInput.Value = defaultValue;
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        private void OnOk(object sender, RoutedEventArgs e)
        {
            Result = (int?)_valueInput.Value;
            Close();
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            Result = null;
            Close();
        }
    }
}
