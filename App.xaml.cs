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
        if (!IsRunAsAdmin())
        {
            MessageBox.Show(
                "Please, run the launcher as administrator.",
                "Attention!",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );

            Environment.Exit(0);
        }

        var serverManager = new ServerManager("DDO_Launcher.ini");
        var mainWindow = new MainWindow(serverManager);

        await mainWindow.UpdateLauncher();
        await mainWindow.TranslationUpdateVerify(DDO_Launcher.Properties.Settings.Default.translationPatchUrl);

        mainWindow.Show();
    }

    private static bool IsRunAsAdmin()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}

