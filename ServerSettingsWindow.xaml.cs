using System.Windows;
using System.Windows.Controls;

namespace DDO_Launcher
{
    public partial class ServerSettingsWindow : Window
    {
        private readonly ServerManager ServerManager;

        public ServerSettingsWindow(ServerManager serverManager)
        {
            InitializeComponent();
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            ServerManager = serverManager;

            if (ServerManager.Servers.Count == 0)
            {
                AddNewServer();
            }
            else
            {
                UpdateServerList();
            }
        }

        private void AddNewServer()
        {
            string newServerName;
            int i = 1;
            do
            {
                newServerName = "Server #" + (ServerManager.Servers.Count + i);
                i++;
            } while (ServerManager.Servers.ContainsKey(newServerName));

            ServerManager.AddServer(newServerName, new Server());
            UpdateServerList();
            serverComboBox.SelectedIndex = serverComboBox.Items.Count - 1;
        }

        private void UpdateServerList()
        {
            int oldSelectionIndex = serverComboBox.SelectedIndex;

            serverComboBox.Items.Clear();
            foreach (var server in ServerManager.Servers)
            {
                serverComboBox.Items.Add(server.Key);
            }

            if (oldSelectionIndex == -1 && serverComboBox.Items.Count > 0)
            {
                serverComboBox.SelectedIndex = 0;
            }
            else if (oldSelectionIndex < serverComboBox.Items.Count)
            {
                serverComboBox.SelectedIndex = oldSelectionIndex;
            }

            UpdateServerFields();
        }

        private void UpdateServerFields()
        {
            try
            {
                if (!ServerManager.Servers.ContainsKey((string)serverComboBox.SelectedItem))
                {
                    textSmServerName.Text = "";
                    textSmServerName.IsEnabled = false;
                    textSmLobbyIP.Text = "";
                    textSmLobbyIP.IsEnabled = false;
                    textSmLobbyPort.Text = "";
                    textSmLobbyPort.IsEnabled = false;
                    textSmDownloadIP.Text = "";
                    textSmDownloadIP.IsEnabled = false;
                    textSmDownloadPort.Text = "";
                    textSmDownloadPort.IsEnabled = false;
                    btnSmRemove.IsEnabled = false;
                }
                else
                {
                    var serverName = (string)serverComboBox.SelectedItem;
                    Server server = ServerManager.Servers[serverName];
                    textSmServerName.Text = serverName;
                    textSmServerName.IsEnabled = true;
                    textSmLobbyIP.Text = server.LobbyIP;
                    textSmLobbyIP.IsEnabled = true;
                    textSmLobbyPort.Text = server.LPort.ToString();
                    textSmLobbyPort.IsEnabled = true;
                    textSmDownloadIP.Text = server.DLIP;
                    textSmDownloadIP.IsEnabled = true;
                    textSmDownloadPort.Text = server.DLPort.ToString();
                    textSmDownloadPort.IsEnabled = true;
                    btnSmRemove.IsEnabled = true;
                }
            }
            catch
            {
                if (!ServerManager.Servers.ContainsKey((string)serverComboBox.SelectionBoxItem))
                {
                    textSmServerName.Text = "";
                    textSmServerName.IsEnabled = false;
                    textSmLobbyIP.Text = "";
                    textSmLobbyIP.IsEnabled = false;
                    textSmLobbyPort.Text = "";
                    textSmLobbyPort.IsEnabled = false;
                    textSmDownloadIP.Text = "";
                    textSmDownloadIP.IsEnabled = false;
                    textSmDownloadPort.Text = "";
                    textSmDownloadPort.IsEnabled = false;
                    btnSmRemove.IsEnabled = false;
                }
                else
                {
                    var serverName = (string)serverComboBox.SelectionBoxItem;
                    Server server = ServerManager.Servers[serverName];
                    textSmServerName.Text = serverName;
                    textSmServerName.IsEnabled = true;
                    textSmLobbyIP.Text = server.LobbyIP;
                    textSmLobbyIP.IsEnabled = true;
                    textSmLobbyPort.Text = server.LPort.ToString();
                    textSmLobbyPort.IsEnabled = true;
                    textSmDownloadIP.Text = server.DLIP;
                    textSmDownloadIP.IsEnabled = true;
                    textSmDownloadPort.Text = server.DLPort.ToString();
                    textSmDownloadPort.IsEnabled = true;
                    btnSmRemove.IsEnabled = true;
                }
            }
        }

        private void btnSmAccept_Click(object sender, RoutedEventArgs e)
        {
            ServerManager.SaveServers();
            this.Close();
        }

        private void btnSmCancel_Click(object sender, RoutedEventArgs e)
        {
            ServerManager.LoadServers();
            this.Close();
        }

        private void serverComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateServerFields();
        }

        private void textSmServerName_LostFocus(object sender, RoutedEventArgs e)
        {
            if (serverComboBox.SelectedItem is string oldName && !string.IsNullOrWhiteSpace(textSmServerName.Text))
            {
                string newName = textSmServerName.Text;
                if (oldName != newName)
                {
                    ServerManager.RenameServer(oldName, newName);
                    UpdateServerList();
                    serverComboBox.SelectedItem = newName;
                }
            }
        }

        private void textSmLobbyIP_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (serverComboBox.SelectedItem is string serverName)
            {
                Server server = ServerManager.Servers[serverName];
                server.LobbyIP = textSmLobbyIP.Text;
            }
        }

        private void textSmLobbyPort_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (serverComboBox.SelectedItem is string serverName)
            {
                Server server = ServerManager.Servers[serverName];
                if (ushort.TryParse(textSmLobbyPort.Text, out ushort port))
                {
                    server.LPort = port;
                }
            }
        }

        private void textSmDownloadIP_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (serverComboBox.SelectedItem is string serverName)
            {
                Server server = ServerManager.Servers[serverName];
                server.DLIP = textSmDownloadIP.Text;
            }
        }

        private void textSmDownloadPort_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (serverComboBox.SelectedItem is string serverName)
            {
                Server server = ServerManager.Servers[serverName];
                if (ushort.TryParse(textSmDownloadPort.Text, out ushort port))
                {
                    server.DLPort = port;
                }
            }
        }

        private void btnSmRemove_Click(object sender, RoutedEventArgs e)
        {
            if (serverComboBox.SelectedItem is string serverName)
            {
                ServerManager.RemoveServer(serverName);
                UpdateServerList();
            }
        }

        private void btnSmAdd_Click(object sender, RoutedEventArgs e)
        {
            AddNewServer();
        }
    }
}
