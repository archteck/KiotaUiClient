namespace KiotaUiClient.Core.Application.Interfaces;

public interface IProcessRunner
{
    Task<ProcessExecutionResult> ExecuteAsync(string file, CancellationToken ct = default, params string[] args);
}
