namespace KiotaUiClient.Core.Application.Interfaces;

public record ReleaseInfo(string TagName, string Name, Version Version, string AssetName, string AssetDownloadUrl);

public interface IUpdateService
{
    string GetCurrentVersionString();
    Task<OperationResult<ReleaseInfo>> GetLatestReleaseAsync(CancellationToken ct = default);
    bool IsUpdateAvailable(Version latest);
    Task<OperationResult<string>> DownloadAssetAsync(string url, IProgress<double>? progress = null, CancellationToken ct = default);
    OperationResult<string> ExtractToNewFolder(string zipPath, string? versionLabel = null);
    OperationResult StartUpdaterAndExit(string extractedDir, Action? shutdownAction = null);
}
