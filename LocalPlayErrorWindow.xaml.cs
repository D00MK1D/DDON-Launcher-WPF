#nullable enable

using System.Text;
using System.Windows;

namespace DDO_Launcher
{
    public partial class LocalPlayErrorWindow : Window
    {
        public LocalPlayErrorWindow(Window owner, string errorMessage, string? fullServerLog)
        {
            InitializeComponent();
            Owner = owner;

            var sb = new StringBuilder();
            sb.AppendLine(errorMessage);

            if (!string.IsNullOrWhiteSpace(fullServerLog))
            {
                sb.AppendLine();
                sb.AppendLine("=== Full Server Log ===");
                sb.Append(fullServerLog);
            }

            textDetails.Text = sb.ToString();
        }

        private void btnCopy_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(textDetails.Text);
            btnCopy.Content = "Copied!";
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
