#nullable enable

using System.Windows;
using System.Windows.Controls;

namespace DDO_Launcher
{
    public partial class ServerSettingsWindow : Window
    {
        private readonly ServerManager _serverManager;

        public ServerSettingsWindow(ServerManager serverManager)
        {
            InitializeComponent();
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            _serverManager = serverManager;

            if (_serverManager.Servers.Count == 0)
                AddNewServer();
            else
                UpdateServerList();
        }

        private void AddNewServer()
        {
            int i = 1;
            string newServerName;

            do
            {
                newServerName = $"Server #{_serverManager.Servers.Count + i}";
                i++;
            }
            while (_serverManager.Servers.ContainsKey(newServerName));

            var server = new Server("localhost", 52100, "localhost", 52099);
            _serverManager.AddServer(newServerName, server);

            UpdateServerList();
            serverComboBox.SelectedItem = newServerName;
        }

        private void UpdateServerList()
        {
            var selected = serverComboBox.SelectedItem as string;

            serverComboBox.Items.Clear();
            foreach (var name in _serverManager.Servers.Keys)
                serverComboBox.Items.Add(name);

            if (selected != null && _serverManager.Servers.ContainsKey(selected))
                serverComboBox.SelectedItem = selected;
            else if (serverComboBox.Items.Count > 0)
                serverComboBox.SelectedIndex = 0;

            UpdateServerFields();
        }

        private void UpdateServerFields()
        {
            if (serverComboBox.SelectedItem is not string serverName ||
                !_serverManager.Servers.TryGetValue(serverName, out var server))
            {
                SetFieldsEnabled(false);
                ClearFields();
                return;
            }

            SetFieldsEnabled(true);

            textSmServerName.Text = serverName;
            textSmLobbyIP.Text = server.LobbyIP;
            textSmLobbyPort.Text = server.LPort.ToString();
            textSmDownloadIP.Text = server.DLIP;
            textSmDownloadPort.Text = server.DLPort.ToString();
        }

        private void SetFieldsEnabled(bool enabled)
        {
            textSmServerName.IsEnabled = enabled;
            textSmLobbyIP.IsEnabled = enabled;
            textSmLobbyPort.IsEnabled = enabled;
            textSmDownloadIP.IsEnabled = enabled;
            textSmDownloadPort.IsEnabled = enabled;
            btnSmRemove.IsEnabled = enabled;
        }

        private void ClearFields()
        {
            textSmServerName.Text = "";
            textSmLobbyIP.Text = "";
            textSmLobbyPort.Text = "";
            textSmDownloadIP.Text = "";
            textSmDownloadPort.Text = "";
        }

        private void serverComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateServerFields();
        }

        private void textSmServerName_LostFocus(object sender, RoutedEventArgs e)
        {
            if (serverComboBox.SelectedItem is string oldName &&
                !string.IsNullOrWhiteSpace(textSmServerName.Text))
            {
                var newName = textSmServerName.Text;

                if (oldName != newName &&
                    _serverManager.RenameServer(oldName, newName))
                {
                    UpdateServerList();
                    serverComboBox.SelectedItem = newName;
                }
            }
        }

        private void UpdateCurrentServer(System.Func<Server, Server> updater)
        {
            if (serverComboBox.SelectedItem is not string name)
                return;

            if (!_serverManager.Servers.TryGetValue(name, out var oldServer))
                return;

            var newServer = updater(oldServer);
            _serverManager.Servers[name] = newServer;
        }

        private void textSmLobbyIP_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateCurrentServer(s =>
                new Server(textSmLobbyIP.Text, s.LPort, s.DLIP, s.DLPort));
        }

        private void textSmLobbyPort_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!ushort.TryParse(textSmLobbyPort.Text, out var port))
                return;

            UpdateCurrentServer(s =>
                new Server(s.LobbyIP, port, s.DLIP, s.DLPort));
        }

        private void textSmDownloadIP_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateCurrentServer(s =>
                new Server(s.LobbyIP, s.LPort, textSmDownloadIP.Text, s.DLPort));
        }

        private void textSmDownloadPort_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!ushort.TryParse(textSmDownloadPort.Text, out var port))
                return;

            UpdateCurrentServer(s =>
                new Server(s.LobbyIP, s.LPort, s.DLIP, port));
        }

        private void btnSmRemove_Click(object sender, RoutedEventArgs e)
        {
            if (serverComboBox.SelectedItem is string name)
            {
                _serverManager.RemoveServer(name);
                UpdateServerList();
            }
        }

        private void btnSmAdd_Click(object sender, RoutedEventArgs e)
        {
            AddNewServer();
        }

        private void btnSmAccept_Click(object sender, RoutedEventArgs e)
        {
            _serverManager.SaveServers();
            Close();
        }

        private void btnSmCancel_Click(object sender, RoutedEventArgs e)
        {
            _serverManager.LoadServers();
            Close();
        }
    }
}
