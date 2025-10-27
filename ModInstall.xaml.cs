using Arrowgene.Ddon.Client;
using Arrowgene.Ddon.Shared.Csv;
using DDO_Launcher.Mods;
using Microsoft.Win32;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using static Arrowgene.Ddon.Client.GmdActions;


namespace DDO_Launcher
{
    public partial class ModSettingsWindow : Window
    {

        private readonly ModManager ModManager;

        public ModSettingsWindow()
        {
            InitializeComponent();
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        private void btnMsCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private async void btnInstallTranslation_Click(object sender, RoutedEventArgs e)
        {
            var client = new HttpClient();
            var waitWindow = new Window
            {
                Title = "Translation Patch",
                Width = 400,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new StackPanel
                {
                    Margin = new Thickness(15),
                    Children =
            {
                new TextBlock { Name = "MessageLabel", Text = "Preparing...", Margin = new Thickness(0, 0, 0, 10) },
                new ProgressBar { Name = "ProgressBar", Height = 20, Minimum = 0, Maximum = 100 }
            }
                }
            };

            try
            {
                string[] labels = { "Translated texts", "Original texts" };
                string[] languages = { "English", "Japanese" };
                int selectedLanguageIndex = ShowDropdownDialog("Select language", "Translation Patch", labels);
                if (selectedLanguageIndex == -1)
                    return;

                var result = ShowInputDialog("Install translation patch?", "Translation Patch", out string url, Properties.Settings.Default.translationPatchUrl);
                Properties.Settings.Default.translationPatchUrl = url;
                if (!result)
                    return;

                waitWindow.Show();
                (waitWindow.Content as StackPanel)!.Children.OfType<TextBlock>().First().Text = "Checking for translation patch updates...";

                var request = new HttpRequestMessage(HttpMethod.Head, Properties.Settings.Default.translationPatchUrl);
                request.Headers.Add("If-None-Match", Properties.Settings.Default.installedTranslationPatchETag);

                var response = await client.SendAsync(request);
                waitWindow.Hide();

                if (response.StatusCode == System.Net.HttpStatusCode.NotModified)
                {
                    var confirmation = MessageBox.Show(
                        "Translation patch is already up to date.\nDo you want to reinstall it?",
                        "Translation Patch",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (confirmation == MessageBoxResult.No)
                        return;
                }

                waitWindow.Show();
                (waitWindow.Content as StackPanel)!.Children.OfType<TextBlock>().First().Text = "Downloading translation patch...";

                var patchDownload = await client.GetAsync(Properties.Settings.Default.translationPatchUrl);
                Properties.Settings.Default.installedTranslationPatchETag = patchDownload.Headers.ETag?.ToString();

                Stream stream = await patchDownload.Content.ReadAsStreamAsync();

                GmdCsv gmdCsvReader = new GmdCsv();
                List<GmdCsv.Entry> gmdCsvEntries = gmdCsvReader.Read(stream);

                var progressBar = (waitWindow.Content as StackPanel)!.Children.OfType<ProgressBar>().First();
                var messageLabel = (waitWindow.Content as StackPanel)!.Children.OfType<TextBlock>().First();
                messageLabel.Text = "Patching client...";
                progressBar.Value = 0;

                var progress = new Progress<PackProgressReport>(progressReport =>
                {
                    progressBar.Maximum = progressReport.Total;
                    progressBar.Value = progressReport.Current;
                });

                try
                {
                    await Task.Run(() =>
                        GmdActions.Pack(gmdCsvEntries, "nativePC/rom", languages[selectedLanguageIndex], progress));
                }
                catch (Exception ex)
                {
                    throw new Exception("Failed to patch GMD files\n\n" + ex.Message);
                }

                waitWindow.Close();
                Properties.Settings.Default.firstInstalledTranslation = true;
                Properties.Settings.Default.Save();

                MessageBox.Show(
                    "Translation patch applied successfully",
                    "Translation applied",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                waitWindow.Close();
                MessageBox.Show(
                    "Error: " + ex.Message,
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        public static int ShowDropdownDialog(string message, string title, string[] options)
        {
            var dialog = new Window
            {
                Title = title,
                Width = 400,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow,
                Background = System.Windows.Media.Brushes.White,
                Owner = Application.Current.MainWindow
            };

            var grid = new Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var messageLabel = new TextBlock
            {
                Text = message,
                Margin = new Thickness(0, 0, 0, 10),
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(messageLabel, 0);

            var comboBox = new ComboBox
            {
                ItemsSource = options,
                SelectedIndex = 0,
                Margin = new Thickness(0, 0, 0, 10),
                Height = 25
            };
            Grid.SetRow(comboBox, 1);

            var buttonsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var okButton = new Button { Content = "OK", Width = 80, Margin = new Thickness(5) };
            var cancelButton = new Button { Content = "Cancel", Width = 80, Margin = new Thickness(5) };

            buttonsPanel.Children.Add(cancelButton);
            buttonsPanel.Children.Add(okButton);
            Grid.SetRow(buttonsPanel, 2);

            grid.Children.Add(messageLabel);
            grid.Children.Add(comboBox);
            grid.Children.Add(buttonsPanel);
            dialog.Content = grid;

            int selectedIndex = -1;

            okButton.Click += (_, __) =>
            {
                selectedIndex = comboBox.SelectedIndex;
                dialog.DialogResult = true;
                dialog.Close();
            };

            cancelButton.Click += (_, __) =>
            {
                dialog.DialogResult = false;
                dialog.Close();
            };

            bool? result = dialog.ShowDialog();

            return result == true ? selectedIndex : -1;
        }

        public static bool ShowInputDialog(string message, string title, out string chosenUrl, string defaultUrl = "")
        {
            var dialog = new Window
            {
                Title = title,
                Width = 400,
                Height = 180,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow,
                Background = System.Windows.Media.Brushes.White,
                Owner = Application.Current.MainWindow
            };

            var grid = new Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var messageLabel = new TextBlock
            {
                Text = message,
                Margin = new Thickness(0, 0, 0, 10),
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(messageLabel, 0);

            var inputBox = new TextBox
            {
                Text = defaultUrl,
                Height = 25,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(inputBox, 1);

            var buttonsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var okButton = new Button { Content = "OK", Width = 80, Margin = new Thickness(5) };
            var cancelButton = new Button { Content = "Cancel", Width = 80, Margin = new Thickness(5) };

            buttonsPanel.Children.Add(cancelButton);
            buttonsPanel.Children.Add(okButton);
            Grid.SetRow(buttonsPanel, 2);

            grid.Children.Add(messageLabel);
            grid.Children.Add(inputBox);
            grid.Children.Add(buttonsPanel);
            dialog.Content = grid;

            bool? result = null;

            okButton.Click += (_, __) =>
            {
                result = true;
                dialog.DialogResult = true;
                dialog.Close();
            };

            cancelButton.Click += (_, __) =>
            {
                result = false;
                dialog.DialogResult = false;
                dialog.Close();
            };

            dialog.ShowDialog();

            chosenUrl = inputBox.Text;
            return result == true;
        }

        private async void buttonInstallMod_Click(object sender, RoutedEventArgs e)
        {
            var messageLabel = new TextBlock
            {
                Text = "Preparing...",
                Margin = new Thickness(0, 0, 0, 10)
            };

            var progressBar = new ProgressBar
            {
                Height = 20,
                Minimum = 0,
                Maximum = 100
            };

            var waitWindow = new Window
            {
                Title = "Mod Installation",
                Width = 400,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow,
                Content = new StackPanel
                {
                    Margin = new Thickness(15),
                    Children = { messageLabel, progressBar }
                }
            };

            try
            {
                var selectModFileDialog = new OpenFileDialog
                {
                    InitialDirectory = System.IO.Directory.GetCurrentDirectory(),
                    Filter = "DDOn Mod Zip Files (*.zip)|*.zip",
                    FilterIndex = 0,
                    RestoreDirectory = true
                };

                var dialogResult = selectModFileDialog.ShowDialog(this);
                if (dialogResult != true)
                    return;

                waitWindow.Show();
                progressBar.IsIndeterminate = false;
                progressBar.Value = 0;

                string name = "", author = "";
                var _modManager = new ModManager();

                await _modManager.InstallMod(
                    selectModFileDialog.FileName,
                    new Progress<ModInstallProgress>(progressReport =>
                    {
                        name = progressReport.Name;
                        author = progressReport.Author;

                        messageLabel.Text = $"Installing \"{name}\"\nAuthor: {author}";
                        progressBar.Maximum = progressReport.TotalActionCount;
                        progressBar.Value = progressReport.ProcessedTotalActions;
                    })
                );

                waitWindow.Close();
                Properties.Settings.Default.Save();

                MessageBox.Show(
                    $"Successfully installed {name} (Author {author})",
                    "Mod installed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            catch (Exception ex)
            {
                waitWindow.Close();
                MessageBox.Show(ex.Message, 
                    "Error", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error);
            }
        }
    }
}
