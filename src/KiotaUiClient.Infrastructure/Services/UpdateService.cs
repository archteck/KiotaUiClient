using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using KiotaUiClient.Core.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace KiotaUiClient.Infrastructure.Services;

public partial class UpdateService : IUpdateService
{
    private const string RepoOwner = "archteck";
    private const string RepoName = "KiotaUiClient";
    private readonly HttpClient _http;
    private readonly ILogger<UpdateService> _logger;
    private static readonly char[] _anyOf = ['-', '+'];

    public UpdateService(HttpClient http, ILogger<UpdateService> logger)
    {
        _http = http;
        _logger = logger;
        if (_http.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("KiotaUiClient", GetCurrentVersionString()));
        }
        if (!_http.DefaultRequestHeaders.Accept.Any(x => x.MediaType == "application/vnd.github+json"))
        {
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        }
    }

    public string GetCurrentVersionString()
    {
        var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var infoAttr = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(infoAttr))
        {
            return infoAttr;
        }
        var fileVer = asm.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
        if (!string.IsNullOrWhiteSpace(fileVer))
        {
            return fileVer!;
        }
        try
        {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName
                          ?? Path.Combine(AppContext.BaseDirectory, AppDomain.CurrentDomain.FriendlyName);
            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                var fvi = FileVersionInfo.GetVersionInfo(exePath);
                if (!string.IsNullOrWhiteSpace(fvi.ProductVersion))
                    return fvi.ProductVersion!;
                if (!string.IsNullOrWhiteSpace(fvi.FileVersion))
                    return fvi.FileVersion!;
            }
        }
        catch
        {
            LogVersionMetadataReadFailed();
        }
        var ver = asm.GetName().Version;
        return ver?.ToString() ?? "0.0.0";
    }

    private static string SanitizeSemVer(string v)
    {
        var idx = v.IndexOfAny(_anyOf);
        return idx > 0 ? v[..idx] : v;
    }

    private Version GetCurrentVersion()
    {
        var s = GetCurrentVersionString();
        s = SanitizeSemVer(s);
        if (Version.TryParse(s, out var v)) return v;
        return new Version(0, 0, 0);
    }

    public async Task<OperationResult<ReleaseInfo>> GetLatestReleaseAsync(CancellationToken ct = default)
    {
        try
        {
            var url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
            using var resp = await _http.GetAsync(url, ct);
            resp.EnsureSuccessStatusCode();
            using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = doc.RootElement;
            var tag = root.GetProperty("tag_name").GetString() ?? "";
            var name = root.GetProperty("name").GetString() ?? tag;
            var version = ParseVersion(tag);

            var assetName = "";
            var assetUrl = "";
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var a in assets.EnumerateArray())
                {
                    var aname = a.GetProperty("name").GetString() ?? "";
                    var dl = a.GetProperty("browser_download_url").GetString() ?? "";
                    if (IsAssetForCurrentPlatform(aname))
                    {
                        assetName = aname;
                        assetUrl = dl;
                        break;
                    }
                }
                if (string.IsNullOrEmpty(assetUrl))
                {
                    foreach (var a in assets.EnumerateArray())
                    {
                        var aname = a.GetProperty("name").GetString() ?? "";
                        var dl = a.GetProperty("browser_download_url").GetString() ?? "";
                        if (aname.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        {
                            assetName = aname;
                            assetUrl = dl;
                            break;
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(assetUrl))
            {
                return new OperationResult<ReleaseInfo>(false, default, "No suitable release asset found for your platform.");
            }

            var release = new ReleaseInfo(tag, name, version, assetName, assetUrl);
            return new OperationResult<ReleaseInfo>(true, release, "Latest release retrieved successfully.");
        }
        catch (Exception ex)
        {
            LogGetLatestReleaseFailed(ex);
            return new OperationResult<ReleaseInfo>(false, default, "Failed to query latest release.", ex.Message);
        }
    }

    private static Version ParseVersion(string tag)
    {
        var t = tag.TrimStart('v', 'V');
        if (Version.TryParse(t, out var v)) return v;
        return new Version(0, 0, 0, 0);
    }

    private static bool IsAssetForCurrentPlatform(string assetName)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return assetName.Contains("win", StringComparison.OrdinalIgnoreCase)
                   && assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return (assetName.Contains("osx", StringComparison.OrdinalIgnoreCase) || assetName.Contains("mac", StringComparison.OrdinalIgnoreCase))
                   && assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
        }
        return assetName.Contains("linux", StringComparison.OrdinalIgnoreCase)
               && assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsUpdateAvailable(Version latest) => latest > GetCurrentVersion();

    private static string GetUpdatesRoot()
    {
        var basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KiotaUiClient", "updates");
        Directory.CreateDirectory(basePath);
        return basePath;
    }

    public async Task<OperationResult<string>> DownloadAssetAsync(string url, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        try
        {
            var updates = GetUpdatesRoot();
            var file = Path.Combine(updates, Path.GetFileName(new Uri(url).AbsolutePath));
            using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();
            var total = resp.Content.Headers.ContentLength ?? -1L;
            await using var input = await resp.Content.ReadAsStreamAsync(ct);
            await using var output = File.Create(file);
            var buffer = new byte[81920];
            long read = 0;
            int n;
            while ((n = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, n), ct);
                read += n;
                if (total > 0 && progress is not null)
                {
                    progress.Report((double)read / total);
                }
            }

            return new OperationResult<string>(true, file, "Update package downloaded successfully.");
        }
        catch (Exception ex)
        {
            LogDownloadAssetFailed(ex, url);
            return new OperationResult<string>(false, default, "Failed to download update package.", ex.Message);
        }
    }

    public OperationResult<string> ExtractToNewFolder(string zipPath, string? versionLabel = null)
    {
        try
        {
            var parent = Path.GetDirectoryName(AppContext.BaseDirectory) ?? AppContext.BaseDirectory;
            var target = Path.Combine(parent, versionLabel ?? DateTime.Now.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture));
            Directory.CreateDirectory(target);
            ZipFile.ExtractToDirectory(zipPath, target, overwriteFiles: true);
            File.Delete(zipPath);
            return new OperationResult<string>(true, target, "Update package extracted successfully.");
        }
        catch (Exception ex)
        {
            LogExtractUpdateFailed(ex, zipPath);
            return new OperationResult<string>(false, default, "Failed to extract update package.", ex.Message);
        }
    }

    private static string? FindAppUpdaterExecutable(string directory)
    {
        var baseName = "KiotaUIUpdater";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var exe = Directory.EnumerateFiles(directory, baseName + ".exe", SearchOption.AllDirectories).FirstOrDefault();
            if (exe != null) return exe;
            return Directory.EnumerateFiles(directory, "*.exe", SearchOption.AllDirectories).FirstOrDefault();
        }
        var candidates = Directory.EnumerateFiles(directory, "*KiotaUIUpdater*", SearchOption.AllDirectories).ToList();
        return candidates.FirstOrDefault();
    }

    public OperationResult StartUpdaterAndExit(string extractedDir, Action? shutdownAction = null)
    {
        try
        {
            var sourceExePath = FindAppUpdaterExecutable(extractedDir);
            if (sourceExePath is null)
            {
                return OperationResult.Failure("Updater executable was not found in extracted package.");
            }

            return TryLaunchAndExit(extractedDir, shutdownAction);
        }
        catch (Exception ex)
        {
            LogStartUpdaterFailed(ex, extractedDir);
            return OperationResult.Failure("Failed to start updater process.", ex.Message);
        }
    }

    private OperationResult TryLaunchAndExit(string extractedDir, Action? shutdownAction = null)
    {
        try
        {
            var appDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var currentExe = Process.GetCurrentProcess().MainModule?.FileName
                             ?? Path.Combine(appDir, "KiotaUiClient.exe");
            var updaterName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "KiotaUIUpdater.exe" : "KiotaUIUpdater";
            var updaterSource = Path.Combine(extractedDir, updaterName);
            var updaterTarget = Path.Combine(appDir, updaterName);
            if (!File.Exists(updaterSource))
            {
                return OperationResult.Failure("Updater executable does not exist in extracted directory.");
            }
            File.Move(updaterSource, updaterTarget, overwrite: true);

            var pid = Environment.ProcessId;
            var psi = new ProcessStartInfo
            {
                FileName = updaterTarget,
                WorkingDirectory = appDir,
                UseShellExecute = true,
                Arguments = $"\"{extractedDir}\" \"{appDir}\" \"{currentExe}\" {pid}"
            };
            Process.Start(psi);
            shutdownAction?.Invoke();
            return OperationResult.Success("Updater launched successfully.");
        }
        catch (Exception ex)
        {
            LogLaunchUpdaterFailed(ex, extractedDir);
            return OperationResult.Failure("Failed to launch updater executable.", ex.Message);
        }
    }

    [LoggerMessage(EventId = 2001, Level = LogLevel.Debug,
        Message = "Failed to read version information from process/executable metadata.")]
    private partial void LogVersionMetadataReadFailed();

    [LoggerMessage(EventId = 2002, Level = LogLevel.Error,
        Message = "Failed to start updater process from {ExtractedDir}")]
    private partial void LogStartUpdaterFailed(Exception ex, string extractedDir);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Error,
        Message = "Failed to launch updater for extracted directory {ExtractedDir}")]
    private partial void LogLaunchUpdaterFailed(Exception ex, string extractedDir);

    [LoggerMessage(EventId = 2004, Level = LogLevel.Error,
        Message = "Failed to query latest release metadata")]
    private partial void LogGetLatestReleaseFailed(Exception ex);

    [LoggerMessage(EventId = 2005, Level = LogLevel.Error,
        Message = "Failed to download update asset from {Url}")]
    private partial void LogDownloadAssetFailed(Exception ex, string url);

    [LoggerMessage(EventId = 2006, Level = LogLevel.Error,
        Message = "Failed to extract update package {ZipPath}")]
    private partial void LogExtractUpdateFailed(Exception ex, string zipPath);
}
