#pragma warning disable CA1848
using System.Diagnostics;
using System.Text.Json;
using KiotaUiClient.Core.Application.Interfaces;
using KiotaUiClient.Core.Domain.Models;
using Microsoft.Extensions.Logging;

namespace KiotaUiClient.Infrastructure.Services;

public class KiotaService : IKiotaService
{
    private readonly ILogger<KiotaService> _logger;

    private readonly record struct CommandResult(int ExitCode, string StdOut, string StdErr)
    {
        public bool IsSuccess => ExitCode == 0;
    }

    public KiotaService(ILogger<KiotaService> logger)
    {
        _logger = logger;
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

    public async Task<string> GenerateClient(
        string url,
        string ns,
        string clientName,
        string language,
        string accessModifier,
        string destination,
        bool clean)
    {
        // Validate language
        if (!_languageCommands.TryGetValue(language, out var languageCommand))
            return "ERROR: Invalid language.";

        // Validate access modifier for C#
        if (language == "C#" && !string.IsNullOrEmpty(accessModifier) &&
            !_validCSharpAccessModifiers.Contains(accessModifier))
            return "ERROR: Invalid access modifier.";

        return await GenerateKiotaClient(url, ns, clientName, languageCommand, accessModifier, destination, clean);
    }

    public async Task<string> GenerateKiotaClient(
        string url,
        string ns,
        string clientName,
        string language,
        string accessModifier,
        string destination,
        bool clean)
    {
        await EnsureKiotaInstalled();

        destination = NormalizePath(destination);

        // Build arguments
        var arguments = BuildGenerateArguments(url, ns, clientName, language, destination, clean);

        // Add access modifier for C# if provided
        if (language == "csharp" && !string.IsNullOrEmpty(accessModifier))
        {
            arguments.Add("--tam");
            arguments.Add(accessModifier);
        }

        return await RunCommandAndFormatResult("kiota", arguments.ToArray());
    }

    public async Task<string> UpdateClient(string destination)
    {
        await EnsureKiotaInstalled();
        await EnsureKiotaUpdated();
        destination = NormalizePath(destination);

        var arguments = new List<string>
        {
            "update",
            "--cc",
            "-o", destination
        };

        return await RunCommandAndFormatResult("kiota", arguments.ToArray());
    }

    public async Task<string> RefreshFromLock(
        string destination,
        string language = "",
        string accessModifier = "")
    {
        try
        {
            var lockPath = Path.Combine(destination, "kiota-lock.json");
            if (!File.Exists(lockPath))
                return "ERROR: kiota-lock.json not found.";

            var json = await File.ReadAllTextAsync(lockPath);
            var data = JsonSerializer.Deserialize<KiotaLock>(json);

            if (data is null || string.IsNullOrWhiteSpace(data.DescriptionLocation))
                return "ERROR: Invalid lock file.";

            // Use provided values or fall back to values from lock file
            if (GetLanguageToUse(language, data, out var languageToUse, out var refreshFromLock))
            {
                return refreshFromLock;
            }

            if (GetAccessModifierToUse(language, accessModifier, data, out var accessModifierToUse,
                    out var invalidAccessModifier))
            {
                return invalidAccessModifier;
            }

            return await GenerateKiotaClient(
                data.DescriptionLocation,
                data.ClientNamespaceName,
                data.ClientClassName,
                languageToUse!,
                accessModifierToUse,
                destination,
                true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh client from lock file in destination {Destination}", destination);
            return $"ERROR: {ex.Message}";
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
                invalidAccessModifier = "ERROR: Invalid access modifier.";
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
                refreshFromLock = "ERROR: Invalid language.";
                return true;
            }
        }

        return false;
    }

    public async Task EnsureKiotaInstalled()
    {
        var result = await RunCommand("dotnet", "tool", "list", "-g");
        if (!result.StdOut.Contains("Microsoft.OpenApi.Kiota", StringComparison.OrdinalIgnoreCase))
        {
            var installResult = await RunCommand("dotnet", "tool", "install", "--global", "Microsoft.OpenApi.Kiota");
            if (!installResult.IsSuccess)
            {
                var installError = FormatError("dotnet tool install --global Microsoft.OpenApi.Kiota", installResult);
                _logger.LogError("{Error}", installError);
                throw new InvalidOperationException(installError);
            }
        }
    }

    public async Task EnsureKiotaUpdated()
    {
        var result = await RunCommand("dotnet", "tool", "update", "--global", "Microsoft.OpenApi.Kiota");
        if (!result.IsSuccess)
        {
            _logger.LogWarning("Failed to update Kiota tool: {Error}", FormatError("dotnet tool update --global Microsoft.OpenApi.Kiota", result));
        }
    }

    private async Task<CommandResult> RunCommand(string file, params string[] args)
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
            psi.ArgumentList.Add(arg);

        var outputBuilder = new System.Text.StringBuilder();
        var errorBuilder = new System.Text.StringBuilder();

        using var proc = Process.Start(psi);
        if (proc is null)
        {
            _logger.LogError("Failed to start process {FileName}", file);
            return new CommandResult(-1, string.Empty, $"Failed to start process '{file}'.");
        }

        var outputTask = Task.Run(() =>
        {
            using var reader = proc.StandardOutput;
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (line != null)
                {
                    outputBuilder.AppendLine(line);
                }
            }
        });

        var errorTask = Task.Run(() =>
        {
            using var reader = proc.StandardError;
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (line != null)
                {
                    errorBuilder.AppendLine(line);
                }
            }
        });

        await Task.WhenAll(outputTask, errorTask, proc.WaitForExitAsync());

        return new CommandResult(proc.ExitCode, outputBuilder.ToString(), errorBuilder.ToString());
    }

    private static string BuildUserMessage(string command, CommandResult result)
    {
        if (result.IsSuccess)
        {
            if (!string.IsNullOrWhiteSpace(result.StdOut))
            {
                return result.StdOut.Trim();
            }

            if (!string.IsNullOrWhiteSpace(result.StdErr))
            {
                return $"Command completed with warnings.\n{result.StdErr.Trim()}";
            }

            return "Operation completed successfully.";
        }

        return FormatError(command, result);
    }

    private static string FormatError(string command, CommandResult result)
    {
        var details = string.IsNullOrWhiteSpace(result.StdErr) ? result.StdOut : result.StdErr;
        details = string.IsNullOrWhiteSpace(details) ? "No details were provided by the process." : details.Trim();
        return $"ERROR: Command '{command}' failed with exit code {result.ExitCode}.\n{details}";
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

    private async Task<string> RunCommandAndFormatResult(string file, params string[] args)
    {
        var result = await RunCommand(file, args);
        var command = string.Join(' ', new[] { file }.Concat(args));

        if (!result.IsSuccess)
        {
            var error = FormatError(command, result);
            _logger.LogError("{Error}", error);
            return error;
        }

        if (!string.IsNullOrWhiteSpace(result.StdErr))
        {
            _logger.LogWarning("Command {Command} completed with warnings: {Warnings}", command, result.StdErr.Trim());
        }

        return BuildUserMessage(command, result);
    }
}
#pragma warning restore CA1848
