using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace MissionPlanner.Prototypes.Avalonia.ConfigMotorTest
{
    public partial class MessageDialog : Window
    {
        private readonly TextBlock _messageTextBlock;

        public MessageDialog()
        {
            InitializeComponent();
            _messageTextBlock = this.FindControl<TextBlock>("MessageTextBlock");
        }

        public MessageDialog(string title, string message) : this()
        {
            Title = title;
            _messageTextBlock.Text = message;
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        private void OnOk(object sender, RoutedEventArgs e) => Close();
    }
}
