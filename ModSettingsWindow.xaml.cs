using System.Windows;


namespace DDO_Launcher
{
    /// <summary>  
    /// Lógica interna para ModSettingsWindow.xaml  
    /// </summary>  
    public partial class ModSettingsWindow : Window
    {
        public ModSettingsWindow()
        {
            InitializeComponent();
        }

        private void btnMsCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
