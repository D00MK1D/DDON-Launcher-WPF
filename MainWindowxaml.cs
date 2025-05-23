using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace DDO_Launcher;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
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
            labelServer.Margin = new Thickness(0, 143, 10, 0);
            serverComboBox.Margin = new Thickness(10, 168, 43, 0);
            btnServerSettings.Margin = new Thickness(218, 168, 10, 77);
            labelEmail.Visibility = Visibility.Visible;
            textEmail.Visibility = Visibility.Visible;
            labelRemember.Visibility = Visibility.Hidden;
            cbRemember.Visibility = Visibility.Hidden;
        }

        else //if ((string)btnSubmit.Content == "Register")
        {
            btnSubmit.Content = "Login";
            btnChangeAction.Content = "Register";
            labelServer.Margin = new Thickness(0, 94, 10, 0);
            serverComboBox.Margin = new Thickness(10, 119, 43, 0);
            btnServerSettings.Margin = new Thickness(218, 119, 10, 126);
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
        
        if ((Action != "create" && Action != "login") || textAccount.Text == "" || textPassword.Text == "")
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
                Password = textPassword.Text,
                Email = ""
            };

            jsonData = JsonSerializer.Serialize(requestData);
            var content = new StringContent(jsonData, Encoding.UTF8, "application/json");
            client.DefaultRequestVersion = HttpVersion.Version11;
            HttpResponseMessage response = new HttpResponseMessage();

            response = await client.PostAsync("http://localhost:52099" + path, content);

            //+ ServerManager.Servers[ServerManager.SelectedServer].DLIP + ":" + ServerManager.Servers[ServerManager.SelectedServer].DLPort

            var responseBody = await response.Content.ReadAsStringAsync();

            /*jsonData = JsonSerializer.Serialize(requestData);
            //string request = $"POST {path} HTTP/1.1\r\n";
            //request += $"Host: localhost:52099\r\n";
            //request += "Content-Type: application/json\r\n";
            //request += $"Content-Length: {jsonData.Length}\r\n";
            //request += "Connection: close\r\n";
            //request += "\r\n";
            //request += jsonData;

            //client.ReceiveTimeout = 5000;
            //client.SendTimeout = 5000;
            //client.Connect("localhost", 52099);

            //var utf8Encoding = new UTF8Encoding(false);

            //using (NetworkStream stream = client.GetStream())
            //using (StreamWriter writer = new StreamWriter(stream, utf8Encoding))
            //using (StreamReader reader = new StreamReader(stream, utf8Encoding))
            //{
            //    writer.Write(request);
            //    writer.Flush();

            //    StringBuilder sb = new StringBuilder();
            //    string line;

            //    stream.ReadTimeout = 5000;

            //    while ((line = reader.ReadLine()) != null)
            //    {
            //        sb.AppendLine(line);
            //    }

            //    string response = sb.ToString();

            //    var bodyStartIndex = response.IndexOf("\r\n\r\n") + 4;
            //    responseBody = response.Substring(bodyStartIndex);
            */

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
                    "Error: " + serverResponse.Error + "\n" +
                    "Message: " + serverResponse.Message + "\n" +
                    "Token: " + serverResponse.Token + "\n",
                    "Dragon's Dogma Online",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                btnChangeAction.IsEnabled = true;
                btnSubmit.IsEnabled = true;

                return;
            }

            try
            {
                Process.Start("ddo.exe",
                              " addr=" + "localhost" +
                              " port=" + "52100" +
                              " token=" + token +
                              " DL=http://" + "localhost" +
                              ":" + "52099" +
                              "/win/ LVer=03.04.003.20181115.0 RVer=3040008");

                btnChangeAction.IsEnabled = true;
                btnSubmit.IsEnabled = true;

                this.Close();
            }
            catch (Win32Exception ex)
            {
                MessageBox.Show(
                    "DDO.exe not found! Make sure the launcher is located in the game folder and you're running the launcher as Admin.",
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
        ServerSettingsWindow ssw = new ServerSettingsWindow();
        ssw.ShowDialog();
    }

    private void btnMinimize_Click(object sender, RoutedEventArgs e)
    {
        this.WindowState = WindowState.Minimized;
    }

    private void btnClose_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }

    private void btnGeneralSettings_Click(object sender, RoutedEventArgs e)
    {

    }
}
