using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KiotaUiClient.Core.Application.Interfaces;

namespace KiotaUiClient.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IKiotaService _kiotaService;
    private readonly IUpdateService _updateService;
    private readonly ISettingsService _settingsService;
    private readonly IUiService _uiService;

    public MainWindowViewModel(
        IKiotaService kiotaService,
        IUpdateService updateService,
        ISettingsService settingsService,
        IUiService uiService)
    {
        _kiotaService = kiotaService;
        _updateService = updateService;
        _settingsService = settingsService;
        _uiService = uiService;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGeneratorButtonEnabled))]
    private string _url = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsUrlMode))]
    private bool _isFileMode;

    public bool IsUrlMode => !IsFileMode;

    [ObservableProperty]
    private string _namespace = string.Empty;

    [ObservableProperty]
    private string _clientName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAccessModifierVisible))]
    private string _language = "";

    [ObservableProperty]
    private string _accessModifier = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGeneratorButtonEnabled))]
    [NotifyPropertyChangedFor(nameof(IsUpdateButtonEnabled))]
    [NotifyPropertyChangedFor(nameof(IsRefreshButtonEnabled))]
    private string _destinationFolder = string.Empty;

    [ObservableProperty]
    private string _statusText = string.Empty;

    // App update related
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDownloadAppUpdate))]
    private string _latestVersion = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDownloadAppUpdate))]
    private bool _isUpdateAvailable;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDownloadAppUpdate))]
    private bool _isCheckingUpdate;

    [ObservableProperty]
    private double _downloadProgress; // 0..1

    public bool CanDownloadAppUpdate => IsUpdateAvailable && !IsCheckingUpdate;

    public ObservableCollection<string> Languages { get; } = new(["","C#", "Go", "Java", "Php", "Python", "Ruby",  "Shell", "Swift", "TypeScript"]);
    public ObservableCollection<string> AccessModifiers { get; } = new(["","Public", "Internal", "Protected"]);
    public bool IsAccessModifierVisible => Language == "C#";
    public bool IsGeneratorButtonEnabled => !(string.IsNullOrEmpty(DestinationFolder) || string.IsNullOrEmpty(Url));
    public bool IsUpdateButtonEnabled => !string.IsNullOrEmpty(DestinationFolder);
    public bool IsRefreshButtonEnabled => !string.IsNullOrEmpty(DestinationFolder);

    [RelayCommand]
    private async Task BrowseOpenApiFile()
    {
        var file = await _uiService.OpenFilePickerAsync("Select OpenAPI specification", ["*.json", "*.yaml", "*.yml"]);
        if (file is not null)
        {
            Url = file;
        }
    }

    [RelayCommand]
    private async Task BrowseFolder()
    {
        var folder = await _uiService.OpenFolderPickerAsync("Select destination folder");
        if (folder is not null)
        {
            DestinationFolder = folder;
        }
    }

    [RelayCommand]
    private async Task GenerateClient()
    {
        StatusText = "Generating client...";
        StatusText = await _kiotaService.GenerateClient(Url, Namespace, ClientName,Language, AccessModifier, DestinationFolder, clean: false);
    }

    [RelayCommand]
    private async Task UpdateClient()
    {
        StatusText = "Updating client...";
        StatusText = await _kiotaService.UpdateClient(DestinationFolder);
    }

    [RelayCommand]
    private async Task RefreshClient()
    {
        StatusText = "Refreshing from kiota-lock.json...";
        StatusText = await _kiotaService.RefreshFromLock(DestinationFolder, Language, AccessModifier);
    }

    [RelayCommand]
    private async Task CheckForAppUpdate()
    {
        try
        {
            IsCheckingUpdate = true;
            StatusText = "Checking for app updates...";
            var latest = await _updateService.GetLatestReleaseAsync();
            if (latest is null)
            {
                StatusText = "No suitable release asset found for your platform.";
                IsUpdateAvailable = false;
                LatestVersion = string.Empty;
                return;
            }
            LatestVersion = latest.TagName;
            IsUpdateAvailable = _updateService.IsUpdateAvailable(latest.Version);
            StatusText = IsUpdateAvailable
                ? $"Update available: {latest.TagName} (asset: {latest.AssetName})."
                : "You're on the latest version.";
        }
        catch (Exception ex)
        {
            StatusText = $"Update check failed: {ex.Message}";
            IsUpdateAvailable = false;
        }
        finally
        {
            IsCheckingUpdate = false;
        }
    }

    [RelayCommand]
    private async Task DownloadAndRunUpdate()
    {
        try
        {
            StatusText = "Preparing download...";
            var latest = await _updateService.GetLatestReleaseAsync();
            if (latest is null)
            {
                StatusText = "No suitable release asset found.";
                return;
            }
            if (!_updateService.IsUpdateAvailable(latest.Version))
            {
                StatusText = "Already up to date.";
                return;
            }

            var progress = new Progress<double>(p => { DownloadProgress = p; });
            var zipPath = await _updateService.DownloadAssetAsync(latest.AssetDownloadUrl, progress);
            StatusText = "Download complete. Extracting...";
            var extractedDir = _updateService.ExtractToNewFolder(zipPath, latest.TagName);
            // Hand off to external updater which will copy files into current app folder and relaunch
            var launched = _updateService.StartUpdaterAndExit(extractedDir, () =>
            {
                (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
            });
            if (launched)
            {
                StatusText = "Updater launched. The application will close and restart after updating.";
            }
            else
            {
                StatusText = $"Update extracted to {extractedDir}, but failed to start updater. Please update manually.";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Update failed: {ex.Message}";
        }
    }
}
