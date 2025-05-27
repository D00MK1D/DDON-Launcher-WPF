using System.Configuration;
using System.Data;
using System.Windows;

namespace DDO_Launcher;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private void Application_Startup(object sender, StartupEventArgs e)
    {
        var serverManager = new ServerManager("DDO_Launcher.ini");
        var mainWindow = new MainWindow(serverManager);
        mainWindow.Show();
    }
}

