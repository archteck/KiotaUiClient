using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KiotaUiClient.Services;

namespace KiotaUiClient.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
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
    public ObservableCollection<string> Languages { get; } = new(["","C#", "Go", "Java", "Php", "Python", "Ruby",  "Swift", "TypeScript"]);
    public ObservableCollection<string> AccessModifiers { get; } = new(["","Public", "Internal", "Protected"]);
    public bool IsAccessModifierVisible => Language == "C#";
    public bool IsGeneratorButtonEnabled => !(string.IsNullOrEmpty(DestinationFolder) || string.IsNullOrEmpty(Url));
    public bool IsUpdateButtonEnabled => !string.IsNullOrEmpty(DestinationFolder);
    public bool IsRefreshButtonEnabled => !string.IsNullOrEmpty(DestinationFolder);

    [RelayCommand]
    private async Task BrowseOpenApiFile()
    {
        var window = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (window is null) return;

        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            Title = "Select OpenAPI specification",
            FileTypeFilter = new[]
            {
                new FilePickerFileType("OpenAPI Specs") { Patterns = ["*.json", "*.yaml", "*.yml"] }
            }
        });

        if (files.Count > 0)
        {
            Url = files[0].Path.LocalPath;
        }
    }

    [RelayCommand]
    private async Task BrowseFolder()
    {
        var window = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (window is null) return;

        var folder = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false
        });

        if (folder.Count > 0)
        {
            DestinationFolder = folder[0].Path.LocalPath;
        }
    }

    [RelayCommand]
    private async Task GenerateClient()
    {
        StatusText = "Generating client...";
        StatusText = await KiotaService.GenerateClient(Url, Namespace, ClientName,Language, AccessModifier, DestinationFolder, clean: false);
    }

    [RelayCommand]
    private async Task UpdateClient()
    {
        StatusText = "Updating client...";
        StatusText = await KiotaService.UpdateClient(DestinationFolder);
    }

    [RelayCommand]
    private async Task RefreshClient()
    {
        StatusText = "Refreshing from kiota-lock.json...";
        StatusText = await KiotaService.RefreshFromLock(DestinationFolder, Language, AccessModifier);
    }
}
