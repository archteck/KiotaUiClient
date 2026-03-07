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
        bool clean,
        CancellationToken ct = default);

    Task<OperationResult> GenerateKiotaClient(
        string url,
        string ns,
        string clientName,
        string language,
        string accessModifier,
        string destination,
        bool clean,
        CancellationToken ct = default);

    Task<OperationResult> UpdateClient(string destination, CancellationToken ct = default);

    Task<OperationResult> RefreshFromLock(
        string destination,
        string language = "",
        string accessModifier = "",
        CancellationToken ct = default);

    Task EnsureKiotaInstalled(CancellationToken ct = default);
    Task EnsureKiotaUpdated(CancellationToken ct = default);
}
