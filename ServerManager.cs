#nullable enable

using IniParser;
using IniParser.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DDO_Launcher
{
    public sealed class ServerManager
    {
        private static class IniKeys
        {
            public const string General = "General";
            public const string LobbyIP = "LobbyIP";
            public const string LobbyPort = "LPort";
            public const string DownloadIP = "DLIP";
            public const string DownloadPort = "DLPort";
        }

        private const string BACKWARDS_COMPAT_DEFAULT_SERVER_NAME = "DDOn";

        private readonly FileIniDataParser _parser;
        private readonly string _serversFile;

        public string? SelectedServer { get; private set; }
        public Dictionary<string, Server> Servers { get; } = new();

        public ServerManager(string serversFile, FileIniDataParser? parser = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(serversFile);

            _serversFile = serversFile;
            _parser = parser ?? new FileIniDataParser();

            LoadServers();
        }

        public void SelectServer(string name)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            if (!Servers.ContainsKey(name))
                throw new ArgumentException("Server not found.", nameof(name));

            SelectedServer = name;
        }

        public void AddServer(string name, Server server)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentNullException.ThrowIfNull(server);

            if (!Servers.TryAdd(name, server))
                throw new InvalidOperationException("Server already exists.");

            SelectedServer ??= name;
        }

        public void RemoveServer(string name)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            if (!Servers.Remove(name))
                return;

            if (SelectedServer == name)
                SelectedServer = null;
        }

        public bool RenameServer(string oldName, string newName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(oldName);
            ArgumentException.ThrowIfNullOrWhiteSpace(newName);

            if (!Servers.Remove(oldName, out var server))
                return false;

            if (!Servers.TryAdd(newName, server))
            {
                Servers.Add(oldName, server);
                return false;
            }

            if (SelectedServer == oldName)
                SelectedServer = newName;

            return true;
        }

        public void LoadServers()
        {
            Servers.Clear();
            SelectedServer = null;

            if (!File.Exists(_serversFile))
                CreateDefaultIni();

            IniData data = _parser.ReadFile(_serversFile);

            foreach (var section in data.Sections.Where(s => s.SectionName != IniKeys.General))
            {
                var server = CreateServerFromSection(section.SectionName, data);
                if (server != null)
                    Servers[section.SectionName] = server;
            }

            ResolveSelectedServer(data);
        }

        public void SaveServers()
        {
            IniData data = new();

            foreach (var (name, server) in Servers)
            {
                data[name][IniKeys.LobbyIP] = server.LobbyIP;
                data[name][IniKeys.LobbyPort] = server.LPort.ToString();
                data[name][IniKeys.DownloadIP] = server.DLIP;
                data[name][IniKeys.DownloadPort] = server.DLPort.ToString();
            }

            if (SelectedServer != null && Servers.TryGetValue(SelectedServer, out var selected))
            {
                data[IniKeys.General][IniKeys.LobbyIP] = selected.LobbyIP;
                data[IniKeys.General][IniKeys.LobbyPort] = selected.LPort.ToString();
                data[IniKeys.General][IniKeys.DownloadIP] = selected.DLIP;
                data[IniKeys.General][IniKeys.DownloadPort] = selected.DLPort.ToString();
            }

            _parser.WriteFile(_serversFile, data);
        }

        private void CreateDefaultIni()
        {
            IniData data = new();

            data[IniKeys.General][IniKeys.LobbyIP] = "localhost";
            data[IniKeys.General][IniKeys.LobbyPort] = "52100";
            data[IniKeys.General][IniKeys.DownloadIP] = "localhost";
            data[IniKeys.General][IniKeys.DownloadPort] = "52099";

            _parser.WriteFile(_serversFile, data);
        }

        private Server? CreateServerFromSection(string sectionName, IniData data)
        {
            try
            {
                return new Server(
                    lobbyIP: data[sectionName][IniKeys.LobbyIP] ?? "",
                    lPort: ParsePort(data[sectionName][IniKeys.LobbyPort], 52100),
                    dlip: data[sectionName][IniKeys.DownloadIP] ?? "",
                    dlPort: ParsePort(data[sectionName][IniKeys.DownloadPort], 52099)
                );
            }
            catch
            {
                return null;
            }
        }

        private void ResolveSelectedServer(IniData data)
        {
            var general = data[IniKeys.General];
            if (general == null)
                return;

            foreach (var (name, server) in Servers)
            {
                if (server.Matches(
                    general[IniKeys.LobbyIP],
                    general[IniKeys.LobbyPort],
                    general[IniKeys.DownloadIP],
                    general[IniKeys.DownloadPort]))
                {
                    SelectedServer = name;
                    return;
                }
            }

            // Backwards compatibility
            try
            {
                var legacyServer = new Server(
                    general[IniKeys.LobbyIP] ?? "",
                    ParsePort(general[IniKeys.LobbyPort], 52100),
                    general[IniKeys.DownloadIP] ?? "",
                    ParsePort(general[IniKeys.DownloadPort], 52099)
                );

                Servers[BACKWARDS_COMPAT_DEFAULT_SERVER_NAME] = legacyServer;
                SelectedServer = BACKWARDS_COMPAT_DEFAULT_SERVER_NAME;
            }
            catch
            {
                // Ignorado propositalmente
            }
        }

        private static ushort ParsePort(string? value, ushort defaultPort)
        {
            return ushort.TryParse(value, out var port) ? port : defaultPort;
        }
    }

    public sealed class Server
    {
        public string LobbyIP { get; }
        public ushort LPort { get; }
        public string DLIP { get; }
        public ushort DLPort { get; }

        public Server(string lobbyIP, ushort lPort, string dlip, ushort dlPort)
        {
            LobbyIP = lobbyIP ?? throw new ArgumentNullException(nameof(lobbyIP));
            DLIP = dlip ?? throw new ArgumentNullException(nameof(dlip));
            LPort = lPort;
            DLPort = dlPort;
        }

        internal bool Matches(string? lobbyIp, string? lobbyPort, string? dlip, string? dlPort)
        {
            return LobbyIP == lobbyIp &&
                   DLIP == dlip &&
                   ushort.TryParse(lobbyPort, out var lp) && lp == LPort &&
                   ushort.TryParse(dlPort, out var dp) && dp == DLPort;
        }
    }
}
