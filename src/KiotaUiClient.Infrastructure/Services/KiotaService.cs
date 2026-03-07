using System.Text.Json;
using KiotaUiClient.Core.Application.Interfaces;
using KiotaUiClient.Core.Domain.Models;
using Microsoft.Extensions.Logging;

namespace KiotaUiClient.Infrastructure.Services;

public partial class KiotaService : IKiotaService
{
    private readonly ILogger<KiotaService> _logger;
    private readonly IProcessRunner _processRunner;

    public KiotaService(ILogger<KiotaService> logger, IProcessRunner processRunner)
    {
        _logger = logger;
        _processRunner = processRunner;
    }
    // Supported languages mapping
    private static readonly Dictionary<string, string> _languageCommands = new()
    {
        { "C#", "csharp" },
        { "Go", "go" },
        { "Java", "java" },
        { "Php", "php" },
        { "Python", "python" },
        { "Ruby", "ruby" },
        { "Shell", "shell" },
        { "Swift", "swift" },
        { "TypeScript", "typescript" }
    };

    // Valid C# access modifiers
    private static readonly HashSet<string> _validCSharpAccessModifiers = new()
    {
        "Public",
        "Internal",
        "Protected"
    };

    public async Task<OperationResult> GenerateClient(
        string url,
        string ns,
        string clientName,
        string language,
        string accessModifier,
        string destination,
        bool clean,
        CancellationToken ct = default)
    {
        // Validate language
        if (!_languageCommands.TryGetValue(language, out var languageCommand))
            return OperationResult.Failure("Invalid language.");

        // Validate access modifier for C#
        if (language == "C#" && !string.IsNullOrEmpty(accessModifier) &&
            !_validCSharpAccessModifiers.Contains(accessModifier))
            return OperationResult.Failure("Invalid access modifier.");

        return await GenerateKiotaClient(url, ns, clientName, languageCommand, accessModifier, destination, clean, ct);
    }

    public async Task<OperationResult> GenerateKiotaClient(
        string url,
        string ns,
        string clientName,
        string language,
        string accessModifier,
        string destination,
        bool clean,
        CancellationToken ct = default)
    {
        try
        {
            await EnsureKiotaInstalled(ct);

            destination = NormalizePath(destination);

            // Build arguments
            var arguments = BuildGenerateArguments(url, ns, clientName, language, destination, clean);

            // Add access modifier for C# if provided
            if (language == "csharp" && !string.IsNullOrEmpty(accessModifier))
            {
                arguments.Add("--tam");
                arguments.Add(accessModifier);
            }

            return await RunCommandAndFormatResult("kiota", ct, arguments.ToArray());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogGenerateClientFailed(ex, destination);
            return OperationResult.Failure("Failed to generate client.", ex.Message);
        }
    }

    public async Task<OperationResult> UpdateClient(string destination, CancellationToken ct = default)
    {
        try
        {
            await EnsureKiotaInstalled(ct);
            await EnsureKiotaUpdated(ct);
            destination = NormalizePath(destination);

            var arguments = new List<string>
            {
                "update",
                "--cc",
                "-o", destination
            };

            return await RunCommandAndFormatResult("kiota", ct, arguments.ToArray());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogUpdateClientFailed(ex, destination);
            return OperationResult.Failure("Failed to update client.", ex.Message);
        }
    }

