using System.Diagnostics;
using System.Text.Json;
using KiotaUiClient.Core.Application.Interfaces;
using KiotaUiClient.Core.Domain.Models;
using Microsoft.Extensions.Logging;

namespace KiotaUiClient.Infrastructure.Services;

public class KiotaService : IKiotaService
{
    private readonly ILogger<KiotaService> _logger;

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
            return "Invalid language";

        // Validate access modifier for C#
        if (language == "C#" && !string.IsNullOrEmpty(accessModifier) &&
            !_validCSharpAccessModifiers.Contains(accessModifier))
            return "Invalid accessModifier";

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

        return await RunCommand("kiota", arguments.ToArray());
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

        return await RunCommand("kiota", arguments.ToArray());
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
                return "kiota-lock.json not found.";

            var json = await File.ReadAllTextAsync(lockPath);
            var data = JsonSerializer.Deserialize<KiotaLock>(json);

            if (data is null || string.IsNullOrWhiteSpace(data.DescriptionLocation))
                return "Invalid lock file.";

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
            return $"Error: {ex.Message}";
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
                invalidAccessModifier = "Invalid accessModifier";
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
                refreshFromLock = "Invalid language";
                return true;
            }
        }

        return false;
    }

    public async Task EnsureKiotaInstalled()
    {
        var result = await RunCommand("dotnet", "tool list -g");
        if (!result.Contains("Microsoft.OpenApi.Kiota"))
        {
            await RunCommand("dotnet", "tool install --global Microsoft.OpenApi.Kiota");
        }
    }

    public async Task EnsureKiotaUpdated()
    {
        await RunCommand("dotnet", "tool update --global Microsoft.OpenApi.Kiota");
    }

    private static async Task<string> RunCommand(string file, params string[] args)
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

        using (var proc = Process.Start(psi)!)
        {
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
        }

        var stdout = outputBuilder.ToString();
        var stderr = errorBuilder.ToString();
        return string.IsNullOrWhiteSpace(stderr)
            ? stdout
            : $"{stdout}\nERROR:\n{stderr}";
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
}
