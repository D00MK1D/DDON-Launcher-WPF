using Arrowgene.Ddon.Client;
using Arrowgene.Ddon.Shared.Csv;
using DDO_Launcher.Mods;
using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static Arrowgene.Ddon.Client.GmdActions;

namespace DDO_Launcher;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly ServerManager ServerManager;

    private string _logo;
    private string _background;

    private BitmapImage _customBackground;
    private BitmapImage _customLogo;

    public BitmapImage CustomBackground
    {
        get => _customBackground;
        set
        {
            if (_customBackground != value)
            {
                _customBackground = value;
                OnPropertyChanged(nameof(CustomBackground));
            }
        }
    }

    public BitmapImage CustomLogo
    {
        get => _customLogo;
        set
        {
            if (_customLogo != value)
            {
                _customLogo = value;
                OnPropertyChanged(nameof(CustomLogo));
            }
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public MainWindow(ServerManager serverManager)
    {
        InitializeComponent();
        this.WindowStartupLocation = WindowStartupLocation.CenterScreen;

        ServerManager = serverManager;
        UpdateServerList();
        DataContext = this;

        CustomBackground = new BitmapImage(new Uri("pack://application:,,,/Images/background.jpg"));
        CustomLogo = new BitmapImage(new Uri("pack://application:,,,/Images/logo.png"));

        Dispatcher.BeginInvoke(new Action(async () =>
        {
            _logo = await GetCustomImagesAsync("logo.png");
            _background = await GetCustomImagesAsync("background.jpg");

            try
            {
                CustomBackground = new BitmapImage(new Uri(_background));
            }
            catch
            {
                //Do nothing
            }

            try
            {
                CustomLogo = new BitmapImage(new Uri(_logo));
            }
            catch
            {
                //Do nothing
            }
        }), System.Windows.Threading.DispatcherPriority.Background);

        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await UpdateLauncher();
        await TranslationUpdateVerify(Properties.Settings.Default.translationPatchUrl);

        if (Properties.Settings.Default.rememberMe)
        {
            textAccount.Text = Properties.Settings.Default.accountText;
            textPassword.Password = Properties.Settings.Default.passwordText;
            cbRemember.IsChecked = Properties.Settings.Default.rememberMe;
            serverComboBox.SelectedIndex = Properties.Settings.Default.lastServerSelected;
        }
    }

    private void btnSubmit_Click(object sender, RoutedEventArgs e)
    {
        switch ((string)btnSubmit.Content)
        {
            case "Login":
                Operation("login");
                break;

            case "Register":
                Operation("create");
                break;
        }
    }

    private void btnChangeAction_Click(object sender, RoutedEventArgs e)
    {
        if ((string)btnSubmit.Content == "Login")
        {
            btnSubmit.Content = "Register";
            btnChangeAction.Content = "Login";
            labelServer.Margin = new Thickness(6, 147, 10, 0);
            serverComboBox.Margin = new Thickness(10, 172, 43, 0);
            btnServerSettings.Margin = new Thickness(218, 172, 10, 77);
            labelEmail.Visibility = Visibility.Visible;
            textEmail.Visibility = Visibility.Visible;
            labelRemember.Visibility = Visibility.Hidden;
            cbRemember.Visibility = Visibility.Hidden;
        }

        else //if ((string)btnSubmit.Content == "Register")
        {
            btnSubmit.Content = "Login";
            btnChangeAction.Content = "Register";
            labelServer.Margin = new Thickness(6, 98, 14, 0);
            serverComboBox.Margin = new Thickness(10, 124, 43, 0);
            btnServerSettings.Margin = new Thickness(218, 124, 10, 0);
            serverComboBox.Visibility = Visibility.Visible;
            labelEmail.Visibility = Visibility.Hidden;
            textEmail.Visibility = Visibility.Hidden;
            labelRemember.Visibility = Visibility.Visible;
            cbRemember.Visibility = Visibility.Visible;
        }
    }

    public class ServerResponse
    {
        public string Error { get; set; }
        public string Message { get; set; }
        public string Token { get; set; }
    }

    private async void Operation(string Action)
    {

        string jsonData;

        btnChangeAction.IsEnabled = false;
        btnSubmit.IsEnabled = false;

        if ((Action != "create" && Action != "login") || textAccount.Text == "" || textPassword.Password == "")
        {
            MessageBox.Show(
                "Account or Password must not be empty!",
                "Dragon's Dogma Online",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            btnChangeAction.IsEnabled = true;
            btnSubmit.IsEnabled = true;

            return;

        }

        using (HttpClient client = new HttpClient())
        {
            var path = "/api/account";
            var requestData = new
            {
                Action = Action,
                Account = textAccount.Text,
                Password = textPassword.Password,
                Email = textEmail.Text
            };

            jsonData = JsonSerializer.Serialize(requestData);
            var content = new StringContent(jsonData, Encoding.UTF8, "application/json");
            client.DefaultRequestVersion = HttpVersion.Version11;
            HttpResponseMessage response = new HttpResponseMessage();

            try
            {
                //response = await client.PostAsync("http://localhost:52099" + path, content);
                response = await client.PostAsync("http://" + ServerManager.Servers[ServerManager.SelectedServer].DLIP +
                              ":" + ServerManager.Servers[ServerManager.SelectedServer].DLPort + path, content);
            }
            catch (HttpRequestException e)
            {
                MessageBox.Show(
                    "Server not reachable!\n" +
                    "Error: " + e.Message,
                    "Dragon's Dogma Online",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                btnChangeAction.IsEnabled = true;
                btnSubmit.IsEnabled = true;

                return;
            }

            var responseBody = await response.Content.ReadAsStringAsync();

            ServerResponse serverResponse;
            try
            {
                serverResponse = JsonSerializer.Deserialize<ServerResponse>(responseBody);

            }
            catch (JsonException e)
            {
                MessageBox.Show(
                    "Invalid response from server\n" +
                    "Error: " + e.Message + "\n" +
                    "Message: " + responseBody + "\n",
                    "Dragon's Dogma Online",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                btnChangeAction.IsEnabled = true;
                btnSubmit.IsEnabled = true;

                return;
            }

            try
            {
                if (!string.IsNullOrEmpty(serverResponse.Token))
                {
                    try
                    {
                        if (cbRemember.IsChecked == true)
                        {
                            Properties.Settings.Default.accountText = textAccount.Text;
                            Properties.Settings.Default.passwordText = textPassword.Password;
                            Properties.Settings.Default.rememberMe = true;
                            Properties.Settings.Default.lastServerSelected = serverComboBox.SelectedIndex;

                            Properties.Settings.Default.Save();
                        }

                            Process.Start("ddo.exe",
                                          " addr=" + ServerManager.Servers[ServerManager.SelectedServer].LobbyIP +
                                          " port=" + ServerManager.Servers[ServerManager.SelectedServer].LPort +
                                          " token=" + serverResponse.Token +
                                          " DL=http://" + ServerManager.Servers[ServerManager.SelectedServer].DLIP +
                                          ":" + ServerManager.Servers[ServerManager.SelectedServer].DLPort +
                                          "/win/ LVer=03.04.003.20181115.0 RVer=3040008");

                        btnChangeAction.IsEnabled = true;
                        btnSubmit.IsEnabled = true;
                        
                        Application.Current.Shutdown();
                    }
                    catch (Win32Exception e)
                    {
                        if (e.NativeErrorCode == 2)
                        {
                            MessageBox.Show(
                                "Launcher couldn't find DDO.exe!\nMake sure the launcher is located in the game folder",
                                "Dragon's Dogma Online",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                        }
                        else if(e.NativeErrorCode == 740)
                        {
                            MessageBox.Show(
                                "Launcher couldn't run DDO.exe!\nMake sure the launcher is running as Admin.",
                                "Dragon's Dogma Online",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                        }

                        btnChangeAction.IsEnabled = true;
                        btnSubmit.IsEnabled = true;
                        return;
                    }
                }
                else if (serverResponse.Error == null)
                {
                    MessageBox.Show(
                        serverResponse.Message,
                        "Dragon's Dogma Online",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    btnChangeAction.IsEnabled = true;
                    btnSubmit.IsEnabled = true;
                    return;
                }
                else
                {
                    MessageBox.Show(
                        serverResponse.Error,
                        "Dragon's Dogma Online",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    btnChangeAction.IsEnabled = true;
                    btnSubmit.IsEnabled = true;
                    return;
                }
            }
            catch (JsonException e)
            {
                MessageBox.Show(
                    "Invalid response from server\n" +
                    "Error: " + serverResponse.Error + "\n" +
                    "Message: " + serverResponse.Message + "\n" +
                    "Message: " + client.ToString() + "\n",
                    "Dragon's Dogma Online",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                btnChangeAction.IsEnabled = true;
                btnSubmit.IsEnabled = true;
                return;
            }
        }
    }

    private void btnServerSettings_Click(object sender, RoutedEventArgs e)
    {
        ServerSettingsWindow ssw = new ServerSettingsWindow(ServerManager);
        ssw.ShowDialog();

        UpdateServerList();

    }

    private void btnMinimize_Click(object sender, RoutedEventArgs e)
    {
        this.WindowState = WindowState.Minimized;
    }

    private void btnClose_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void UpdateServerList()
    {
        serverComboBox.Items.Clear();
        foreach (var server in ServerManager.Servers)
        {
            int addedItemIndex = serverComboBox.Items.Add(server.Key);
            if (serverComboBox.SelectedIndex == -1 && server.Key == ServerManager.SelectedServer)
            {
                serverComboBox.SelectedIndex = addedItemIndex;
            }
        }

        if (serverComboBox.SelectedIndex == -1)
        {
            serverComboBox.SelectedIndex = 0;
        }
    }

    private async Task<string> GetCustomImagesAsync(string img)
    {
        string imageUrl = $"http://{ServerManager.Servers[ServerManager.SelectedServer].DLIP}:{ServerManager.Servers[ServerManager.SelectedServer].DLPort}/launcher/{img}";

        try
        {
            using (HttpClient client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(5);

                var request = new HttpRequestMessage(HttpMethod.Get, imageUrl);
                var response = await client.SendAsync(request);

                if (response.IsSuccessStatusCode)
                    return imageUrl;
                else
                    return $"pack://application:,,,/Images/{img}";
            }
        }
        catch
        {
            return $"pack://application:,,,/Images/{img}";
        }
    }

    private async void serverComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (serverComboBox.SelectedItem != null)
        {
            try
            {
                string html;
                string newsUrl = $"http://{ServerManager.Servers[ServerManager.SelectedServer].DLIP}:{ServerManager.Servers[ServerManager.SelectedServer].DLPort}/news/news.html";
                string newsBannerUrl = $"http://{ServerManager.Servers[ServerManager.SelectedServer].DLIP}:{ServerManager.Servers[ServerManager.SelectedServer].DLPort}/news/newsbanner.jpg";

                ServerManager.SelectServer((string)serverComboBox.SelectedItem);

                _background = await GetCustomImagesAsync("background.jpg");
                _logo = await GetCustomImagesAsync("logo.png");

                CustomBackground = new BitmapImage(new Uri(_background));
                CustomLogo = new BitmapImage(new Uri(_logo));

                try
                {
                    using HttpClient client = new();
                    using HttpRequestMessage request = new(HttpMethod.Head, newsBannerUrl);
                    using HttpResponseMessage response = await client.SendAsync(request);

                    if (response.IsSuccessStatusCode)
                    {
                        imgNewsBanner.Source = new BitmapImage(new Uri(newsBannerUrl));
                    }
                    else
                    {
                        imgNewsBanner.Source = new BitmapImage(new Uri("pack://application:,,,/Images/newsbanner.png"));
                    }
                }
                catch
                {
                    imgNewsBanner.Source = new BitmapImage(new Uri("pack://application:,,,/Images/newsbanner.png"));
                }

                try
                {
                    html = await new HttpClient().GetStringAsync(newsUrl);

                    textAnnoucementTitle.Text = WebUtility.HtmlDecode(
                        Regex.Replace(Regex.Match(html, "<title[^>]*>([\\s\\S]*?)</title>").Groups[1].Value, "<.*?>", "")
                        .Trim()
                    );

                    textAnnouncementType.Text = WebUtility.HtmlDecode(
                        Regex.Replace(Regex.Match(html, "<type[^>]*>([\\s\\S]*?)</type>").Groups[1].Value, "<.*?>", "")
                        .Trim()
                    );

                    switch (textAnnouncementType.Text?.Trim().ToUpper())
                    {
                        case "UNAVAILABILITY":
                            colorAnnouncementTypeBg.Background =
                                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B1224A"));
                            break;

                        case "MAINTENANCE":
                            colorAnnouncementTypeBg.Background =
                                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#845E14"));
                            break;

                        case "UPDATE":
                            colorAnnouncementTypeBg.Background =
                                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#615D9F"));
                            break;

                        case "INFORMATION":
                            colorAnnouncementTypeBg.Background =
                                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#247CAA"));
                            break;

                        case "EVENT":
                            colorAnnouncementTypeBg.Background =
                                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#348B3A"));
                            break;

                        default:
                            textAnnouncementType.Text = "NO EVENT?";
                            colorAnnouncementTypeBg.Background =
                                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#348B3A"));
                            break;
                    }

                    textAnnouncementDate.Text = WebUtility.HtmlDecode(
                        Regex.Replace(Regex.Match(html, "<date[^>]*>([\\s\\S]*?)</date>").Groups[1].Value, "<.*?>", "")
                        .Trim()
                    );

                    textAnnoucementContent.Text = WebUtility.HtmlDecode(
                        Regex.Replace(Regex.Match(html, "<content[^>]*>([\\s\\S]*?)</content>").Groups[1].Value, "<.*?>", "")
                        .Trim()
                    );
                }
                catch
                {
                    textAnnoucementTitle.Text = "No Title?";
                    colorAnnouncementTypeBg.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#348B3A"));
                    textAnnouncementType.Text = "NO EVENT?";
                    textAnnouncementDate.Text = "No Date?";
                    textAnnoucementContent.Text = "No Description?";
                    return;
                }
            }
            catch (ArgumentException)
            {
                MessageBox.Show(
                    "Selected server not found!",
                    "Dragon's Dogma Online",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                UpdateServerList();
                return;
            }
        }
        else
        {
            return;
        }
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

            waitWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            waitWindow.Show();
            
            (waitWindow.Content as StackPanel)!.Children.OfType<TextBlock>().First().Text = "Checking for translation patch updates...";

            var update = await TranslationUpdateVerify(url);

            waitWindow.Hide();

            if (update == true)
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

            btnTranslationPath.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D3D3D3"));
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

    private async void btnInstallMod_Click(object sender, RoutedEventArgs e)
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

    private void DragWindow_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            this.DragMove();
        }
    }

    private async Task<bool> TranslationUpdateVerify(string url)
    {
        try
        {
            if (Properties.Settings.Default.firstInstalledTranslation != true)
                return false;

            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("DDO_Launcher");

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(Properties.Settings.Default.installedTranslationPatchETag));

            var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                return true;
            }

            btnTranslationPath.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50"));
            return false;
        }
        catch
        {
            return false;
        }
        
    }

    private async Task UpdateLauncher()
    {
        try
        {
            const string url = "https://api.github.com/repos/D00MK1D/DDON-Launcher/releases/latest";
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("DDO_Launcher");

            var json = await http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            string tag = doc.RootElement.GetProperty("tag_name").GetString();

            tag = tag.TrimStart('v');

            string[] parts = tag.Split('.');
            while (parts.Length < 4) { tag += ".0"; parts = tag.Split('.'); }

            Version remoteVersion = new Version(tag);
            var localVersion = Assembly.GetExecutingAssembly().GetName().Version!;

            if (remoteVersion > localVersion)
            {
                btnUpdate.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Error while checking for updates:\n\n" + ex.Message,
                "Update Check",
                MessageBoxButton.OK);
        }
    }

    private async void btnUpdate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var uri = new Uri(
                "pack://application:,,,/Update/update_launcher.bat",
                UriKind.Absolute
            );

            using var stream = Application.GetResourceStream(uri)?.Stream;
            if (stream == null)
            {
                MessageBox.Show("Updater not found.");
                return;
            }

            string batPath = Path.Combine(AppContext.BaseDirectory, "update_launcher.bat");

            using (var file = File.Create(batPath))
                await stream.CopyToAsync(file);

            Process.Start(new ProcessStartInfo
            {
                FileName = batPath,
                WorkingDirectory = AppContext.BaseDirectory,
                UseShellExecute = true
            });

            await Task.Delay(300);
            Application.Current.Shutdown();
        }
        catch
        {
            MessageBox.Show("Error while updating.", "Update", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void cbRemember_Click(object sender, RoutedEventArgs e)
    {
        if (cbRemember.IsChecked != true)
        {
            Properties.Settings.Default.accountText = "";
            Properties.Settings.Default.passwordText = "";
            Properties.Settings.Default.rememberMe = false;
            Properties.Settings.Default.lastServerSelected = 0;

            Properties.Settings.Default.Save();
        }
    }
}