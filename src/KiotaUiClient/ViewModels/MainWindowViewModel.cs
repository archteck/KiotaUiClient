using System.Collections.ObjectModel;
using System.IO;
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
    private CancellationTokenSource? _operationCancellationSource;

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
    [NotifyPropertyChangedFor(nameof(UrlValidationError))]
    [NotifyPropertyChangedFor(nameof(HasUrlValidationError))]
    [NotifyPropertyChangedFor(nameof(IsInputValid))]
    private string _url = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsUrlMode))]
    [NotifyPropertyChangedFor(nameof(UrlValidationError))]
    [NotifyPropertyChangedFor(nameof(HasUrlValidationError))]
    [NotifyPropertyChangedFor(nameof(IsInputValid))]
    [NotifyPropertyChangedFor(nameof(IsGeneratorButtonEnabled))]
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
    [NotifyPropertyChangedFor(nameof(DestinationValidationError))]
    [NotifyPropertyChangedFor(nameof(HasDestinationValidationError))]
    [NotifyPropertyChangedFor(nameof(IsDestinationValid))]
    [NotifyPropertyChangedFor(nameof(IsInputValid))]
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
    [NotifyPropertyChangedFor(nameof(CanCancelOperation))]
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
    public bool CanCancelOperation => IsBusy;
    public bool IsLoading => IsBusy || IsCheckingUpdate;
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);
    public string StatusText => BuildStatusText();
    public string UrlValidationError => ValidateUrl();
    public bool HasUrlValidationError => !string.IsNullOrWhiteSpace(UrlValidationError);
    public string DestinationValidationError => ValidateDestinationFolder();
    public bool HasDestinationValidationError => !string.IsNullOrWhiteSpace(DestinationValidationError);
    public bool IsDestinationValid => !HasDestinationValidationError;
    public bool IsInputValid => !HasUrlValidationError && !HasDestinationValidationError;

    public ObservableCollection<string> Languages { get; } = new(["","C#", "Go", "Java", "Php", "Python", "Ruby",  "Shell", "Swift", "TypeScript"]);
    public ObservableCollection<string> AccessModifiers { get; } = new(["","Public", "Internal", "Protected"]);
    public bool IsAccessModifierVisible => Language == "C#";
    public bool IsGeneratorButtonEnabled =>
        !IsBusy && IsInputValid;

    public bool IsUpdateButtonEnabled =>
        !IsBusy && IsDestinationValid;

    public bool IsRefreshButtonEnabled =>
        !IsBusy && IsDestinationValid;

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
        if (!IsInputValid)
        {
            ErrorMessage = "Please fix validation errors before generating the client.";
            return;
        }

        await RunBusyOperationAsync("Generating client...", ct =>
            _kiotaService.GenerateClient(Url, Namespace, ClientName, Language, AccessModifier, DestinationFolder, clean: false, ct));
    }

    [RelayCommand]
    private async Task UpdateClient()
    {
        if (!IsDestinationValid)
        {
            ErrorMessage = DestinationValidationError;
            return;
        }

        await RunBusyOperationAsync("Updating client...", ct => _kiotaService.UpdateClient(DestinationFolder, ct));
    }

    [RelayCommand]
    private async Task RefreshClient()
    {
        if (!IsDestinationValid)
        {
            ErrorMessage = DestinationValidationError;
            return;
        }

        await RunBusyOperationAsync("Refreshing from kiota-lock.json...", ct =>
            _kiotaService.RefreshFromLock(DestinationFolder, Language, AccessModifier, ct));
    }

    [RelayCommand]
    private void CancelCurrentOperation()
    {
        if (!IsBusy || _operationCancellationSource is null || _operationCancellationSource.IsCancellationRequested)
        {
            return;
        }

        _operationCancellationSource.Cancel();
        ErrorMessage = string.Empty;
        StatusMessage = "Cancellation requested...";
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
            var latestResult = await _updateService.GetLatestReleaseAsync();
            if (!latestResult.IsSuccess || latestResult.Value is null)
            {
                ErrorMessage = string.IsNullOrWhiteSpace(latestResult.Details)
                    ? latestResult.Message
                    : $"{latestResult.Message}\n{latestResult.Details}";
                IsUpdateAvailable = false;
                LatestVersion = string.Empty;
                return;
            }

            var latest = latestResult.Value;
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
            var latestResult = await _updateService.GetLatestReleaseAsync();
            if (!latestResult.IsSuccess || latestResult.Value is null)
            {
                ErrorMessage = string.IsNullOrWhiteSpace(latestResult.Details)
                    ? latestResult.Message
                    : $"{latestResult.Message}\n{latestResult.Details}";
                return;
            }

            var latest = latestResult.Value;
            if (!_updateService.IsUpdateAvailable(latest.Version))
            {
                StatusMessage = "Already up to date.";
                return;
            }

            var progress = new Progress<double>(p => { DownloadProgress = p; });
            var downloadResult = await _updateService.DownloadAssetAsync(latest.AssetDownloadUrl, progress);
            if (!downloadResult.IsSuccess || string.IsNullOrWhiteSpace(downloadResult.Value))
            {
                ErrorMessage = string.IsNullOrWhiteSpace(downloadResult.Details)
                    ? downloadResult.Message
                    : $"{downloadResult.Message}\n{downloadResult.Details}";
                return;
            }

            StatusMessage = "Download complete. Extracting...";
            var extractResult = _updateService.ExtractToNewFolder(downloadResult.Value, latest.TagName);
            if (!extractResult.IsSuccess || string.IsNullOrWhiteSpace(extractResult.Value))
            {
                ErrorMessage = string.IsNullOrWhiteSpace(extractResult.Details)
                    ? extractResult.Message
                    : $"{extractResult.Message}\n{extractResult.Details}";
                return;
            }

            var extractedDir = extractResult.Value;
            // Hand off to external updater which will copy files into current app folder and relaunch
            var launchedResult = _updateService.StartUpdaterAndExit(extractedDir, () =>
            {
                (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
            });
            if (launchedResult.IsSuccess)
            {
                StatusMessage = "Updater launched. The application will close and restart after updating.";
            }
            else
            {
                ErrorMessage = string.IsNullOrWhiteSpace(launchedResult.Details)
                    ? launchedResult.Message
                    : $"{launchedResult.Message}\n{launchedResult.Details}";
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

    private async Task RunBusyOperationAsync(string pendingStatus, Func<CancellationToken, Task<OperationResult>> operation)
    {
        if (IsBusy)
        {
            return;
        }

        using var cts = new CancellationTokenSource();
        _operationCancellationSource = cts;

        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            StatusMessage = pendingStatus;
            var result = await operation(cts.Token);
            SetResultState(result);
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = string.Empty;
            StatusMessage = "Operation canceled.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            _operationCancellationSource = null;
            IsBusy = false;
        }
    }

    private void SetResultState(OperationResult result)
    {
        if (!result.IsSuccess)
        {
            ErrorMessage = string.IsNullOrWhiteSpace(result.Details)
                ? result.Message
                : $"{result.Message}\n{result.Details}";
            return;
        }

        StatusMessage = string.IsNullOrWhiteSpace(result.Details)
            ? result.Message
            : $"{result.Message}\n{result.Details}";
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

    private string ValidateUrl()
    {
        if (string.IsNullOrWhiteSpace(Url))
        {
            return "OpenAPI URL or file path is required.";
        }

        if (IsFileMode)
        {
            return File.Exists(Url) ? string.Empty : "Selected OpenAPI file does not exist.";
        }

        if (!Uri.TryCreate(Url, UriKind.Absolute, out var uri))
        {
            return "OpenAPI URL must be an absolute URL.";
        }

        return uri.Scheme is "http" or "https"
            ? string.Empty
            : "OpenAPI URL must start with http:// or https://.";
    }

    private string ValidateDestinationFolder()
    {
        if (string.IsNullOrWhiteSpace(DestinationFolder))
        {
            return "Destination folder is required.";
        }

        return Directory.Exists(DestinationFolder)
            ? string.Empty
            : "Destination folder does not exist.";
    }
}