    public async Task<OperationResult> RefreshFromLock(
        string destination,
        string language = "",
        string accessModifier = "",
        CancellationToken ct = default)
    {
        try
        {
            var lockPath = Path.Combine(destination, "kiota-lock.json");
            if (!File.Exists(lockPath))
                return OperationResult.Failure("kiota-lock.json not found.");

            var json = await File.ReadAllTextAsync(lockPath, ct);
            var data = JsonSerializer.Deserialize<KiotaLock>(json);

            if (data is null || string.IsNullOrWhiteSpace(data.DescriptionLocation))
                return OperationResult.Failure("Invalid lock file.");

            // Use provided values or fall back to values from lock file
            if (GetLanguageToUse(language, data, out var languageToUse, out var refreshFromLock))
            {
                return OperationResult.Failure(refreshFromLock);
            }

            if (GetAccessModifierToUse(language, accessModifier, data, out var accessModifierToUse,
                    out var invalidAccessModifier))
            {
                return OperationResult.Failure(invalidAccessModifier);
            }

            return await GenerateKiotaClient(
                data.DescriptionLocation,
                data.ClientNamespaceName,
                data.ClientClassName,
                languageToUse!,
                accessModifierToUse,
                destination,
                true,
                ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogRefreshFromLockFailed(ex, destination);
            return OperationResult.Failure("Failed to refresh from lock file.", ex.Message);
        }
    }

    private static bool GetAccessModifierToUse(string language, string accessModifier, KiotaLock data,
        out string accessModifierToUse, out string invalidAccessModifier)
    {
        accessModifierToUse = "";
        invalidAccessModifier = "";
        if (string.IsNullOrEmpty(accessModifier))
        {
            accessModifierToUse = data.TypeAccessModifier;
        }
        else
        {
            if (language == "csharp" && !_validCSharpAccessModifiers.Contains(accessModifier))
            {
                invalidAccessModifier = "Invalid access modifier.";
                return true;
            }
        }

        return false;
    }

    private static bool GetLanguageToUse(string language, KiotaLock data, out string? languageToUse,
        out string refreshFromLock)
    {
        languageToUse = "";
        refreshFromLock = "";
        if (string.IsNullOrEmpty(language))
        {
            languageToUse = data.Language.ToLowerInvariant();
        }
        else
        {
            if (!_languageCommands.TryGetValue(language, out languageToUse))
            {
                refreshFromLock = "Invalid language.";
                return true;
            }
        }

        return false;
    }

    public async Task EnsureKiotaInstalled(CancellationToken ct = default)
    {
        var result = await _processRunner.ExecuteAsync("dotnet", ct, "tool", "list", "-g");
        if (!result.StdOut.Contains("Microsoft.OpenApi.Kiota", StringComparison.OrdinalIgnoreCase))
        {
            var installResult = await _processRunner.ExecuteAsync("dotnet", ct, "tool", "install", "--global", "Microsoft.OpenApi.Kiota");
            if (!installResult.IsSuccess)
            {
                var installFailure = BuildFailureResult("dotnet tool install --global Microsoft.OpenApi.Kiota", installResult);
                LogKiotaInstallFailed(installFailure.Message, installFailure.Details ?? string.Empty);
                throw new InvalidOperationException(installFailure.Message);
            }
        }
    }

    public async Task EnsureKiotaUpdated(CancellationToken ct = default)
    {
        var result = await _processRunner.ExecuteAsync("dotnet", ct, "tool", "update", "--global", "Microsoft.OpenApi.Kiota");
        if (!result.IsSuccess)
        {
            var updateFailure = BuildFailureResult("dotnet tool update --global Microsoft.OpenApi.Kiota", result);
            LogKiotaUpdateFailed(updateFailure.Message, updateFailure.Details ?? string.Empty);
        }
    }

    private static OperationResult BuildResult(string command, ProcessExecutionResult result)
    {
        if (result.IsSuccess)
        {
            if (!string.IsNullOrWhiteSpace(result.StdOut))
            {
                return OperationResult.Success(result.StdOut.Trim());
            }

            if (!string.IsNullOrWhiteSpace(result.StdErr))
            {
                return OperationResult.Success("Command completed with warnings.", result.StdErr.Trim());
            }

            return OperationResult.Success("Operation completed successfully.");
        }

        return BuildFailureResult(command, result);
    }

    private static OperationResult BuildFailureResult(string command, ProcessExecutionResult result)
    {
        var details = string.IsNullOrWhiteSpace(result.StdErr) ? result.StdOut : result.StdErr;
        details = string.IsNullOrWhiteSpace(details) ? "No details were provided by the process." : details.Trim();
        return OperationResult.Failure($"Command '{command}' failed.", details, result.ExitCode);
    }

    private static List<string> BuildGenerateArguments(
        string url,
        string ns,
        string clientName,
        string languageCommand,
        string destination,
        bool clean)
    {
        var arguments = new List<string>
        {
            "generate",
            "-d", url,
            "-n", ns,
            "-c", clientName,
            "-l", languageCommand,
            "-o", destination,
            "--cc"
        };

        if (clean)
        {
            arguments.Add("--clean-output");
        }

        return arguments;
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private async Task<OperationResult> RunCommandAndFormatResult(string file, CancellationToken ct, params string[] args)
    {
        var result = await _processRunner.ExecuteAsync(file, ct, args);
        var command = string.Join(' ', new[] { file }.Concat(args));

        if (result.ExitCode == -1)
        {
            LogProcessStartFailed(file);
        }

        if (!result.IsSuccess)
        {
            var failure = BuildFailureResult(command, result);
            LogCommandFailed(failure.Message, failure.Details ?? string.Empty);
            return failure;
        }

        if (!string.IsNullOrWhiteSpace(result.StdErr))
        {
            LogCommandCompletedWithWarnings(command, result.StdErr.Trim());
        }

        return BuildResult(command, result);
    }

    [LoggerMessage(EventId = 1001, Level = LogLevel.Error,
        Message = "Failed to refresh client from lock file in destination {Destination}")]
    private partial void LogRefreshFromLockFailed(Exception ex, string destination);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Error,
        Message = "Kiota install command failed: {Message} Details: {Details}")]
    private partial void LogKiotaInstallFailed(string message, string details);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Warning,
        Message = "Kiota update command failed: {Message} Details: {Details}")]
    private partial void LogKiotaUpdateFailed(string message, string details);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Error,
        Message = "Failed to start process {FileName}")]
    private partial void LogProcessStartFailed(string fileName);

    [LoggerMessage(EventId = 1005, Level = LogLevel.Error,
        Message = "Command execution failed: {Message} Details: {Details}")]
    private partial void LogCommandFailed(string message, string details);

    [LoggerMessage(EventId = 1006, Level = LogLevel.Warning,
        Message = "Command {Command} completed with warnings: {Warnings}")]
    private partial void LogCommandCompletedWithWarnings(string command, string warnings);

    [LoggerMessage(EventId = 1007, Level = LogLevel.Error,
        Message = "Generate client failed for destination {Destination}")]
    private partial void LogGenerateClientFailed(Exception ex, string destination);

    [LoggerMessage(EventId = 1008, Level = LogLevel.Error,
        Message = "Update client failed for destination {Destination}")]
    private partial void LogUpdateClientFailed(Exception ex, string destination);
}
