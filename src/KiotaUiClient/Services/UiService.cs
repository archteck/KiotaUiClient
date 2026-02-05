using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using KiotaUiClient.Core.Application.Interfaces;

namespace KiotaUiClient.Services;

public class UiService : IUiService
{
    private static IStorageProvider? GetStorageProvider()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow?.StorageProvider;
        }
        return null;
    }

    public async Task<string?> OpenFilePickerAsync(string title, string[] extensions)
    {
        var storage = GetStorageProvider();
        if (storage == null) return null;

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType(title) { Patterns = extensions }
            }
        });

        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }

    public async Task<string?> OpenFolderPickerAsync(string title)
    {
        var storage = GetStorageProvider();
        if (storage == null) return null;

        var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        return folders.Count > 0 ? folders[0].Path.LocalPath : null;
    }
}
