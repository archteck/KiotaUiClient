using System.IO.Compression;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace KiotaUiClient.Services;

public static class UpdateService
{
    private const string RepoOwner = "archteck";
    private const string RepoName = "KiotaUiClient";
    private static readonly HttpClient _http = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(30)
    };
    private static readonly char[] _anyOf = ['-', '+'];

    static UpdateService()
    {
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("KiotaUiClient", GetCurrentVersionString()));
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    private static string GetCurrentVersionString()
    {
        // Try AssemblyInformationalVersionAttribute first (can carry semver tags from CI)
        var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var infoAttr = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(infoAttr))
        {
            return infoAttr;
        }
        // Try AssemblyFileVersion
        var fileVer = asm.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
        if (!string.IsNullOrWhiteSpace(fileVer))
        {
            return fileVer!;
        }
        // Try reading ProductVersion from the actual executable on disk
        try
        {
            var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
                          ?? Path.Combine(AppContext.BaseDirectory, AppDomain.CurrentDomain.FriendlyName);
            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                var fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(exePath);
                if (!string.IsNullOrWhiteSpace(fvi.ProductVersion))
                    return fvi.ProductVersion!;
                if (!string.IsNullOrWhiteSpace(fvi.FileVersion))
                    return fvi.FileVersion!;
            }
        }
        catch
        {
            // ignore and fall back
        }
        // Fallback to AssemblyName.Version (often 1.0.0.0 if not set explicitly)
        var ver = asm.GetName().Version;
        return ver?.ToString() ?? "0.0.0";
    }

    private static string SanitizeSemVer(string v)
    {
        // Strip metadata/prerelease parts for System.Version parsing
        var idx = v.IndexOfAny(_anyOf);
        return idx > 0 ? v[..idx] : v;
    }

    private static Version GetCurrentVersion()
    {
        var s = GetCurrentVersionString();
        s = SanitizeSemVer(s);
        if (Version.TryParse(s, out var v)) return v;
        return new Version(0,0,0);
    }

    public record ReleaseInfo(string TagName, string Name, Version Version, string AssetName, string AssetDownloadUrl);

    public static async Task<ReleaseInfo?> GetLatestReleaseAsync(CancellationToken ct = default)
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
        // pick asset
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
            // fallback: first zip
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
        if (string.IsNullOrEmpty(assetUrl)) return null;
        return new ReleaseInfo(tag, name, version, assetName, assetUrl);
    }

    private static Version ParseVersion(string tag)
    {
        var t = tag.TrimStart('v', 'V');
        if (Version.TryParse(t, out var v)) return v;
        return new Version(0,0,0,0);
    }

    private static bool IsAssetForCurrentPlatform(string assetName)
    {
        // Very simple heuristics by OS
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
        // Linux
        return assetName.Contains("linux", StringComparison.OrdinalIgnoreCase)
               && assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsUpdateAvailable(Version latest) => latest > GetCurrentVersion();

    public static string GetUpdatesRoot()
    {
        var basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KiotaUiClient", "updates");
        Directory.CreateDirectory(basePath);
        return basePath;
    }

    public static async Task<string> DownloadAssetAsync(string url, IProgress<double>? progress = null, CancellationToken ct = default)
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
        return file;
    }

    public static string ExtractToNewFolder(string zipPath, string? versionLabel = null)
    {
        var parent = Path.GetDirectoryName(AppContext.BaseDirectory) ?? AppContext.BaseDirectory;
        var target = Path.Combine(parent, versionLabel ?? DateTime.Now.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture));
        Directory.CreateDirectory(target);
        ZipFile.ExtractToDirectory(zipPath, target, overwriteFiles: true);
        File.Delete(zipPath);
        return target;
    }

    public static string? FindAppUpdaterExecutable(string directory)
    {
        var baseName = "KiotaUIUpdater";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var exe = Directory.EnumerateFiles(directory, baseName + ".exe", SearchOption.AllDirectories).FirstOrDefault();
            if (exe != null) return exe;
            // fallback: any exe
            return Directory.EnumerateFiles(directory, "*.exe", SearchOption.AllDirectories).FirstOrDefault();
        }
        // On Unix/Mac, the published artifact may be a binary without extension
        var candidates = Directory.EnumerateFiles(directory, "*KiotaUIUpdater*", SearchOption.AllDirectories).ToList();
        return candidates.FirstOrDefault();
    }
    public static bool StartUpdaterAndExit(string extractedDir)
    {
        try
        {
            var sourceExePath = FindAppUpdaterExecutable(extractedDir);
            if (sourceExePath is null) return false;
            var ok = TryLaunchAndExit(extractedDir);
            if (ok)
            {
                (Avalonia.Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.Shutdown();
            }
            return ok;
        }
        catch
        {
            return false;
        }
    }

    public static bool TryLaunchAndExit(string extractedDir)
    {
        try
        {
            var appDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var currentExe = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
                             ?? Path.Combine(appDir, "KiotaUiClient.exe");
            var updaterName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "KiotaUIUpdater.exe" : "KiotaUIUpdater";
            var updaterSource = Path.Combine(extractedDir, updaterName);
            var  updaterTarget = Path.Combine(appDir, updaterName);
            if (!File.Exists(updaterSource))
            {
                // Updater must have been copied to app folder during build
                return false;
            }
            File.Move(updaterSource, updaterTarget, overwrite: true);

            var pid = Environment.ProcessId;
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = updaterTarget,
                WorkingDirectory = appDir,
                UseShellExecute = true,
                Arguments = $"\"{extractedDir}\" \"{appDir}\" \"{currentExe}\" {pid}"
            };
            System.Diagnostics.Process.Start(psi);
            // Request app shutdown
            (Avalonia.Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.Shutdown();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
