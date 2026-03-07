using KiotaUiClient.Core.Application.Interfaces;
using KiotaUiClient.ViewModels;
using Xunit;

namespace KiotaUiClient.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public async Task GenerateClientWithInvalidInputSetsValidationError()
    {
        var services = new TestServices();
        var vm = CreateViewModel(services);

        vm.Url = string.Empty;
        vm.DestinationFolder = string.Empty;

        await vm.GenerateClientCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.Contains("Please fix validation errors", vm.ErrorMessage);
        Assert.Equal(0, services.Kiota.GenerateClientCalls);
    }

    [Fact]
    public async Task GenerateClientWithServiceFailureUsesErrorMessageAndDetails()
    {
        var services = new TestServices
        {
            Kiota =
            {
                GenerateClientResult = OperationResult.Failure("Generation failed.", "Bad request")
            }
        };

        var vm = CreateViewModel(services);
        vm.Url = "https://example.com/openapi.json";
        vm.DestinationFolder = CreateTempDirectory();

        await vm.GenerateClientCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.Contains("Generation failed.", vm.ErrorMessage);
        Assert.Contains("Bad request", vm.ErrorMessage);
        Assert.Equal(1, services.Kiota.GenerateClientCalls);
    }

    [Fact]
    public async Task CheckForAppUpdateWithServiceFailureSetsErrorAndClearsAvailability()
    {
        var services = new TestServices
        {
            Update =
            {
                LatestReleaseResult = new OperationResult<ReleaseInfo>(false, default, "Release query failed.", "403")
            }
        };

        var vm = CreateViewModel(services);

        await vm.CheckForAppUpdateCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.False(vm.IsUpdateAvailable);
        Assert.Equal(string.Empty, vm.LatestVersion);
        Assert.Contains("Release query failed.", vm.ErrorMessage);
    }

    [Fact]
    public async Task DownloadAndRunUpdateWithSuccessfulFlowSetsLaunchedStatus()
    {
        var tempZip = Path.Combine(Path.GetTempPath(), $"update-{Guid.NewGuid():N}.zip");
        var extractedDir = CreateTempDirectory();
        var latest = new ReleaseInfo("v9.9.9", "Latest", new Version(9, 9, 9), "linux.zip", "https://example.com/linux.zip");

        var services = new TestServices
        {
            Update =
            {
                LatestReleaseResult = new OperationResult<ReleaseInfo>(true, latest, "ok"),
                IsUpdateAvailableResult = true,
                DownloadResult = new OperationResult<string>(true, tempZip, "downloaded"),
                ExtractResult = new OperationResult<string>(true, extractedDir, "extracted"),
                LaunchResult = OperationResult.Success("launched")
            }
        };

        var vm = CreateViewModel(services);

        await vm.DownloadAndRunUpdateCommand.ExecuteAsync(null);

        Assert.False(vm.HasError);
        Assert.Contains("Updater launched", vm.StatusMessage);
    }

    [Fact]
    public async Task CancelCurrentOperationCancelsInFlightGenerateOperation()
    {
        var services = new TestServices
        {
            Kiota =
            {
                BlockGenerateUntilCanceled = true
            }
        };

        var vm = CreateViewModel(services);
        vm.Url = "https://example.com/openapi.json";
        vm.DestinationFolder = CreateTempDirectory();

        var generateTask = vm.GenerateClientCommand.ExecuteAsync(null);

        var operationStarted = await WaitUntilAsync(() => vm.CanCancelOperation, timeoutMs: 2000);
        Assert.True(operationStarted);

        vm.CancelCurrentOperationCommand.Execute(null);
        await generateTask;

        Assert.False(vm.HasError);
        Assert.Equal("Operation canceled.", vm.StatusMessage);
        Assert.True(services.Kiota.GenerateClientCancellationObserved);
    }

    [Fact]
    public async Task CancelCurrentOperationCancelsInFlightUpdateOperation()
    {
        var services = new TestServices
        {
            Kiota =
            {
                BlockUpdateUntilCanceled = true
            }
        };

        var vm = CreateViewModel(services);
        vm.DestinationFolder = CreateTempDirectory();

        var updateTask = vm.UpdateClientCommand.ExecuteAsync(null);

        var operationStarted = await WaitUntilAsync(() => vm.CanCancelOperation, timeoutMs: 2000);
        Assert.True(operationStarted);

        vm.CancelCurrentOperationCommand.Execute(null);
        await updateTask;

        Assert.False(vm.HasError);
        Assert.Equal("Operation canceled.", vm.StatusMessage);
        Assert.True(services.Kiota.UpdateClientCancellationObserved);
    }

    [Fact]
    public async Task CancelCurrentOperationCancelsInFlightRefreshOperation()
    {
        var services = new TestServices
        {
            Kiota =
            {
                BlockRefreshUntilCanceled = true
            }
        };

        var vm = CreateViewModel(services);
        vm.DestinationFolder = CreateTempDirectory();

        var refreshTask = vm.RefreshClientCommand.ExecuteAsync(null);

        var operationStarted = await WaitUntilAsync(() => vm.CanCancelOperation, timeoutMs: 2000);
        Assert.True(operationStarted);

        vm.CancelCurrentOperationCommand.Execute(null);
        await refreshTask;

        Assert.False(vm.HasError);
        Assert.Equal("Operation canceled.", vm.StatusMessage);
        Assert.True(services.Kiota.RefreshClientCancellationObserved);
    }

    private static MainWindowViewModel CreateViewModel(TestServices services)
    {
        return new MainWindowViewModel(services.Kiota, services.Update, services.Settings, services.Ui);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"kiota-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static async Task<bool> WaitUntilAsync(Func<bool> predicate, int timeoutMs)
    {
        var started = DateTime.UtcNow;
        while ((DateTime.UtcNow - started).TotalMilliseconds < timeoutMs)
        {
            if (predicate())
            {
                return true;
            }

            await Task.Delay(25);
        }

        return predicate();
    }

    private sealed class TestServices
    {
        public FakeKiotaService Kiota { get; } = new();
        public FakeUpdateService Update { get; } = new();
        public FakeSettingsService Settings { get; } = new();
        public FakeUiService Ui { get; } = new();
    }

    private sealed class FakeKiotaService : IKiotaService
    {
        public int GenerateClientCalls { get; private set; }
        public bool GenerateClientCancellationObserved { get; private set; }
        public bool BlockGenerateUntilCanceled { get; set; }
        public bool BlockUpdateUntilCanceled { get; set; }
        public bool UpdateClientCancellationObserved { get; private set; }
        public bool BlockRefreshUntilCanceled { get; set; }
        public bool RefreshClientCancellationObserved { get; private set; }
        public OperationResult GenerateClientResult { get; set; } = OperationResult.Success("Client generated.");

        public Task<OperationResult> GenerateClient(string url, string ns, string clientName, string language, string accessModifier,
            string destination, bool clean, CancellationToken ct = default)
        {
            GenerateClientCalls++;
            if (!BlockGenerateUntilCanceled)
            {
                return Task.FromResult(GenerateClientResult);
            }

            return WaitForCancellationAsync(ct);
        }

        private async Task<OperationResult> WaitForCancellationAsync(CancellationToken ct)
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                return GenerateClientResult;
            }
            catch (OperationCanceledException)
            {
                GenerateClientCancellationObserved = true;
                throw;
            }
        }

        public Task<OperationResult> GenerateKiotaClient(string url, string ns, string clientName, string language, string accessModifier,
            string destination, bool clean, CancellationToken ct = default)
            => Task.FromResult(OperationResult.Success("Generated."));

        public Task<OperationResult> UpdateClient(string destination, CancellationToken ct = default)
        {
            if (!BlockUpdateUntilCanceled)
            {
                return Task.FromResult(OperationResult.Success("Updated."));
            }

            return WaitForUpdateCancellationAsync(ct);
        }

        public Task<OperationResult> RefreshFromLock(string destination, string language = "", string accessModifier = "", CancellationToken ct = default)
        {
            if (!BlockRefreshUntilCanceled)
            {
                return Task.FromResult(OperationResult.Success("Refreshed."));
            }

            return WaitForRefreshCancellationAsync(ct);
        }

        public Task EnsureKiotaInstalled(CancellationToken ct = default) => Task.CompletedTask;

        public Task EnsureKiotaUpdated(CancellationToken ct = default) => Task.CompletedTask;

        private async Task<OperationResult> WaitForUpdateCancellationAsync(CancellationToken ct)
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                return OperationResult.Success("Updated.");
            }
            catch (OperationCanceledException)
            {
                UpdateClientCancellationObserved = true;
                throw;
            }
        }

        private async Task<OperationResult> WaitForRefreshCancellationAsync(CancellationToken ct)
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                return OperationResult.Success("Refreshed.");
            }
            catch (OperationCanceledException)
            {
                RefreshClientCancellationObserved = true;
                throw;
            }
        }
    }

    private sealed class FakeUpdateService : IUpdateService
    {
        public OperationResult<ReleaseInfo> LatestReleaseResult { get; set; } =
            new(false, default, "No release.");

        public bool IsUpdateAvailableResult { get; set; }

        public OperationResult<string> DownloadResult { get; set; } =
            new(false, default, "Download not configured.");

        public OperationResult<string> ExtractResult { get; set; } =
            new(false, default, "Extract not configured.");

        public OperationResult LaunchResult { get; set; } =
            OperationResult.Failure("Launch not configured.");

        public string GetCurrentVersionString() => "1.0.0";

        public Task<OperationResult<ReleaseInfo>> GetLatestReleaseAsync(CancellationToken ct = default)
            => Task.FromResult(LatestReleaseResult);

        public bool IsUpdateAvailable(Version latest) => IsUpdateAvailableResult;

        public Task<OperationResult<string>> DownloadAssetAsync(string url, IProgress<double>? progress = null, CancellationToken ct = default)
            => Task.FromResult(DownloadResult);

        public OperationResult<string> ExtractToNewFolder(string zipPath, string? versionLabel = null)
            => ExtractResult;

        public OperationResult StartUpdaterAndExit(string extractedDir, Action? shutdownAction = null)
            => LaunchResult;
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public Task<double> GetDoubleAsync(string key, double defaultValue) => Task.FromResult(defaultValue);

        public Task SetDoubleAsync(string key, double value) => Task.CompletedTask;
    }

    private sealed class FakeUiService : IUiService
    {
        public Task<string?> OpenFilePickerAsync(string title, string[] extensions) => Task.FromResult<string?>(null);

        public Task<string?> OpenFolderPickerAsync(string title) => Task.FromResult<string?>(null);
    }
}
