using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using KiotaUiClient.Models;

namespace KiotaUiClient.Services;

public class KiotaService
{
    public async Task<string> GenerateClient(string url, string ns, string clientName, string language,
        string accessModifier,
        string destination, bool clean)
    {
        string languageCommand;
        switch (language)
        {
            case "C#":
                languageCommand = "csharp";
                break;
            case "Go":
                languageCommand = "go";
                break;
            case "Java":
                languageCommand = "java";
                break;
            case "Php":
                languageCommand = "php";
                break;
            case "Python":
                languageCommand = "python";
                break;
            case "Ruby":
                languageCommand = "ruby";
                break;
            case "Shell":
                languageCommand = "shell";
                break;
            case "Swift":
                languageCommand = "swift";
                break;
            case "TypeScript":
                languageCommand = "typescript";
                break;
            default:
                return "Invalid language";
        }
        await EnsureKiotaInstalled();
        destination = Path.GetFullPath(destination)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        // build the args array – no manual quotes needed
        var arguments = new List<string>
        {
            "generate",
            "-d", url,
            "-n", ns,
            "-c", clientName,
            "-l", languageCommand,
            "-o", destination
        };
        if (clean)
        {
            arguments.Add("--clean-output");
        }
        if (language == "C#")
        {
            switch (accessModifier)
            {
                case "Public":
                case "Internal":
                case "Protected":
                    break;
                default:
                    return "Invalid accessModifier";
            }
            arguments.Add("--tam");
            arguments.Add(accessModifier);
        }
        return await RunCommand("kiota", arguments.ToArray());
    }

    public async Task<string> UpdateClient(string destination)
    {
        await EnsureKiotaInstalled();
        destination = Path.GetFullPath(destination)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var arguments = new List<string>
        {
            "update",
            "-o", destination
        };
        return await RunCommand("kiota", arguments.ToArray());
    }

    public async Task<string> RefreshFromLock(string destination)
    {
        try
        {
            var lockPath = Path.Combine(destination, "kiota-lock.json");
            if (!File.Exists(lockPath)) return "kiota-lock.json not found.";

            var json = await File.ReadAllTextAsync(lockPath);
            var data = JsonSerializer.Deserialize<KiotaLock>(json);

            if (data is null || string.IsNullOrWhiteSpace(data.DescriptionLocation)) return "Invalid lock file.";
            
            return await GenerateClient(data.DescriptionLocation, data.ClientNamespaceName, data.ClientClassName, data.Language,
                data.TypeAccessModifier, destination, true);
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

        foreach (var a in args)
            psi.ArgumentList.Add(a);

        using var proc = Process.Start(psi)!;
        var stdout = await proc.StandardOutput.ReadToEndAsync();
        var stderr = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();

        return string.IsNullOrWhiteSpace(stderr)
            ? stdout
            : $"{stdout}\nERROR:\n{stderr}";
    }
}