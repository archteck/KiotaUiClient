namespace KiotaUiClient.Core.Application.Interfaces;

public interface IKiotaService
{
    Task<string> GenerateClient(
        string url,
        string ns,
        string clientName,
        string language,
        string accessModifier,
        string destination,
        bool clean);

    Task<string> GenerateKiotaClient(
        string url,
        string ns,
        string clientName,
        string language,
        string accessModifier,
        string destination,
        bool clean);

    Task<string> UpdateClient(string destination);

    Task<string> RefreshFromLock(
        string destination,
        string language = "",
        string accessModifier = "");

    Task EnsureKiotaInstalled();
    Task EnsureKiotaUpdated();
}
