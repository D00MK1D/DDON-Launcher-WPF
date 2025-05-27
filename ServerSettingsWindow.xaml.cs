using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IniParser.Model;
using IniParser;
using IniParser.Parser;
using System.Windows;


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
