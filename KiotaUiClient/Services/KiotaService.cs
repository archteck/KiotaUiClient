using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using KiotaUiClient.Models;

namespace KiotaUiClient.Services;

public class KiotaService
{
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

        await EnsureKiotaInstalled();

        destination = NormalizePath(destination);

        // Build arguments
        var arguments = BuildGenerateArguments(url, ns, clientName, languageCommand, destination, clean);

        // Add access modifier for C# if provided
        if (language == "C#" && !string.IsNullOrEmpty(accessModifier))
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
            var languageToUse = string.IsNullOrEmpty(language) ? data.Language : language;
            var accessModifierToUse = string.IsNullOrEmpty(accessModifier) ? data.TypeAccessModifier : accessModifier;

            return await GenerateClient(
                data.DescriptionLocation,
                data.ClientNamespaceName,
                data.ClientClassName,
                languageToUse,
                accessModifierToUse,
                destination,
                true);
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    private async Task EnsureKiotaInstalled()
    {
        var result = await RunCommand("dotnet", "tool list -g");
        if (!result.Contains("Microsoft.OpenApi.Kiota"))
        {
            await RunCommand("dotnet", "tool install --global Microsoft.OpenApi.Kiota");
        }
    }
    private async Task EnsureKiotaUpdated()
    {
            await RunCommand("dotnet", "tool update --global Microsoft.OpenApi.Kiota");
    }

    private async Task<string> RunCommand(string file, params string[] args)
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

        using var proc = Process.Start(psi)!;
        var stdout = await proc.StandardOutput.ReadToEndAsync();
        var stderr = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();

        return string.IsNullOrWhiteSpace(stderr)
            ? stdout
            : $"{stdout}\nERROR:\n{stderr}";
    }

    private List<string> BuildGenerateArguments(
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

    private string NormalizePath(string path)
    {
        return Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
