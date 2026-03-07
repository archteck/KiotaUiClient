using KiotaUiClient.Core.Application.Interfaces;
using KiotaUiClient.Infrastructure.Services;

using Microsoft.Extensions.Logging.Abstractions;

using Xunit;

namespace KiotaUiClient.Tests;

public sealed class KiotaServiceTests
{
    [Fact]
    public async Task GenerateClientReturnsFailureForInvalidLanguage()
    {
        var runner = new FakeProcessRunner();
        var service = new KiotaService(NullLogger<KiotaService>.Instance, runner);

        var result = await service.GenerateClient("https://example.com/openapi.json", "My.Namespace", "MyClient", "NotALanguage", "", "/tmp", clean: false, ct: TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid language.", result.Message);
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public async Task GenerateClientReturnsSuccessWhenRunnerSucceeds()
    {
        var runner = new FakeProcessRunner();
        runner.Responses.Enqueue(new ProcessExecutionResult(0, "Microsoft.OpenApi.Kiota", ""));
        runner.Responses.Enqueue(new ProcessExecutionResult(0, "Client generated successfully\n", ""));

        var service = new KiotaService(NullLogger<KiotaService>.Instance, runner);

        var result = await service.GenerateClient("https://example.com/openapi.json", "My.Namespace", "MyClient", "C#", "Public", "/tmp/out", clean: false, ct: TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("Client generated successfully", result.Message);
        Assert.Equal(2, runner.Calls.Count);
        Assert.Equal("dotnet", runner.Calls[0].File);
        Assert.Equal("kiota", runner.Calls[1].File);
    }

    [Fact]
    public async Task GenerateClientReturnsFailureWhenKiotaCommandFails()
    {
        var runner = new FakeProcessRunner();
        runner.Responses.Enqueue(new ProcessExecutionResult(0, "Microsoft.OpenApi.Kiota", ""));
        runner.Responses.Enqueue(new ProcessExecutionResult(2, "", "invalid input"));

        var service = new KiotaService(NullLogger<KiotaService>.Instance, runner);

        var result = await service.GenerateClient("https://example.com/openapi.json", "My.Namespace", "MyClient", "C#", "Public", "/tmp/out", clean: false, ct: TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Contains("Command 'kiota", result.Message);
        Assert.Equal("invalid input", result.Details);
        Assert.Equal(2, result.ExitCode);
    }

    [Fact]
    public async Task GenerateClientThrowsWhenOperationCanceled()
    {
        var runner = new FakeProcessRunner();
        runner.Responses.Enqueue(new ProcessExecutionResult(0, "Microsoft.OpenApi.Kiota", ""));
        runner.Responses.Enqueue(new OperationCanceledException());

        var service = new KiotaService(NullLogger<KiotaService>.Instance, runner);

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.GenerateClient("https://example.com/openapi.json", "My.Namespace", "MyClient", "C#", "Public", "/tmp/out", clean: false, ct: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GenerateClientReturnsFailureWhenToolInstallFails()
    {
        var runner = new FakeProcessRunner();
        runner.Responses.Enqueue(new ProcessExecutionResult(0, "some-other-tool", ""));
        runner.Responses.Enqueue(new ProcessExecutionResult(1, "", "install denied"));

        var service = new KiotaService(NullLogger<KiotaService>.Instance, runner);

        var result = await service.GenerateClient("https://example.com/openapi.json", "My.Namespace", "MyClient", "C#", "Public", "/tmp/out", clean: false, ct: TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal("Failed to generate client.", result.Message);
        Assert.Contains("Command 'dotnet tool install --global Microsoft.OpenApi.Kiota' failed.", result.Details);
    }

    private sealed class FakeProcessRunner : IProcessRunner
    {
        public Queue<object> Responses { get; } = new();
        public List<ProcessCall> Calls { get; } = new();

        public Task<ProcessExecutionResult> ExecuteAsync(string file, CancellationToken ct = default, params string[] args)
        {
            Calls.Add(new ProcessCall(file, args));

            if (Responses.Count == 0)
            {
                return Task.FromResult(new ProcessExecutionResult(0, string.Empty, string.Empty));
            }

            var next = Responses.Dequeue();
            if (next is Exception ex)
            {
                return Task.FromException<ProcessExecutionResult>(ex);
            }

            return Task.FromResult((ProcessExecutionResult)next);
        }
    }

    private sealed record ProcessCall(string File, string[] Args);
}
