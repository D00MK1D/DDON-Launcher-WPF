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
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using static Arrowgene.Ddon.Client.GmdActions;

namespace DDO_Launcher;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    #region Not UI
    private CancellationTokenSource _serverChangeCts = new CancellationTokenSource();
    private CancellationTokenSource? _copyFeedbackCts;
    private const string LocalAccountName = "LocalPlayer";
    private const string LocalAccountPassword = "LocalPlayer";
    private const string LocalAccountEmail = "localplayer@localhost.local";
    private static readonly Server LocalServer = new("127.0.0.1", 52100, "127.0.0.1", 52099);

    private readonly ServerManager ServerManager;
    private readonly LocalServerReleaseInstaller _localServerReleaseInstaller;
    private bool _isLocalPlayMode;
    private CancellationTokenSource? _localServerStatusCts;
    private Process? _ownedLocalServerProcess;
    private readonly object _ownedLocalServerLogLock = new();
    private StringBuilder _ownedLocalServerLog = new();
    private string? _lastServerFailureLog;

    public MainWindow(ServerManager serverManager)
    {
        InitializeComponent();
        this.WindowStartupLocation = WindowStartupLocation.CenterScreen;

        ServerManager = serverManager;
        _localServerReleaseInstaller = new LocalServerReleaseInstaller(ServerManager.LocalServerReleaseUrl);
        UpdateServerList();
        DataContext = this;
        SetLocalPlayMode(Properties.Settings.Default.localPlayMode);

        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
        Application.Current.Exit += (_, _) => StopOwnedLocalServer();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {

        if (Properties.Settings.Default.rememberMe)
        {
            textAccount.Text = Properties.Settings.Default.accountText;
            textPassword.Password = Properties.Settings.Default.passwordText;
            cbRemember.IsChecked = Properties.Settings.Default.rememberMe;
            if (Properties.Settings.Default.lastServerSelected >= 0 &&
                Properties.Settings.Default.lastServerSelected < serverComboBox.Items.Count)
            {
                serverComboBox.SelectedIndex = Properties.Settings.Default.lastServerSelected;
            }
        }

    }

    public class ServerResponse
    {
        public string? Error { get; set; }
        public string? Message { get; set; }
        public string? Token { get; set; }
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

                            Properties.Settings.Default.localPlayMode = false;
                            Properties.Settings.Default.Save();
                        }

                        LaunchGame(ServerManager.Servers[ServerManager.SelectedServer], serverResponse.Token);

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

    private async Task<ServerResponse> SendAccountRequestAsync(Server server, string action, string account, string password)
    {
        using HttpClient client = new();
        var requestData = new
        {
            Action = action,
            Account = account,
            Password = password,
            Email = LocalAccountEmail
        };

        string jsonData = JsonSerializer.Serialize(requestData);
        using var content = new StringContent(jsonData, Encoding.UTF8, "application/json");
        client.DefaultRequestVersion = HttpVersion.Version11;

        HttpResponseMessage response = await client.PostAsync(
            $"http://{server.DLIP}:{server.DLPort}/api/account",
            content);

        string responseBody = await response.Content.ReadAsStringAsync();
        ServerResponse? serverResponse = JsonSerializer.Deserialize<ServerResponse>(responseBody);

        return serverResponse ?? throw new JsonException("Empty response from server.");
    }

    private async Task PlayLocalAsync()
    {
        _localServerStatusCts?.Cancel();
        _lastServerFailureLog = null;
        SetFormEnabled(false);
        object originalSubmitContent = btnSubmit.Content;

        try
        {
            ShowLocalPlayOperationStatus("Checking server", "Checking local server files...");
            await EnsureLocalServerReadyAsync();

            ShowLocalPlayOperationStatus("Signing in", "Signing in to the local server...");

            ServerResponse serverResponse = await SendAccountRequestAsync(
                LocalServer,
                "login",
                LocalAccountName,
                LocalAccountPassword);

            if (string.IsNullOrEmpty(serverResponse.Token))
            {
                await SendAccountRequestAsync(
                    LocalServer,
                    "create",
                    LocalAccountName,
                    LocalAccountPassword);

                serverResponse = await SendAccountRequestAsync(
                    LocalServer,
                    "login",
                    LocalAccountName,
                    LocalAccountPassword);
            }

            if (string.IsNullOrEmpty(serverResponse.Token))
            {
                StopOwnedLocalServer();
                new LocalPlayErrorWindow(this,
                    serverResponse.Error ?? serverResponse.Message ?? "Local login failed.",
                    _lastServerFailureLog).ShowDialog();
                return;
            }

            Properties.Settings.Default.localPlayMode = true;
            Properties.Settings.Default.Save();

            ShowLocalPlayOperationStatus("Launching game", "Starting Dragon's Dogma Online...");
            await WaitForLoginServerReadyAsync();
            Process? gameProcess = LaunchGame(LocalServer, serverResponse.Token);
            if (_ownedLocalServerProcess != null && gameProcess != null)
            {
                Hide();
                await gameProcess.WaitForExitAsync();
                StopOwnedLocalServer();
                Application.Current.Shutdown();
                return;
            }

            StopOwnedLocalServer();
            Application.Current.Shutdown();
        }
        catch (Win32Exception e) when (e.NativeErrorCode == 2)
        {
            StopOwnedLocalServer();
            new LocalPlayErrorWindow(this,
                "Launcher couldn't find DDO.exe!\nMake sure the launcher is located in the game folder.\n\n" + e.Message,
                _lastServerFailureLog).ShowDialog();
        }
        catch (Exception e)
        {
            StopOwnedLocalServer();
            new LocalPlayErrorWindow(this, e.Message, _lastServerFailureLog).ShowDialog();
        }
        finally
        {
            btnSubmit.Content = originalSubmitContent;
            if (_isLocalPlayMode)
            {
                localServerProgressOverlay.Visibility = Visibility.Hidden;
                if (IsVisible)
                {
                    _localServerStatusCts?.Dispose();
                    _localServerStatusCts = new CancellationTokenSource();
                    await RefreshLocalServerStatusAsync();
                }
            }

            SetFormEnabled(true);
        }
    }

    private Process? LaunchGame(Server server, string token)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "ddo.exe",
            Arguments = " addr=" + server.LobbyIP +
                        " port=" + server.LPort +
                        " token=" + token +
                        " DL=http://" + server.DLIP +
                        ":" + server.DLPort +
                        "/win/ LVer=03.04.003.20181115.0 RVer=3040008",
            UseShellExecute = false
        };

        psi.EnvironmentVariables["__COMPAT_LAYER"] = "RUNASINVOKER";
        return Process.Start(psi);
    }

    private async Task EnsureLocalServerReadyAsync()
    {
        bool localServerAlreadyRunning = await IsAnyLocalServerPortAliveAsync();
        string serverExe = await EnsureLocalServerInstalledAsync(
            allowUpdate: !localServerAlreadyRunning && ServerManager.LocalServerAutoUpdate);

        if (localServerAlreadyRunning)
        {
            await WaitForLocalServerReadyAsync(stopOwnedOnFailure: false);
            return;
        }

        ShowLocalPlayOperationStatus("Updating database", "Applying local server database updates...");
        await RunLocalDatabaseMigrationAsync(serverExe);

        ShowLocalPlayOperationStatus("Starting server", "Starting the local server...");
        StartLocalServer(serverExe);

        await WaitForLocalServerReadyAsync(stopOwnedOnFailure: true);
    }

    private async Task WaitForLocalServerReadyAsync(bool stopOwnedOnFailure)
    {
        int timeoutSeconds = ServerManager.LocalServerStartupTimeoutSeconds;
        ShowLocalPlayOperationStatus(
            "Waiting for server",
            $"Waiting for the local server to finish starting... ({timeoutSeconds}s timeout)");

        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(timeoutSeconds));
        while (!timeout.IsCancellationRequested)
        {
            await Task.Delay(1000, timeout.Token).ContinueWith(_ => { });
            if (stopOwnedOnFailure && _ownedLocalServerProcess?.HasExited == true)
            {
                string serverLog = GetOwnedLocalServerLog();
                _lastServerFailureLog = serverLog;
                StopOwnedLocalServer();
                throw new InvalidOperationException(
                    "The local server exited before it finished starting.\n" +
                    GetLastLogLines(serverLog));
            }

            if (await IsLocalServerReadyAsync())
                return;
        }

        if (stopOwnedOnFailure)
        {
            string startupLog = GetOwnedLocalServerLog();
            _lastServerFailureLog = startupLog;
            StopOwnedLocalServer();

            throw new TimeoutException(
                $"The local server did not finish starting within {timeoutSeconds} seconds.\n" +
                GetLastLogLines(startupLog));
        }

        throw new TimeoutException(
            $"A local server port is already in use, but the local server did not become fully ready within {timeoutSeconds} seconds.");
    }

    private async Task<string> EnsureLocalServerInstalledAsync(bool allowUpdate)
    {
        LocalServerReleaseStatus status = await _localServerReleaseInstaller.GetStatusAsync(CancellationToken.None);
        if (!status.IsInstalled)
        {
            if (!string.IsNullOrWhiteSpace(status.LatestCheckError))
                throw new InvalidOperationException(
                    "The local server is not installed, and the launcher could not check GitHub for the latest server release.\n" +
                    status.LatestCheckError);

            await InstallLatestLocalServerReleaseAsync("Downloading...");
        }
        else if (status.UpdateAvailable && allowUpdate)
        {
            await InstallLatestLocalServerReleaseAsync("Updating...");
        }

        return _localServerReleaseInstaller.FindInstalledServerExe() ??
            throw new FileNotFoundException("The local server could not be found after installation.");
    }

    private async Task RunLocalDatabaseMigrationAsync(string serverExe)
    {
        string serverDirectory = Path.GetDirectoryName(serverExe)!;
        BackupLocalDatabase(serverDirectory);

        var psi = CreateLocalServerProcessStartInfo(
            serverExe,
            redirectStandardInput: false,
            redirectOutput: true,
            "dbmigration");

        using Process process = new() { StartInfo = psi };

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("The local database updater could not be started.");
            }
        }
        catch (Win32Exception ex) when (ShouldRunLocalServerWithDotnet(serverExe) && ex.NativeErrorCode == 2)
        {
            throw new InvalidOperationException(
                "The downloaded local server release requires the .NET runtime, but dotnet could not be found. " +
                "Use a self-contained server release or install the .NET 10 runtime.",
                ex);
        }

        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();

        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(90));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            TryKillProcess(process);
            throw new TimeoutException("The local database update did not finish in time.");
        }

        string output = await outputTask;
        string error = await errorTask;
        string migrationLog = (output + Environment.NewLine + error).Trim();

        if (process.ExitCode != 0 || migrationLog.Contains("Failed to migrate the database.", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The local database update failed.\n" + GetLastLogLines(migrationLog));
        }
    }

    private static void BackupLocalDatabase(string serverDirectory)
    {
        string databasePath = Path.Combine(serverDirectory, "Files", "Database", "db.sqlite");
        if (!File.Exists(databasePath))
            return;

        string backupDirectory = Path.Combine(serverDirectory, "Files", "Database", "Backups");
        Directory.CreateDirectory(backupDirectory);

        string backupPath = Path.Combine(
            backupDirectory,
            $"db-{DateTime.Now:yyyyMMdd-HHmmss}.sqlite");

        File.Copy(databasePath, backupPath, overwrite: false);
    }

    private static string GetLastLogLines(string log)
    {
        if (string.IsNullOrWhiteSpace(log))
            return "No details were reported by the server.";

        string[] lines = log
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .TakeLast(8)
            .ToArray();

        return string.Join(Environment.NewLine, lines);
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
        }
    }

    private void StartLocalServer(string serverExe)
    {
        var psi = CreateLocalServerProcessStartInfo(
            serverExe,
            redirectStandardInput: true,
            redirectOutput: true,
            "server",
            "start");

        var process = new Process
        {
            StartInfo = psi,
            EnableRaisingEvents = true
        };

        ResetOwnedLocalServerLog();
        process.OutputDataReceived += (_, e) => AppendOwnedLocalServerLog(e.Data);
        process.ErrorDataReceived += (_, e) => AppendOwnedLocalServerLog(e.Data);

        try
        {
            if (!process.Start())
            {
                process.Dispose();
                throw new InvalidOperationException("The local server process could not be started.");
            }

            _ownedLocalServerProcess = process;
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
        catch (Win32Exception ex) when (ShouldRunLocalServerWithDotnet(serverExe) && ex.NativeErrorCode == 2)
        {
            process.Dispose();
            throw new InvalidOperationException(
                "The downloaded local server release requires the .NET runtime, but dotnet could not be found. " +
                "Use a self-contained server release or install the .NET 10 runtime.",
                ex);
        }
        catch
        {
            process.Dispose();
            throw;
        }
    }

    private static ProcessStartInfo CreateLocalServerProcessStartInfo(
        string serverExe,
        bool redirectStandardInput,
        bool redirectOutput,
        params string[] arguments)
    {
        string serverDirectory = Path.GetDirectoryName(serverExe)!;
        bool runWithDotnet = ShouldRunLocalServerWithDotnet(serverExe);

        var psi = new ProcessStartInfo
        {
            FileName = runWithDotnet ? "dotnet" : serverExe,
            WorkingDirectory = serverDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = redirectStandardInput,
            RedirectStandardOutput = redirectOutput,
            RedirectStandardError = redirectOutput
        };

        if (runWithDotnet)
            psi.ArgumentList.Add(Path.Combine(serverDirectory, "Arrowgene.Ddon.Cli.dll"));

        foreach (string argument in arguments)
            psi.ArgumentList.Add(argument);

        return psi;
    }

    private static bool ShouldRunLocalServerWithDotnet(string serverExe)
    {
        string serverDirectory = Path.GetDirectoryName(serverExe)!;
        return !File.Exists(Path.Combine(serverDirectory, "coreclr.dll")) &&
            File.Exists(Path.Combine(serverDirectory, "Arrowgene.Ddon.Cli.dll"));
    }

    private void ResetOwnedLocalServerLog()
    {
        lock (_ownedLocalServerLogLock)
        {
            _ownedLocalServerLog = new StringBuilder();
        }
    }

    private void AppendOwnedLocalServerLog(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        lock (_ownedLocalServerLogLock)
        {
            _ownedLocalServerLog.AppendLine(line);
            if (_ownedLocalServerLog.Length > 20000)
                _ownedLocalServerLog.Remove(0, _ownedLocalServerLog.Length - 20000);
        }
    }

    private string GetOwnedLocalServerLog()
    {
        lock (_ownedLocalServerLogLock)
        {
            return _ownedLocalServerLog.ToString();
        }
    }

    private void StopOwnedLocalServer()
    {
        Process? process = _ownedLocalServerProcess;
        _ownedLocalServerProcess = null;

        if (process == null)
            return;

        try
        {
            if (!process.HasExited)
            {
                try
                {
                    process.StandardInput.WriteLine("e");
                    process.StandardInput.Flush();
                }
                catch
                {
                }

                if (!process.WaitForExit(10000))
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                }
            }
        }
        catch
        {
        }
        finally
        {
            try
            {
                process.CancelOutputRead();
            }
            catch
            {
            }

            try
            {
                process.CancelErrorRead();
            }
            catch
            {
            }

            process.Dispose();
        }
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        StopOwnedLocalServer();
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

    public static int ShowDropdownDialog(string message, string title, string[] options)
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

        var cancelButton = new Button { Content = "Cancel", Width = 60, Margin = new Thickness(5) };
        var okButton = new Button { Content = "OK", Width = 60, Margin = new Thickness(5) };


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

    public async Task<bool> TranslationUpdateVerify(string url)
    {
        try
        {
            if (Properties.Settings.Default.firstInstalledTranslation != true)
            {
                btnTranslationPath.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50"));
                btnTranslation.ToolTip = "Translation update available. Click to apply.";
                return false;
            }

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
            btnTranslation.ToolTip = "Translation update available. Click to apply.";
            return false;
        }
        catch
        {
            return false;
        }

    }

    public async Task UpdateLauncher()
    {
        try
        {
            const string url = "https://api.github.com/repos/D00MK1D/DDON-Launcher-WPF/releases/latest";
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
        catch
        {
        }
    }

    private static async Task<bool> IsHostAlive(string url)
    {
        using var cts = new CancellationTokenSource(2000);
        using var client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(2000) };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cts.Token
            );

            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> IsTcpPortAlive(string host, int port)
    {
        using var client = new TcpClient();
        using var cts = new CancellationTokenSource(2000);

        try
        {
            await client.ConnectAsync(host, port, cts.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static Task<bool> IsLocalServerReadyAsync()
    {
        return IsHostAlive($"http://{LocalServer.DLIP}:{LocalServer.DLPort}");
    }

    private async Task WaitForLoginServerReadyAsync()
    {
        int timeoutSeconds = ServerManager.LocalServerStartupTimeoutSeconds;
        ShowLocalPlayOperationStatus(
            "Waiting for game server",
            $"Waiting for the game server to finish starting... ({timeoutSeconds}s timeout)");

        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(timeoutSeconds));
        while (!timeout.IsCancellationRequested)
        {
            if (await IsTcpPortAlive(LocalServer.LobbyIP, LocalServer.LPort))
                return;

            if (_ownedLocalServerProcess?.HasExited == true)
            {
                string serverLog = GetOwnedLocalServerLog();
                _lastServerFailureLog = serverLog;
                StopOwnedLocalServer();
                throw new InvalidOperationException(
                    "The local server exited before the game server finished starting.\n" +
                    GetLastLogLines(serverLog));
            }

            await Task.Delay(1000, timeout.Token).ContinueWith(_ => { });
        }

        if (_ownedLocalServerProcess != null)
        {
            string startupLog = GetOwnedLocalServerLog();
            _lastServerFailureLog = startupLog;
            StopOwnedLocalServer();
            throw new TimeoutException(
                $"The game server did not finish starting within {timeoutSeconds} seconds.\n" +
                GetLastLogLines(startupLog));
        }

        throw new TimeoutException(
            $"The game server did not become ready within {timeoutSeconds} seconds.");
    }

    private static async Task<bool> IsAnyLocalServerPortAliveAsync()
    {
        if (await IsHostAlive($"http://{LocalServer.DLIP}:{LocalServer.DLPort}"))
            return true;

        return await IsTcpPortAlive(LocalServer.LobbyIP, LocalServer.LPort);
    }

    #endregion

    #region UI Content

    private async void btnSubmit_Click(object sender, RoutedEventArgs e)
    {
        if (_isLocalPlayMode)
        {
            await PlayLocalAsync();
            return;
        }

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

    private void btnModeToggle_Click(object sender, RoutedEventArgs e)
    {
        SetLocalPlayMode(!_isLocalPlayMode);
        Properties.Settings.Default.localPlayMode = _isLocalPlayMode;
        Properties.Settings.Default.Save();
    }

    private async void btnCopyLocalServer_Click(object sender, RoutedEventArgs e)
    {
        string? address = textLocalServerAddress.Text;
        if (!string.IsNullOrWhiteSpace(address))
        {
            Clipboard.SetText(address);
            await ShowCopyFeedbackAsync();
        }
    }

    private async Task ShowCopyFeedbackAsync()
    {
        _copyFeedbackCts?.Cancel();
        _copyFeedbackCts?.Dispose();
        _copyFeedbackCts = new CancellationTokenSource();

        CancellationToken token = _copyFeedbackCts.Token;
        Color normalColor = (Color)ColorConverter.ConvertFromString("#FFD87A");
        Color copiedColor = (Color)ColorConverter.ConvertFromString("#8FE388");

        btnCopyLocalServer.ToolTip = "Copied";
        btnCopyLocalServer.Foreground = new SolidColorBrush(normalColor);
        btnCopyLocalServer.RenderTransformOrigin = new Point(0.5, 0.5);
        btnCopyLocalServer.RenderTransform = new ScaleTransform(1, 1);

        var flash = new ColorAnimation(copiedColor, TimeSpan.FromMilliseconds(120))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        var pulse = new DoubleAnimation(1.12, TimeSpan.FromMilliseconds(120))
        {
            AutoReverse = true,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        ((SolidColorBrush)btnCopyLocalServer.Foreground).BeginAnimation(SolidColorBrush.ColorProperty, flash);
        ((ScaleTransform)btnCopyLocalServer.RenderTransform).BeginAnimation(ScaleTransform.ScaleXProperty, pulse);
        ((ScaleTransform)btnCopyLocalServer.RenderTransform).BeginAnimation(ScaleTransform.ScaleYProperty, pulse);

        try
        {
            await Task.Delay(1200, token);
            var fadeBack = new ColorAnimation(normalColor, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };

            ((SolidColorBrush)btnCopyLocalServer.Foreground).BeginAnimation(SolidColorBrush.ColorProperty, fadeBack);
            btnCopyLocalServer.ToolTip = "Copy local network address";
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async void serverComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLocalPlayMode)
            return;

        _serverChangeCts?.Cancel();
        _serverChangeCts?.Dispose();
        _serverChangeCts = new CancellationTokenSource();

        var token = _serverChangeCts.Token;

        if (serverComboBox.SelectedItem is not string serverName)
            return;

        if (!ServerManager.Servers.TryGetValue(serverName, out var server))
            return;

        ServerManager.SelectServer(serverName);

        serverComboBox.IsEnabled = false;
        try
        {
            if (await IsHostAlive($"http://{server.DLIP}:{server.DLPort}"))
            {
                BitmapImage background = await GetCustomImageAsync(server, "launcher/background.png", token);
                if (IsStaleServerContentLoad(token))
                    return;
                imgBackground.ImageSource = background;

                BitmapImage logo = await GetCustomImageAsync(server, "launcher/logo.png", token);
                if (IsStaleServerContentLoad(token))
                    return;
                imgLogo.Source = logo;

                BitmapImage newsBanner = await GetCustomImageAsync(server, "news/newsbanner.png", token);
                if (IsStaleServerContentLoad(token))
                    return;
                imgNewsBanner.Source = newsBanner;

                await UpdateNewsAsync(token);

                serverComboBox.IsEnabled = true;
            }
            else
            {
                if (IsStaleServerContentLoad(token))
                    return;

                imgBackground.ImageSource = new BitmapImage(new Uri("pack://application:,,,/Images/launcher/background.png"));
                imgLogo.Source = new BitmapImage(new Uri("pack://application:,,,/Images/launcher/logo.png"));
                imgNewsBanner.Source = new BitmapImage(new Uri("pack://application:,,,/Images/news/newsbanner.png"));
                ApplyDefaultNews();

                serverComboBox.IsEnabled = true;
            }

        }
        catch (OperationCanceledException)
        {
            serverComboBox.IsEnabled = true;
        }
        catch (Exception ex)
        {
            serverComboBox.IsEnabled = true;

            MessageBox.Show(
                ex.Message,
                "Dragon's Dogma Online",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void btnChangeAction_Click(object sender, RoutedEventArgs e)
    {
        if (_isLocalPlayMode)
            return;

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

        else
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
            string url = string.Empty;
            string[] labels = { "English", "日本語", "Português (Brasil)", "Español", "中文（繁體）", "Tiếng Việt", "Custom" };
            string[] languages = { "English", "Japanese" };
            int selectedLanguageIndex = ShowDropdownDialog("Select language", "Translation Patch", labels);
            
            switch (selectedLanguageIndex)
            {
                case -1:
                    return;

                case 0:
                    ServerManager.SetTranslationPatchUrl("https://raw.githubusercontent.com/Sapphiratelaemara/DDON-translation/refs/heads/main/English/gmd.csv");
                    break;
                case 1:
                    ServerManager.SetTranslationPatchUrl("https://raw.githubusercontent.com/Sapphiratelaemara/DDON-translation/refs/heads/main/gmd.csv");
                    break;
                case 2:
                    ServerManager.SetTranslationPatchUrl("https://raw.githubusercontent.com/Sapphiratelaemara/DDON-translation/refs/heads/main/Portuguese%20(Brazil)/gmd.csv");
                    break;
                case 3:
                    ServerManager.SetTranslationPatchUrl("https://raw.githubusercontent.com/Sapphiratelaemara/DDON-translation/refs/heads/main/Spanish/gmd.csv");
                    break;
                case 4:
                    ServerManager.SetTranslationPatchUrl("https://raw.githubusercontent.com/Sapphiratelaemara/DDON-translation/refs/heads/main/Traditional%20Chinese/gmd.csv");
                    break;
                case 5:
                    ServerManager.SetTranslationPatchUrl("https://raw.githubusercontent.com/Sapphiratelaemara/DDON-translation/refs/heads/main/Viet/gmd.csv");
                    break;
                case 6:
                    var result = ShowInputDialog("Link to CSV file:", "Custom Translation Patch", out url, ServerManager.TranslationPatchUrl);
                    if (!result)
                        return;
                    ServerManager.SetTranslationPatchUrl(url);
                    break;

            }

            waitWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            waitWindow.Show();

            (waitWindow.Content as StackPanel)!.Children.OfType<TextBlock>().First().Text = "Checking for translation patch updates...";

            var update = await TranslationUpdateVerify(ServerManager.TranslationPatchUrl);

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

            var patchDownload = await client.GetAsync(ServerManager.TranslationPatchUrl);
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
                if (selectedLanguageIndex == 1)
                {
                    await Task.Run(() =>
                        GmdActions.Pack(gmdCsvEntries, "nativePC/rom", languages[selectedLanguageIndex], progress));
                }
                else
                {
                    await Task.Run(() =>
                        GmdActions.Pack(gmdCsvEntries, "nativePC/rom", languages[0], progress));
                }
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

    private void DragWindow_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            this.DragMove();
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

    private void SetLocalPlayMode(bool enabled)
    {
        _isLocalPlayMode = enabled;

        _serverChangeCts?.Cancel();
        _serverChangeCts?.Dispose();
        _serverChangeCts = new CancellationTokenSource();

        _localServerStatusCts?.Cancel();
        _localServerStatusCts?.Dispose();
        _localServerStatusCts = enabled ? new CancellationTokenSource() : null;

        labelAccount.Visibility = enabled ? Visibility.Hidden : Visibility.Visible;
        textAccount.Visibility = enabled ? Visibility.Hidden : Visibility.Visible;
        passLabel.Visibility = enabled ? Visibility.Hidden : Visibility.Visible;
        textPassword.Visibility = enabled ? Visibility.Hidden : Visibility.Visible;
        labelServer.Visibility = enabled ? Visibility.Hidden : Visibility.Visible;
        serverComboBox.Visibility = enabled ? Visibility.Hidden : Visibility.Visible;
        btnServerSettings.Visibility = enabled ? Visibility.Hidden : Visibility.Visible;
        labelRemember.Visibility = enabled ? Visibility.Hidden : Visibility.Visible;
        cbRemember.Visibility = enabled ? Visibility.Hidden : Visibility.Visible;
        labelEmail.Visibility = Visibility.Hidden;
        textEmail.Visibility = Visibility.Hidden;
        btnChangeAction.Visibility = enabled ? Visibility.Hidden : Visibility.Visible;
        labelLocalPlay.Visibility = enabled ? Visibility.Visible : Visibility.Hidden;
        localServerAddressPanel.Visibility = enabled ? Visibility.Visible : Visibility.Hidden;
        localServerStatusPanel.Visibility = enabled ? Visibility.Visible : Visibility.Hidden;
        localServerProgressOverlay.Visibility = Visibility.Hidden;
        btnDownloadLocalServer.Visibility = Visibility.Collapsed;
        textLocalServerAddress.Text = GetLocalNetworkAddress();
        localServerAddressPanel.ToolTip = "Other players on your network can use this address with ports 52100 and 52099.";

        btnSubmit.Content = enabled ? "Play" : "Login";
        btnSubmit.Margin = enabled ? new Thickness(10, 174, 10, 0) : new Thickness(10, 207, 10, 0);
        btnModeToggle.Content = enabled ? "Online Login" : "Local Play";
        btnModeToggle.Margin = enabled ? new Thickness(10, 259, 10, 0) : new Thickness(140, 259, 10, 0);

        if (enabled)
        {
            ApplyLocalLauncherContent();
            _ = RefreshLocalServerStatusAsync();
        }
        else if (serverComboBox.SelectedItem is not null)
        {
            serverComboBox_SelectionChanged(serverComboBox, null!);
        }
    }

    private void SetFormEnabled(bool enabled)
    {
        btnSubmit.IsEnabled = enabled;
        btnChangeAction.IsEnabled = enabled;
        btnModeToggle.IsEnabled = enabled;
        serverComboBox.IsEnabled = enabled;
        btnServerSettings.IsEnabled = enabled;
        btnCopyLocalServer.IsEnabled = enabled;
        btnDownloadLocalServer.IsEnabled = enabled;
    }

    private async Task RefreshLocalServerStatusAsync()
    {
        CancellationToken token = _localServerStatusCts?.Token ?? CancellationToken.None;
        if (token.IsCancellationRequested)
            return;

        textLocalServerStatus.Text = "Checking server...";
        textLocalServerStatus.Foreground = BrushFromHex("#CFCFCF");
        textLocalServerStatus.ToolTip = null;
        localServerStatusDot.Fill = BrushFromHex("#CFCFCF");
        localServerProgressOverlay.Visibility = Visibility.Hidden;
        btnDownloadLocalServer.Visibility = Visibility.Collapsed;

        LocalServerReleaseStatus status = await _localServerReleaseInstaller.GetStatusAsync(token);
        if (token.IsCancellationRequested || !_isLocalPlayMode)
            return;

        ApplyLocalServerStatus(status);
    }

    private void ApplyLocalServerStatus(LocalServerReleaseStatus status)
    {
        if (!string.IsNullOrWhiteSpace(status.LatestCheckError))
        {
            textLocalServerStatus.Text = status.IsInstalled ? "Server ready" : "Server not installed";
            textLocalServerStatus.Foreground = BrushFromHex("#D3D3D3");
            localServerStatusDot.Fill = BrushFromHex(status.IsInstalled ? "#8FE388" : "#FFD87A");
            textLocalServerStatus.ToolTip = status.LatestCheckError;
            btnDownloadLocalServer.Visibility = status.IsInstalled ? Visibility.Collapsed : Visibility.Visible;
            btnDownloadLocalServer.ToolTip = "Retry local server download";
            btnDownloadLocalServer.Foreground = BrushFromHex("#FFD87A");
            localServerProgressOverlay.Visibility = Visibility.Hidden;
            return;
        }

        textLocalServerStatus.ToolTip = null;

        if (!status.IsInstalled)
        {
            textLocalServerStatus.Text = "Server not installed";
            textLocalServerStatus.Foreground = BrushFromHex("#D3D3D3");
            localServerStatusDot.Fill = BrushFromHex("#FFD87A");
            textLocalServerStatus.ToolTip = GetLocalServerStatusTooltip(status);
            btnDownloadLocalServer.Visibility = Visibility.Visible;
            btnDownloadLocalServer.ToolTip = "Download local server";
            btnDownloadLocalServer.Foreground = BrushFromHex("#FFD87A");
            localServerProgressOverlay.Visibility = Visibility.Hidden;
            return;
        }

        if (status.UpdateAvailable)
        {
            textLocalServerStatus.Text = "Update available";
            textLocalServerStatus.Foreground = BrushFromHex("#D3D3D3");
            localServerStatusDot.Fill = BrushFromHex("#FFD87A");
            textLocalServerStatus.ToolTip = GetLocalServerStatusTooltip(status);
            btnDownloadLocalServer.Visibility = Visibility.Visible;
            btnDownloadLocalServer.ToolTip = "Update local server";
            btnDownloadLocalServer.Foreground = BrushFromHex("#FFD87A");
            localServerProgressOverlay.Visibility = Visibility.Hidden;
            return;
        }

        textLocalServerStatus.Text = "Server ready";
        textLocalServerStatus.Foreground = BrushFromHex("#D3D3D3");
        localServerStatusDot.Fill = BrushFromHex("#8FE388");
        textLocalServerStatus.ToolTip = GetLocalServerStatusTooltip(status);
        localServerProgressOverlay.Visibility = Visibility.Hidden;
        btnDownloadLocalServer.Visibility = Visibility.Collapsed;
    }

    private static Brush BrushFromHex(string color)
    {
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    }

    private void ShowLocalPlayOperationStatus(string status, string detail)
    {
        btnSubmit.Content = CreateLoadingDots();
        textLocalServerStatus.Text = status;
        textLocalServerStatus.Foreground = BrushFromHex("#D3D3D3");
        localServerStatusDot.Fill = BrushFromHex("#FFD87A");
        btnDownloadLocalServer.Visibility = Visibility.Collapsed;
        localServerProgressOverlay.Visibility = Visibility.Visible;
        localServerProgressBar.IsIndeterminate = true;
        textLocalServerInstallProgress.Text = detail;
    }

    private static UIElement CreateLoadingDots()
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        for (int i = 0; i < 3; i++)
        {
            var dot = new System.Windows.Shapes.Ellipse
            {
                Width = 9,
                Height = 9,
                Fill = new SolidColorBrush(Color.FromRgb(0x1A, 0x10, 0x00)),
                Margin = new Thickness(5, 0, 5, 0),
                Opacity = 0.2,
                VerticalAlignment = VerticalAlignment.Center
            };

            dot.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation
            {
                From = 0.2,
                To = 1.0,
                Duration = TimeSpan.FromSeconds(0.45),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                BeginTime = TimeSpan.FromSeconds(i * 0.18)
            });

            panel.Children.Add(dot);
        }

        return panel;
    }

    private static string GetLocalServerStatusTooltip(LocalServerReleaseStatus status)
    {
        string installed = status.Installed?.ReleaseTag ?? "none";
        string latest = status.LatestReleaseTag ?? "unknown";

        return $"Installed: {installed}\nLatest: {latest}";
    }

    private async void btnDownloadLocalServer_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await InstallLatestLocalServerReleaseAsync("Downloading...");
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "The local server could not be downloaded.\n" + ex.Message,
                "Dragon's Dogma Online",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            await RefreshLocalServerStatusAsync();
        }
    }

    private async Task InstallLatestLocalServerReleaseAsync(string initialStatus)
    {
        bool restoreFormEnabled = btnSubmit.IsEnabled;
        SetFormEnabled(false);
        object originalSubmitContent = btnSubmit.Content;
        object originalDownloadTooltip = btnDownloadLocalServer.ToolTip;

        try
        {
            btnSubmit.Content = initialStatus;
            btnDownloadLocalServer.Visibility = Visibility.Collapsed;
            textLocalServerStatus.Text = "Setting up server";
            textLocalServerStatus.Foreground = BrushFromHex("#D3D3D3");
            localServerStatusDot.Fill = BrushFromHex("#FFD87A");
            textLocalServerStatus.ToolTip = null;
            localServerProgressOverlay.Visibility = Visibility.Visible;
            localServerProgressBar.IsIndeterminate = true;
            localServerProgressBar.Value = 0;
            textLocalServerInstallProgress.Text = initialStatus;

            var progress = new Progress<LocalServerInstallProgress>(installProgress =>
            {
                textLocalServerInstallProgress.Text = installProgress.Message;

                if (installProgress.Percent.HasValue)
                {
                    localServerProgressBar.IsIndeterminate = false;
                    localServerProgressBar.Value = installProgress.Percent.Value;
                }
                else
                {
                    localServerProgressBar.IsIndeterminate = true;
                }

                btnSubmit.Content = installProgress.IsComplete ? "Play" : initialStatus;
            });

            await _localServerReleaseInstaller.InstallLatestAsync(progress, CancellationToken.None);
            await RefreshLocalServerStatusAsync();
        }
        finally
        {
            btnSubmit.Content = originalSubmitContent;
            btnDownloadLocalServer.ToolTip = originalDownloadTooltip;
            if (restoreFormEnabled)
                SetFormEnabled(true);
        }
    }

    #endregion

    #region UI Helpers


    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private async Task UpdateNewsAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        string newsUrl =
            $"http://{ServerManager.Servers[ServerManager.SelectedServer].DLIP}:" +
            $"{ServerManager.Servers[ServerManager.SelectedServer].DLPort}/news/news.html";

        try
        {
            string raw = await new HttpClient().GetStringAsync(newsUrl, token);
            if (IsStaleServerContentLoad(token))
                return;

            string wrappedXml = $"<news>{raw}</news>";

            var doc = System.Xml.Linq.XDocument.Parse(wrappedXml);

            string type = doc.Root.Element("type")?.Value.TrimEnd().TrimStart() ?? "NO EVENT?";
            string date = doc.Root.Element("date")?.Value.TrimEnd().TrimStart() ?? "No Date?";
            string title = doc.Root.Element("title")?.Value.TrimEnd().TrimStart() ?? "No Title?";
            string content = doc.Root.Element("content")?.Value.TrimEnd().TrimStart() ?? "No Description?";

            textAnnouncementType.Text = type;
            textAnnouncementDate.Text = date;
            textAnnoucementTitle.Text = title;
            newsScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            textAnnoucementContent.Text = content.Trim();

            colorAnnouncementTypeBg.Background =
                new SolidColorBrush(GetAnnouncementColor(type));
        }
        catch
        {
            if (IsStaleServerContentLoad(token))
                return;

            ApplyDefaultNews();
        }
    }

    private static Color GetAnnouncementColor(string type)
    {
        return type?.Trim().ToUpperInvariant() switch
        {
            "UNAVAILABILITY" => (Color)ColorConverter.ConvertFromString("#B1224A"),
            "MAINTENANCE" => (Color)ColorConverter.ConvertFromString("#845E14"),
            "UPDATE" => (Color)ColorConverter.ConvertFromString("#615D9F"),
            "INFORMATION" => (Color)ColorConverter.ConvertFromString("#247CAA"),
            "EVENT" => (Color)ColorConverter.ConvertFromString("#348B3A"),
            _ => (Color)ColorConverter.ConvertFromString("#348B3A"),
        };
    }

    private void ApplyDefaultNews()
    {
        textAnnoucementTitle.Text = "No Title?";
        textAnnouncementType.Text = "NO EVENT?";
        textAnnouncementDate.Text = "No Date?";
        newsScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        textAnnoucementContent.Text = "No Description?";

        colorAnnouncementTypeBg.Background =
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#348B3A"));
    }

    private void ApplyLocalLauncherContent()
    {
        imgBackground.ImageSource = new BitmapImage(new Uri("pack://application:,,,/Images/launcher/background.png"));
        imgLogo.Source = new BitmapImage(new Uri("pack://application:,,,/Images/launcher/logo.png"));
        imgNewsBanner.Source = GetLocalNewsBannerImage();

        textAnnoucementTitle.Text = "Local Play";
        textAnnouncementType.Text = "LOCAL";
        textAnnouncementDate.Text = DateTime.Today.ToShortDateString();
        newsScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
        textAnnoucementContent.Inlines.Clear();
        textAnnoucementContent.Inlines.Add(new Run("Press Play to install or update the local server and start DDO. Share the address on the left for LAN play."));
        textAnnoucementContent.Inlines.Add(new LineBreak());
        textAnnoucementContent.Inlines.Add(new LineBreak());
        AddLocalPlayLink("DDON Reference", "https://reference.dd-on.com/");
        textAnnoucementContent.Inlines.Add(new Run("  |  "));
        AddLocalPlayLink("DDOn-Tools", "https://github.com/alborrajo/DDOn-Tools/releases");
        textAnnoucementContent.Inlines.Add(new Run("  |  "));
        AddLocalPlayLink("Arrowgene.DragonsDogmaOnline", "https://github.com/sebastian-heinz/Arrowgene.DragonsDogmaOnline");

        colorAnnouncementTypeBg.Background =
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#348B3A"));
    }

    private void AddLocalPlayLink(string text, string url)
    {
        var hyperlink = new Hyperlink(new Run(text))
        {
            NavigateUri = new Uri(url),
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFD87A"))
        };

        hyperlink.RequestNavigate += Hyperlink_RequestNavigate;
        textAnnoucementContent.Inlines.Add(hyperlink);
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri)
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Could not open link.\n" + ex.Message,
                "Dragon's Dogma Online",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        e.Handled = true;
    }

    private static BitmapImage GetLocalNewsBannerImage()
    {
        string localBannerPath = Path.Combine(AppContext.BaseDirectory, "Images", "news", "localnewsbanner.png");
        string resourcePath = File.Exists(localBannerPath)
            ? "pack://siteoforigin:,,,/Images/news/localnewsbanner.png"
            : "pack://application:,,,/Images/news/localnewsbanner.png";

        try
        {
            return new BitmapImage(new Uri(resourcePath));
        }
        catch
        {
            return new BitmapImage(new Uri("pack://application:,,,/Images/news/newsbanner.png"));
        }
    }

    private static string GetLocalNetworkAddress()
    {
        try
        {
            foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus != OperationalStatus.Up ||
                    networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    networkInterface.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                {
                    continue;
                }

                IPInterfaceProperties properties = networkInterface.GetIPProperties();
                if (properties.GatewayAddresses.Count == 0)
                    continue;

                foreach (UnicastIPAddressInformation address in properties.UnicastAddresses)
                {
                    if (address.Address.AddressFamily != AddressFamily.InterNetwork)
                        continue;

                    string value = address.Address.ToString();
                    if (IPAddress.IsLoopback(address.Address) || value.StartsWith("169.254.", StringComparison.Ordinal))
                        continue;

                    return value;
                }
            }
        }
        catch
        {
        }

        return LocalServer.DLIP;
    }

    private async Task<BitmapImage> GetCustomImageAsync(Server server, string imageName, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        string imageUrl = $"http://{server.DLIP}:{server.DLPort}/{imageName}";

        using HttpClient client = new();
        using HttpRequestMessage request = new(HttpMethod.Head, imageUrl);
        using HttpResponseMessage response = await client.SendAsync(request, token);
        token.ThrowIfCancellationRequested();

        if (response.StatusCode != HttpStatusCode.OK)
        {
            return new BitmapImage(new Uri($"pack://application:,,,/Images/{imageName}"));
        }

        return new BitmapImage(new Uri(imageUrl));
    }

    private bool IsStaleServerContentLoad(CancellationToken token)
    {
        return _isLocalPlayMode || token.IsCancellationRequested;
    }

    #endregion
}
