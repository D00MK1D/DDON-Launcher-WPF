using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace DDO_Launcher;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void btnLogin_Click(object sender, RoutedEventArgs e)
    {
        Operation("login");
    }

    private void btnRegister_Click(object sender, RoutedEventArgs e)
    {
        Operation("create");
    }

    public class ServerResponse
    {
        public string Error { get; set; }
        public string Message { get; set; }
        public string Token { get; set; }
    }

    private void Operation(string Action)
    {
        if ((Action != "create" && Action != "login") || textAccount.Text == "" || textPassword.Text == "")
        {
            MessageBox.Show(
                "Account or Password must not be empty!",
                "Dragon's Dogma Online",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        string responseBody = string.Empty;

        using (TcpClient client = new TcpClient())
        {
            var path = "/api/account";
            var requestData = new
            {
                Action = Action,
                Account = textAccount.Text,
                Password = textPassword.Text,
                Email = ""
                // if (textEmail.Text == ""){Email = ""}
                // else {Email = textEmail.Text} 
            };

            string jsonData = JsonSerializer.Serialize(requestData);
            string request = $"POST {path} HTTP/1.1\r\n";
            request += $"Host: localhost:52099\r\n";
            request += "Content-Type: application/json\r\n";
            request += $"Content-Length: {jsonData.Length}\r\n";
            request += "Connection: close\r\n";
            request += "\r\n";
            request += jsonData;

            client.ReceiveTimeout = 5000;
            client.SendTimeout = 5000;
            client.Connect("localhost", 52099);

            var utf8Encoding = new UTF8Encoding(false);

            using (NetworkStream stream = client.GetStream())
            using (StreamWriter writer = new StreamWriter(stream, utf8Encoding))
            using (StreamReader reader = new StreamReader(stream, utf8Encoding))
            {
                writer.Write(request);
                writer.Flush();

                StringBuilder sb = new StringBuilder();
                string line;

                stream.ReadTimeout = 5000;

                while ((line = reader.ReadLine()) != null)
                {
                    sb.AppendLine(line);
                }

                string response = sb.ToString();

                var bodyStartIndex = response.IndexOf("\r\n\r\n") + 4;
                responseBody = response.Substring(bodyStartIndex);
                
                ServerResponse serverResponse = JsonSerializer.Deserialize<ServerResponse>(responseBody);

                var token = string.Empty;
                try
                {
                    if (serverResponse.Message == "Login Success")
                    {
                        // Login
                        token = serverResponse.Token;
                    }
                    else if (serverResponse.Error == null)
                    {
                        // Register
                        MessageBox.Show(
                            serverResponse.Message,
                            "Dragon's Dogma Online",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                        return;
                    }
                    else
                    {
                        MessageBox.Show(
                            serverResponse.Error,
                            "Dragon's Dogma Online",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return;
                    }
                }
                catch (JsonException e)
                {
                    MessageBox.Show(
                        "Invalid response from server\n" + 
                        "Error: " +serverResponse.Error + "\n" +
                        "Message: " + serverResponse.Message + "\n" +
                        "Token: " + serverResponse.Token + "\n",
                        "Dragon's Dogma Online",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                try
                {
                    /* Try to show the admin prompt to launch DDOn
                    ProcessStartInfo pStartInfo = new ProcessStartInfo
                    {
                        FileName = "ddo.exe",
                        Arguments = (" addr=" +
                                        ServerManager.Servers[ServerManager.SelectedServer].LobbyIP +
                                        " port=" +
                                        ServerManager.Servers[ServerManager.SelectedServer].LPort +
                                        " token=" +
                                        token +
                                        " DL=http://" +
                                        ServerManager.Servers[ServerManager.SelectedServer].DLIP +
                                        ":" +
                                        ServerManager.Servers[ServerManager.SelectedServer].DLPort +
                                        "/win/ LVer=03.04.003.20181115.0 RVer=3040008"),

                        //Verb = "runas",
                        //UseShellExecute = true
                    };
                    Process.Start(pStartInfo);*/

                    Process.Start("ddo.exe",
                                  " addr=" +
                                  "localhost" +
                                  " port=" +
                                  "52100" +
                                  " token=" +
                                  token +
                                  " DL=http://" +
                                  "localhost" +
                                  ":" +
                                  "52099" +
                                  "/win/ LVer=03.04.003.20181115.0 RVer=3040008");

                    this.Close();
                }
                catch (Win32Exception ex)
                {
                    MessageBox.Show(
                        "DDO.exe not found! Make sure the launcher is located in the game folder \nand you're running the launcher as Admin.",
                        "Dragon's Dogma Online",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }
            }
        }

    }
}