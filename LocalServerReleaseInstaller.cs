#nullable enable

using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.BZip2;
using ICSharpCode.SharpZipLib.Tar;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DDO_Launcher;

internal sealed class LocalServerReleaseInstaller
{
    private const string DefaultAssetPattern64 = "win-x64";
    private const string DefaultAssetPattern86 = "win-x86";
    private const string ManifestFileName = "server-release.json";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _releaseSourceUrl;
    private readonly string _rootDirectory;

    public LocalServerReleaseInstaller(string releaseSourceUrl, string? rootDirectory = null)
    {
        _releaseSourceUrl = releaseSourceUrl;
        _rootDirectory = rootDirectory ?? Path.Combine(AppContext.BaseDirectory, "LocalServer");
    }

    public string ServerDirectory => Path.Combine(_rootDirectory, "Server");

    private string ManifestPath => Path.Combine(_rootDirectory, ManifestFileName);

    public string? FindInstalledServerExe()
    {
        string serverExe = Path.Combine(ServerDirectory, "Arrowgene.Ddon.Cli.exe");
        return File.Exists(serverExe) ? serverExe : null;
    }

    public async Task<LocalServerReleaseStatus> GetStatusAsync(CancellationToken token)
    {
        LocalServerInstallManifest? installed = await ReadManifestAsync(token);
        bool hasServer = FindInstalledServerExe() != null;

        try
        {
            (GitHubRelease release, GitHubAsset asset) latest = await GetLatestReleaseAssetAsync(token);
            bool isLatest = hasServer &&
                installed != null &&
                IsSameReleaseAsset(installed, latest.release, latest.asset);

            return new LocalServerReleaseStatus(
                hasServer,
                isLatest,
                installed,
                latest.release.TagName,
                latest.asset.Name,
                null);
        }
        catch (Exception ex)
        {
            return new LocalServerReleaseStatus(
                hasServer,
                false,
                installed,
                null,
                null,
                ex.Message);
        }
    }

