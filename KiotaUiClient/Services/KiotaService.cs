using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using KiotaUiClient.Models;

namespace KiotaUiClient.Services;

public class KiotaService
{
    public async Task<string> GenerateClient(string url, string ns, string clientName, string accessModifier, string destination, bool clean)
    {
        await EnsureKiotaInstalled();
        var args = $"generate -d {url} -n {ns} -c {clientName} --tam {accessModifier} -l Csharp -o \"{destination}\"";
        if (clean) args += " --clean-output";
        return await RunCommand("kiota", args);
    }

    public async Task<string> UpdateClient(string destination)
    {
        await EnsureKiotaInstalled();
        return await RunCommand("kiota", $"update -o \"{destination}\"");
    }

    public async Task<string> RefreshFromLock(string destination)
    {
        try
        {
            var lockPath = Path.Combine(destination, "kiota-lock.json");
            if (!File.Exists(lockPath)) return "kiota-lock.json not found.";

            var json = await File.ReadAllTextAsync(lockPath);
            var data = JsonSerializer.Deserialize<KiotaLock>(json);

            if (data is null || string.IsNullOrWhiteSpace(data.descriptionLocation)) return "Invalid lock file.";

            return await GenerateClient(data.descriptionLocation, data.clientNamespaceName, data.clientClassName, data.typeAccessModifier, destination, true);
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

    private async Task<string> RunCommand(string file, string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = file,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var proc = Process.Start(psi)!;
        var stdout = await proc.StandardOutput.ReadToEndAsync();
        var stderr = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();

        return string.IsNullOrWhiteSpace(stderr) ? stdout : $"{stdout}\nERROR:\n{stderr}";
    }
}
