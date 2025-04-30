using System.Collections.ObjectModel;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using ReactiveUI;
using KiotaUiClient.Services;

namespace KiotaUiClient.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public string Url { get; set; } = string.Empty;
    public string Namespace { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string AccessModifier { get; set; } = "Public";
    public ObservableCollection<string> AccessModifiers { get; } = new(["Public", "Internal", "Protected"]);
    public string DestinationFolder { get; set; } = string.Empty;
    public string StatusText { get; set; } = string.Empty;

    public ReactiveCommand<Unit, Unit> BrowseCommand { get; }
    public ReactiveCommand<Unit, Unit> GenerateCommand { get; }
    public ReactiveCommand<Unit, Unit> UpdateCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

    private readonly KiotaService _kiotaService = new();

    public MainWindowViewModel()
    {
        BrowseCommand = ReactiveCommand.CreateFromTask(BrowseFolder);
        GenerateCommand = ReactiveCommand.CreateFromTask(GenerateClient);
        UpdateCommand = ReactiveCommand.CreateFromTask(UpdateClient);
        RefreshCommand = ReactiveCommand.CreateFromTask(RefreshClient);
    }

    private async Task BrowseFolder()
    {
        var window = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (window is null) return;

        var folder = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false
        });

        if (folder?.Count > 0)
        {
            DestinationFolder = folder[0].Path.LocalPath;
            this.RaisePropertyChanged(nameof(DestinationFolder));
        }
    }

    private async Task GenerateClient()
    {
        StatusText = "Generating client...";
        this.RaisePropertyChanged(nameof(StatusText));
        StatusText = await _kiotaService.GenerateClient(Url, Namespace, ClientName, AccessModifier, DestinationFolder, clean: false);
        this.RaisePropertyChanged(nameof(StatusText));
    }

    private async Task UpdateClient()
    {
        StatusText = "Updating client...";
        this.RaisePropertyChanged(nameof(StatusText));
        StatusText = await _kiotaService.UpdateClient(DestinationFolder);
        this.RaisePropertyChanged(nameof(StatusText));
    }

    private async Task RefreshClient()
    {
        StatusText = "Refreshing from kiota-lock.json...";
        this.RaisePropertyChanged(nameof(StatusText));
        StatusText = await _kiotaService.RefreshFromLock(DestinationFolder);
        this.RaisePropertyChanged(nameof(StatusText));
    }
}
