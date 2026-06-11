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
            public const string Launcher = "Launcher";
            public const string LocalPlay = "LocalPlay";
            public const string LobbyIP = "LobbyIP";
            public const string LobbyPort = "LPort";
            public const string DownloadIP = "DLIP";
            public const string DownloadPort = "DLPort";
            public const string TranslationPatchUrl = "TranslationPatchUrl";
            public const string ServerReleaseUrl = "ServerReleaseUrl";
            public const string AutoUpdateServer = "AutoUpdateServer";
            public const string ServerStartupTimeoutSeconds = "ServerStartupTimeoutSeconds";
        }

        private const string BACKWARDS_COMPAT_DEFAULT_SERVER_NAME = "DDOn";
        public const string DefaultTranslationPatchUrl = "https://raw.githubusercontent.com/Sapphiratelaemara/DDON-translation/refs/heads/main/English/gmd.csv";
        public const string DefaultLocalServerReleaseUrl = "https://github.com/sebastian-heinz/Arrowgene.DragonsDogmaOnline/releases";

        private readonly FileIniDataParser _parser;
        private readonly string _serversFile;

        public string? SelectedServer { get; private set; }
        public string TranslationPatchUrl { get; private set; } = DefaultTranslationPatchUrl;
        public string LocalServerReleaseUrl { get; private set; } = DefaultLocalServerReleaseUrl;
        public bool LocalServerAutoUpdate { get; private set; } = true;
        public int LocalServerStartupTimeoutSeconds { get; private set; } = 90;
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
                if (section.SectionName == IniKeys.Launcher || section.SectionName == IniKeys.LocalPlay)
                    continue;

                var server = CreateServerFromSection(section.SectionName, data);
                if (server != null)
                    Servers[section.SectionName] = server;
            }

            ResolveLauncherSettings(data);
            ResolveLocalPlaySettings(data);
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

            data[IniKeys.Launcher][IniKeys.TranslationPatchUrl] = TranslationPatchUrl;
            data[IniKeys.LocalPlay][IniKeys.ServerReleaseUrl] = LocalServerReleaseUrl;
            data[IniKeys.LocalPlay][IniKeys.AutoUpdateServer] = LocalServerAutoUpdate.ToString();
            data[IniKeys.LocalPlay][IniKeys.ServerStartupTimeoutSeconds] = LocalServerStartupTimeoutSeconds.ToString();

            _parser.WriteFile(_serversFile, data);
        }

        public void SetTranslationPatchUrl(string url)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(url);

            TranslationPatchUrl = url.Trim();
            SaveServers();
        }

        private void CreateDefaultIni()
        {
            IniData data = new();

            data[IniKeys.General][IniKeys.LobbyIP] = "localhost";
            data[IniKeys.General][IniKeys.LobbyPort] = "52100";
            data[IniKeys.General][IniKeys.DownloadIP] = "localhost";
            data[IniKeys.General][IniKeys.DownloadPort] = "52099";
            data[IniKeys.Launcher][IniKeys.TranslationPatchUrl] = DefaultTranslationPatchUrl;
            data[IniKeys.LocalPlay][IniKeys.ServerReleaseUrl] = DefaultLocalServerReleaseUrl;
            data[IniKeys.LocalPlay][IniKeys.AutoUpdateServer] = true.ToString();
            data[IniKeys.LocalPlay][IniKeys.ServerStartupTimeoutSeconds] = "90";

            _parser.WriteFile(_serversFile, data);
        }

        private void ResolveLauncherSettings(IniData data)
        {
            string? translationPatchUrl = data[IniKeys.Launcher]?[IniKeys.TranslationPatchUrl];
            TranslationPatchUrl = string.IsNullOrWhiteSpace(translationPatchUrl)
                ? DefaultTranslationPatchUrl
                : translationPatchUrl.Trim();
        }

        private void ResolveLocalPlaySettings(IniData data)
        {
            string? releaseUrl = data[IniKeys.LocalPlay]?[IniKeys.ServerReleaseUrl];
            LocalServerReleaseUrl = string.IsNullOrWhiteSpace(releaseUrl)
                ? DefaultLocalServerReleaseUrl
                : releaseUrl.Trim();

            string? autoUpdate = data[IniKeys.LocalPlay]?[IniKeys.AutoUpdateServer];
            LocalServerAutoUpdate = string.IsNullOrWhiteSpace(autoUpdate) ||
                !bool.TryParse(autoUpdate, out bool parsed) ||
                parsed;

            string? startupTimeout = data[IniKeys.LocalPlay]?[IniKeys.ServerStartupTimeoutSeconds];
            LocalServerStartupTimeoutSeconds =
                int.TryParse(startupTimeout, out int timeoutSeconds)
                    ? Math.Clamp(timeoutSeconds, 30, 300)
                    : 90;
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
