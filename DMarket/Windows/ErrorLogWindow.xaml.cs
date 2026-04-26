using System.Windows;

namespace DMarket.Windows
{
    public partial class ErrorLogWindow : Window
    {
        public ErrorLogWindow()
        {
            InitializeComponent();
            RefreshLog();
        }

        private void RefreshLog()
        {
            LogTextBox.Text = AppDiagnostics.BuildText();
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshLog();
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(LogTextBox.Text))
            {
                System.Windows.Clipboard.SetText(LogTextBox.Text);
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            AppDiagnostics.Clear();
            RefreshLog();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
