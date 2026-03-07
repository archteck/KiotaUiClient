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
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(HasStatusMessage))]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGeneratorButtonEnabled))]
    [NotifyPropertyChangedFor(nameof(IsUpdateButtonEnabled))]
    [NotifyPropertyChangedFor(nameof(IsRefreshButtonEnabled))]
    [NotifyPropertyChangedFor(nameof(CanDownloadAppUpdate))]
    [NotifyPropertyChangedFor(nameof(IsLoading))]
    private bool _isBusy;

    // App update related
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDownloadAppUpdate))]
    private string _latestVersion = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDownloadAppUpdate))]
    private bool _isUpdateAvailable;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDownloadAppUpdate))]
    [NotifyPropertyChangedFor(nameof(IsLoading))]
    private bool _isCheckingUpdate;

    [ObservableProperty]
    private double _downloadProgress; // 0..1

    public bool CanDownloadAppUpdate => IsUpdateAvailable && !IsCheckingUpdate && !IsBusy;
    public bool IsLoading => IsBusy || IsCheckingUpdate;
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);
    public string StatusText => BuildStatusText();

    public ObservableCollection<string> Languages { get; } = new(["","C#", "Go", "Java", "Php", "Python", "Ruby",  "Shell", "Swift", "TypeScript"]);
    public ObservableCollection<string> AccessModifiers { get; } = new(["","Public", "Internal", "Protected"]);
    public bool IsAccessModifierVisible => Language == "C#";
    public bool IsGeneratorButtonEnabled =>
        !IsBusy && !string.IsNullOrEmpty(DestinationFolder) && !string.IsNullOrEmpty(Url);

    public bool IsUpdateButtonEnabled =>
        !IsBusy && !string.IsNullOrEmpty(DestinationFolder);

    public bool IsRefreshButtonEnabled =>
        !IsBusy && !string.IsNullOrEmpty(DestinationFolder);

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
        await RunBusyOperationAsync("Generating client...", () =>
            _kiotaService.GenerateClient(Url, Namespace, ClientName, Language, AccessModifier, DestinationFolder, clean: false));
    }

    [RelayCommand]
    private async Task UpdateClient()
    {
        await RunBusyOperationAsync("Updating client...", () => _kiotaService.UpdateClient(DestinationFolder));
    }

    [RelayCommand]
    private async Task RefreshClient()
    {
        await RunBusyOperationAsync("Refreshing from kiota-lock.json...", () =>
            _kiotaService.RefreshFromLock(DestinationFolder, Language, AccessModifier));
    }

    [RelayCommand]
    private async Task CheckForAppUpdate()
    {
        if (IsBusy || IsCheckingUpdate)
        {
            return;
        }

        try
        {
            IsCheckingUpdate = true;
            ErrorMessage = string.Empty;
            StatusMessage = "Checking for app updates...";
            var latest = await _updateService.GetLatestReleaseAsync();
            if (latest is null)
            {
                StatusMessage = "No suitable release asset found for your platform.";
                IsUpdateAvailable = false;
                LatestVersion = string.Empty;
                return;
            }
            LatestVersion = latest.TagName;
            IsUpdateAvailable = _updateService.IsUpdateAvailable(latest.Version);
            StatusMessage = IsUpdateAvailable
                ? $"Update available: {latest.TagName} (asset: {latest.AssetName})."
                : "You're on the latest version.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Update check failed: {ex.Message}";
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
        if (IsBusy || IsCheckingUpdate)
        {
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            StatusMessage = "Preparing download...";
            var latest = await _updateService.GetLatestReleaseAsync();
            if (latest is null)
            {
                ErrorMessage = "No suitable release asset found.";
                return;
            }
            if (!_updateService.IsUpdateAvailable(latest.Version))
            {
                StatusMessage = "Already up to date.";
                return;
            }

            var progress = new Progress<double>(p => { DownloadProgress = p; });
            var zipPath = await _updateService.DownloadAssetAsync(latest.AssetDownloadUrl, progress);
            StatusMessage = "Download complete. Extracting...";
            var extractedDir = _updateService.ExtractToNewFolder(zipPath, latest.TagName);
            // Hand off to external updater which will copy files into current app folder and relaunch
            var launched = _updateService.StartUpdaterAndExit(extractedDir, () =>
            {
                (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
            });
            if (launched)
            {
                StatusMessage = "Updater launched. The application will close and restart after updating.";
            }
            else
            {
                ErrorMessage = $"Update extracted to {extractedDir}, but failed to start updater. Please update manually.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Update failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RunBusyOperationAsync(string pendingStatus, Func<Task<string>> operation)
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            StatusMessage = pendingStatus;
            var result = await operation();
            SetResultState(result);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void SetResultState(string result)
    {
        var value = (result ?? string.Empty).Trim();
        if (value.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = value;
            return;
        }

        StatusMessage = value;
    }

    private string BuildStatusText()
    {
        if (string.IsNullOrWhiteSpace(StatusMessage))
        {
            return ErrorMessage;
        }

        if (string.IsNullOrWhiteSpace(ErrorMessage))
        {
            return StatusMessage;
        }

        return $"{StatusMessage}\n{ErrorMessage}";
    }
}
