using System.Security.Principal;
using System.Windows;

namespace DDO_Launcher;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private async void Application_Startup(object sender, StartupEventArgs e)
    {
        var serverManager = new ServerManager("DDO_Launcher.ini");
        var mainWindow = new MainWindow(serverManager);

        await mainWindow.UpdateLauncher();
        await mainWindow.TranslationUpdateVerify(DDO_Launcher.Properties.Settings.Default.translationPatchUrl);

        mainWindow.Show();
    }
}

