using System.Diagnostics;

namespace KiotaUIUpdater;

internal static class Program
{
    // args: <sourceUpdateDir> <targetAppDir> <mainExePath> <parentPid>
    public static int Main(string[] args)
    {
        try
        {
            if (args.Length < 3)
            {
                Console.Error.WriteLine("Usage: KiotaUIUpdater <sourceUpdateDir> <targetAppDir> <mainExePath> [parentPid]");
                return 2;
            }
            var source = args[0];
            var target = args[1];
            var mainExe = args[2];
            int? parentPid = null;
            if (args.Length >= 4 && int.TryParse(args[3], out var pid)) parentPid = pid;

            // Wait for parent process to exit so files are unlocked
            if (parentPid is int p && p > 0)
            {
                try
                {
                    var proc = Process.GetProcessById(p);
                    if (!proc.HasExited)
                    {
                        proc.WaitForExit(30000); // wait up to 30s
                    }
                }
                catch { /* ignore */ }
            }

            // Copy all files from source to target (overwrite)
            CopyDirectory(source, target);

            // Try to delete update folder to keep clean
            TryDeleteDirectory(source);

            // Relaunch main app
            var psi = new ProcessStartInfo
            {
                FileName = mainExe,
                UseShellExecute = true
            };
            Process.Start(psi);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        if (!Directory.Exists(sourceDir)) throw new DirectoryNotFoundException(sourceDir);
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, file);
            var dest = Path.Combine(destDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            // Ensure we're not trying to overwrite the running updater itself
            try
            {
                File.Copy(file, dest, overwrite: true);
            }
            catch (IOException)
            {
                // retry with slight delay in case file was locked
                Thread.Sleep(200);
                File.Copy(file, dest, overwrite: true);
            }
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch { /* ignore cleanup errors */ }
    }
}