    public async Task<LocalServerInstallManifest> InstallLatestAsync(
        IProgress<LocalServerInstallProgress>? progress,
        CancellationToken token)
    {
        progress?.Report(LocalServerInstallProgress.Indeterminate("Checking latest release..."));
        (GitHubRelease release, GitHubAsset asset) = await GetLatestReleaseAssetAsync(token);

        Directory.CreateDirectory(_rootDirectory);
        string downloadsDirectory = Path.Combine(_rootDirectory, "Downloads");
        Directory.CreateDirectory(downloadsDirectory);

        string archivePath = Path.Combine(downloadsDirectory, asset.Name ?? "server-release.tar.gz");

        progress?.Report(LocalServerInstallProgress.Indeterminate($"Downloading {release.TagName ?? "server"}..."));
        await DownloadFileAsync(asset.DownloadUrl!, archivePath, progress, token);
        ArchiveKind archiveKind = GetArchiveKind(archivePath);
        VerifyAssetDigest(archivePath, asset, archiveKind);

        string extractDirectory = Path.Combine(_rootDirectory, ".install-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(extractDirectory);

        try
        {
            progress?.Report(LocalServerInstallProgress.Indeterminate("Extracting server files..."));
            ExtractArchive(archivePath, extractDirectory, archiveKind);

            string extractedServerDirectory = FindExtractedServerDirectory(extractDirectory);

            progress?.Report(LocalServerInstallProgress.Indeterminate("Installing server..."));
            InstallExtractedServer(extractedServerDirectory);

            var manifest = new LocalServerInstallManifest
            {
                ReleaseTag = release.TagName,
                ReleaseUrl = release.HtmlUrl,
                AssetName = asset.Name,
                AssetDigest = asset.Digest,
                DownloadUrl = asset.DownloadUrl,
                AssetSize = asset.Size,
                InstalledAt = DateTimeOffset.UtcNow
            };

            await SaveManifestAsync(manifest, token);
            TryDeleteFile(archivePath);

            progress?.Report(LocalServerInstallProgress.Complete("Server ready"));
            return manifest;
        }
        finally
        {
            TryDeleteDirectory(extractDirectory);
        }
    }

    private async Task<(GitHubRelease release, GitHubAsset asset)> GetLatestReleaseAssetAsync(CancellationToken token)
    {
        Uri apiUri = GetLatestReleaseApiUri(_releaseSourceUrl);

        using HttpClient client = CreateGitHubClient();
        using HttpResponseMessage response = await client.GetAsync(apiUri, token);
        response.EnsureSuccessStatusCode();

        await using Stream stream = await response.Content.ReadAsStreamAsync(token);
        GitHubRelease? release = await JsonSerializer.DeserializeAsync<GitHubRelease>(stream, cancellationToken: token);
        if (release == null)
            throw new InvalidOperationException("The server release response was empty.");

        GitHubAsset? asset = SelectWindowsAsset(release);
        if (asset?.DownloadUrl == null)
            throw new InvalidOperationException("The latest server release does not include a Windows server archive.");

        return (release, asset);
    }

    private static HttpClient CreateGitHubClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("DDO-Launcher", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    private static HttpClient CreateDownloadClient()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.None
        };

        var client = new HttpClient(handler);
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("DDO-Launcher", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*", 0.8));
        return client;
    }

    private static Uri GetLatestReleaseApiUri(string releaseSourceUrl)
    {
        if (!Uri.TryCreate(releaseSourceUrl, UriKind.Absolute, out Uri? uri))
            throw new InvalidOperationException("The local server release URL in DDO_Launcher.ini is invalid.");

        if (uri.Host.Equals("api.github.com", StringComparison.OrdinalIgnoreCase))
            return uri;

        if (!uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only GitHub release URLs are supported for local server downloads.");

        string[] parts = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3 || !parts[2].Equals("releases", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The local server release URL must point to a GitHub releases page.");

        return new Uri($"https://api.github.com/repos/{parts[0]}/{parts[1]}/releases/latest");
    }

    private static GitHubAsset? SelectWindowsAsset(GitHubRelease release)
    {
        string preferred = Environment.Is64BitOperatingSystem ? DefaultAssetPattern64 : DefaultAssetPattern86;
        string fallback = Environment.Is64BitOperatingSystem ? DefaultAssetPattern86 : DefaultAssetPattern64;

        return FindAsset(release, preferred) ?? FindAsset(release, fallback);
    }

    private static GitHubAsset? FindAsset(GitHubRelease release, string assetPattern)
    {
        return release.Assets
            .Where(asset => asset.Name != null &&
                            asset.Name.Contains(assetPattern, StringComparison.OrdinalIgnoreCase) &&
                            asset.Name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
            .OrderBy(asset => asset.Name)
            .FirstOrDefault();
    }

    private static bool IsSameReleaseAsset(
        LocalServerInstallManifest installed,
        GitHubRelease release,
        GitHubAsset asset)
    {
        if (!string.Equals(installed.ReleaseTag, release.TagName, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(installed.AssetName, asset.Name, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(asset.Digest))
            return true;

        return string.Equals(installed.AssetDigest, asset.Digest, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task DownloadFileAsync(
        string url,
        string destinationPath,
        IProgress<LocalServerInstallProgress>? progress,
        CancellationToken token)
    {
        using HttpClient client = CreateDownloadClient();
        using HttpResponseMessage response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();

        long? totalBytes = response.Content.Headers.ContentLength;
        long downloadedBytes = 0;
        byte[] buffer = new byte[81920];

        await using Stream source = await response.Content.ReadAsStreamAsync(token);
        await using FileStream destination = File.Create(destinationPath);
        while (true)
        {
            int read = await source.ReadAsync(buffer, token);
            if (read == 0)
                break;

            await destination.WriteAsync(buffer.AsMemory(0, read), token);
            downloadedBytes += read;

            if (totalBytes.HasValue && totalBytes.Value > 0)
            {
                progress?.Report(LocalServerInstallProgress.WithPercent(
                    $"Downloading {FormatBytes(downloadedBytes)} / {FormatBytes(totalBytes.Value)}",
                    downloadedBytes,
                    totalBytes.Value));
            }
        }
    }

    private static ArchiveKind GetArchiveKind(string archivePath)
    {
        using FileStream stream = File.OpenRead(archivePath);
        byte[] header = new byte[512];
        int read = stream.Read(header, 0, header.Length);

        if (read >= 2 && header[0] == 0x1F && header[1] == 0x8B)
            return ArchiveKind.GzipTar;

        if (read >= 3 && header[0] == (byte)'B' && header[1] == (byte)'Z' && header[2] == (byte)'h')
            return ArchiveKind.Bzip2Tar;

        if (read >= 265 &&
            header[257] == (byte)'u' &&
            header[258] == (byte)'s' &&
            header[259] == (byte)'t' &&
            header[260] == (byte)'a' &&
            header[261] == (byte)'r')
        {
            return ArchiveKind.Tar;
        }

        throw new InvalidOperationException(
            "The downloaded server file was not a supported tar archive. GitHub may have returned an error page instead of the release asset.");
    }

    private static string FormatBytes(long bytes)
    {
        const double scale = 1024;
        string[] units = { "B", "KB", "MB", "GB" };
        double value = bytes;
        int unit = 0;

        while (value >= scale && unit < units.Length - 1)
        {
            value /= scale;
            unit++;
        }

        return $"{value:0.#} {units[unit]}";
    }

    private static void VerifyAssetDigest(string archivePath, GitHubAsset asset, ArchiveKind archiveKind)
    {
        if (string.IsNullOrWhiteSpace(asset.Digest) ||
            !asset.Digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (archiveKind == ArchiveKind.Tar)
        {
            return;
        }

        string expected = asset.Digest["sha256:".Length..].Trim();
        using FileStream stream = File.OpenRead(archivePath);
        string actual = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();

        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The downloaded local server archive did not match GitHub's SHA-256 digest.");
    }

    private static void ExtractArchive(string archivePath, string destinationDirectory, ArchiveKind archiveKind)
    {
        string destinationRoot = Path.GetFullPath(destinationDirectory);
        string separator = Path.DirectorySeparatorChar.ToString();
        string destinationRootWithSeparator = destinationRoot.EndsWith(separator, StringComparison.Ordinal)
            ? destinationRoot
            : destinationRoot + separator;

        using FileStream fileStream = File.OpenRead(archivePath);
        using Stream archiveStream = CreateArchiveStream(fileStream, archiveKind);
        using TarInputStream tarStream = new(archiveStream, Encoding.UTF8);

        TarEntry? entry;
        while ((entry = tarStream.GetNextEntry()) != null)
        {
            string entryName = entry.Name.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
            if (string.IsNullOrWhiteSpace(entryName))
                continue;

            string destinationPath = Path.GetFullPath(Path.Combine(destinationRoot, entryName));
            if (!destinationPath.StartsWith(destinationRootWithSeparator, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The local server archive contains an invalid path.");

            if (entry.IsDirectory)
            {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            using FileStream output = File.Create(destinationPath);
            tarStream.CopyEntryContents(output);
        }
    }

    private static Stream CreateArchiveStream(Stream fileStream, ArchiveKind archiveKind)
    {
        return archiveKind switch
        {
            ArchiveKind.GzipTar => new GZipInputStream(fileStream),
            ArchiveKind.Bzip2Tar => new BZip2InputStream(fileStream),
            ArchiveKind.Tar => fileStream,
            _ => throw new InvalidOperationException("Unsupported server archive format.")
        };
    }

    private static string FindExtractedServerDirectory(string extractDirectory)
    {
        string? serverExe = Directory
            .EnumerateFiles(extractDirectory, "Arrowgene.Ddon.Cli.exe", SearchOption.AllDirectories)
            .FirstOrDefault();

        if (serverExe == null)
            throw new InvalidOperationException("The downloaded server archive did not contain Arrowgene.Ddon.Cli.exe.");

        string serverDirectory = Path.GetDirectoryName(serverExe)!;
        if (!Directory.Exists(Path.Combine(serverDirectory, "Files")))
            throw new InvalidOperationException("The downloaded server archive did not contain the server Files folder.");

        return serverDirectory;
    }

    private void InstallExtractedServer(string extractedServerDirectory)
    {
        string backupDirectory = Path.Combine(_rootDirectory, "Server.previous-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        string preservedDirectory = Path.Combine(_rootDirectory, ".preserved-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(_rootDirectory);

        try
        {
            PreserveLocalServerData(ServerDirectory, preservedDirectory);

            if (Directory.Exists(ServerDirectory))
                Directory.Move(ServerDirectory, backupDirectory);

            Directory.Move(extractedServerDirectory, ServerDirectory);
            RestoreLocalServerData(ServerDirectory, preservedDirectory);

            TryDeleteDirectory(backupDirectory);
        }
        catch
        {
            if (!Directory.Exists(ServerDirectory) && Directory.Exists(backupDirectory))
                Directory.Move(backupDirectory, ServerDirectory);

            throw;
        }
        finally
        {
            TryDeleteDirectory(preservedDirectory);
        }
    }

    private static void PreserveLocalServerData(string serverDirectory, string preservedDirectory)
    {
        if (!Directory.Exists(serverDirectory))
            return;

        CopyIfExists(
            Path.Combine(serverDirectory, "Files", "Database", "db.sqlite"),
            Path.Combine(preservedDirectory, "Files", "Database", "db.sqlite"));

        CopyIfExists(
            Path.Combine(serverDirectory, "Files", "Arrowgene.Ddon.config.json"),
            Path.Combine(preservedDirectory, "Files", "Arrowgene.Ddon.config.json"));

        CopyDirectoryIfExists(
            Path.Combine(serverDirectory, "Files", "Database", "Backups"),
            Path.Combine(preservedDirectory, "Files", "Database", "Backups"));
    }

    private static void RestoreLocalServerData(string serverDirectory, string preservedDirectory)
    {
        CopyIfExists(
            Path.Combine(preservedDirectory, "Files", "Database", "db.sqlite"),
            Path.Combine(serverDirectory, "Files", "Database", "db.sqlite"));

        CopyIfExists(
            Path.Combine(preservedDirectory, "Files", "Arrowgene.Ddon.config.json"),
            Path.Combine(serverDirectory, "Files", "Arrowgene.Ddon.config.json"));

        CopyDirectoryIfExists(
            Path.Combine(preservedDirectory, "Files", "Database", "Backups"),
            Path.Combine(serverDirectory, "Files", "Database", "Backups"));
    }

    private static void CopyIfExists(string source, string destination)
    {
        if (!File.Exists(source))
            return;

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination, overwrite: true);
    }

    private static void CopyDirectoryIfExists(string source, string destination)
    {
        if (!Directory.Exists(source))
            return;

        foreach (string directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(source, directory);
            Directory.CreateDirectory(Path.Combine(destination, relative));
        }

        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(source, file);
            string destinationFile = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);
            File.Copy(file, destinationFile, overwrite: true);
        }
    }

    private async Task<LocalServerInstallManifest?> ReadManifestAsync(CancellationToken token)
    {
        if (!File.Exists(ManifestPath))
            return null;

        try
        {
            await using FileStream stream = File.OpenRead(ManifestPath);
            return await JsonSerializer.DeserializeAsync<LocalServerInstallManifest>(stream, cancellationToken: token);
        }
        catch
        {
            return null;
        }
    }

    private async Task SaveManifestAsync(LocalServerInstallManifest manifest, CancellationToken token)
    {
        Directory.CreateDirectory(_rootDirectory);
        await using FileStream stream = File.Create(ManifestPath);
        await JsonSerializer.SerializeAsync(stream, manifest, JsonOptions, token);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("assets")]
        public List<GitHubAsset> Assets { get; set; } = new();
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string? DownloadUrl { get; set; }

        [JsonPropertyName("digest")]
        public string? Digest { get; set; }

        [JsonPropertyName("size")]
        public long Size { get; set; }
    }
}

internal sealed class LocalServerReleaseStatus
{
    public LocalServerReleaseStatus(
        bool isInstalled,
        bool isLatest,
        LocalServerInstallManifest? installed,
        string? latestReleaseTag,
        string? latestAssetName,
        string? latestCheckError)
    {
        IsInstalled = isInstalled;
        IsLatest = isLatest;
        Installed = installed;
        LatestReleaseTag = latestReleaseTag;
        LatestAssetName = latestAssetName;
        LatestCheckError = latestCheckError;
    }

    public bool IsInstalled { get; }
    public bool IsLatest { get; }
    public bool UpdateAvailable => IsInstalled && LatestReleaseTag != null && !IsLatest;
    public LocalServerInstallManifest? Installed { get; }
    public string? LatestReleaseTag { get; }
    public string? LatestAssetName { get; }
    public string? LatestCheckError { get; }
}

internal sealed class LocalServerInstallManifest
{
    public string? ReleaseTag { get; set; }
    public string? ReleaseUrl { get; set; }
    public string? AssetName { get; set; }
    public string? AssetDigest { get; set; }
    public string? DownloadUrl { get; set; }
    public long AssetSize { get; set; }
    public DateTimeOffset InstalledAt { get; set; }
}

internal sealed class LocalServerInstallProgress
{
    private LocalServerInstallProgress(string message, double? percent, bool isComplete)
    {
        Message = message;
        Percent = percent;
        IsComplete = isComplete;
    }

    public string Message { get; }
    public double? Percent { get; }
    public bool IsComplete { get; }

    public static LocalServerInstallProgress Indeterminate(string message)
    {
        return new LocalServerInstallProgress(message, null, false);
    }

    public static LocalServerInstallProgress WithPercent(string message, long current, long total)
    {
        double percent = total <= 0 ? 0 : Math.Clamp((double)current / total * 100, 0, 100);
        return new LocalServerInstallProgress(message, percent, false);
    }

    public static LocalServerInstallProgress Complete(string message)
    {
        return new LocalServerInstallProgress(message, 100, true);
    }
}

internal enum ArchiveKind
{
    GzipTar,
    Bzip2Tar,
    Tar
}
