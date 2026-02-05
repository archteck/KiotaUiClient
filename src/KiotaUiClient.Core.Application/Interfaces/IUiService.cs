namespace KiotaUiClient.Core.Application.Interfaces;

public interface IUiService
{
    Task<string?> OpenFilePickerAsync(string title, string[] extensions);
    Task<string?> OpenFolderPickerAsync(string title);
}
