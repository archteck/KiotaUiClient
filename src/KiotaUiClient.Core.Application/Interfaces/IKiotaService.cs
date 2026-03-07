namespace KiotaUiClient.Core.Application.Interfaces;

public interface IKiotaService
{
    Task<OperationResult> GenerateClient(
        string url,
        string ns,
        string clientName,
        string language,
        string accessModifier,
        string destination,
        bool clean);

    Task<OperationResult> GenerateKiotaClient(
        string url,
        string ns,
        string clientName,
        string language,
        string accessModifier,
        string destination,
        bool clean);

    Task<OperationResult> UpdateClient(string destination);

    Task<OperationResult> RefreshFromLock(
        string destination,
        string language = "",
        string accessModifier = "");

    Task EnsureKiotaInstalled();
    Task EnsureKiotaUpdated();
}
