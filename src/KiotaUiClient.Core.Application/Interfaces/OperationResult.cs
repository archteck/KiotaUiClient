namespace KiotaUiClient.Core.Application.Interfaces;

public sealed record OperationResult(bool IsSuccess, string Message, string? Details = null, int? ExitCode = null)
{
    public static OperationResult Success(string message, string? details = null)
        => new(true, message, details, null);

    public static OperationResult Failure(string message, string? details = null, int? exitCode = null)
        => new(false, message, details, exitCode);
}

public sealed record OperationResult<T>(bool IsSuccess, T? Value, string Message, string? Details = null, int? ExitCode = null);
