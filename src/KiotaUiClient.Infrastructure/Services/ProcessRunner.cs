using System.Diagnostics;
using KiotaUiClient.Core.Application.Interfaces;

namespace KiotaUiClient.Infrastructure.Services;

public class ProcessRunner : IProcessRunner
{
    public async Task<ProcessExecutionResult> ExecuteAsync(string file, CancellationToken ct = default, params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = file,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        var outputBuilder = new System.Text.StringBuilder();
        var errorBuilder = new System.Text.StringBuilder();

        using var proc = Process.Start(psi);
        if (proc is null)
        {
            return new ProcessExecutionResult(-1, string.Empty, $"Failed to start process '{file}'.");
        }

        var outputTask = Task.Run(() =>
        {
            using var reader = proc.StandardOutput;
            while (!reader.EndOfStream)
            {
                ct.ThrowIfCancellationRequested();
                var line = reader.ReadLine();
                if (line != null)
                {
                    outputBuilder.AppendLine(line);
                }
            }
        }, ct);

        var errorTask = Task.Run(() =>
        {
            using var reader = proc.StandardError;
            while (!reader.EndOfStream)
            {
                ct.ThrowIfCancellationRequested();
                var line = reader.ReadLine();
                if (line != null)
                {
                    errorBuilder.AppendLine(line);
                }
            }
        }, ct);

        try
        {
            await Task.WhenAll(outputTask, errorTask, proc.WaitForExitAsync(ct));
        }
        catch (OperationCanceledException)
        {
            if (!proc.HasExited)
            {
                proc.Kill(true);
            }

            throw;
        }

        return new ProcessExecutionResult(proc.ExitCode, outputBuilder.ToString(), errorBuilder.ToString());
    }
}
