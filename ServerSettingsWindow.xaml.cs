using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace DDO_Launcher
{
    /// <summary>
    /// Lógica interna para ServerSettingsWindow.xaml
    /// </summary>
    public partial class ServerSettingsWindow : Window
    {
        public ServerSettingsWindow()
        {
            InitializeComponent();
        }

        private void btnSmRemove_Click(object sender, RoutedEventArgs e)
        {

        }

        private void btnSmAdd_Click(object sender, RoutedEventArgs e)
        {

        }

        private void btnSmCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
