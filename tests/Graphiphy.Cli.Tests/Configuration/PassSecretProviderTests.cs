using Graphiphy.Cli.Configuration.Secrets;

namespace Graphiphy.Cli.Tests.Configuration;

public class PassSecretProviderTests
{
    private sealed class FakeRunner : IProcessRunner
    {
        public required string Stdout { get; init; }
        public string Stderr { get; init; } = "";
        public int ExitCode { get; init; } = 0;
        public int CallCount { get; private set; }

        public Task<(string, string, int)> RunAsync(string exe, IReadOnlyList<string> args, CancellationToken ct)
        {
            CallCount++;
            return Task.FromResult((Stdout, Stderr, ExitCode));
        }
    }

    [Test]
    public async Task ResolveAsync_SuccessfulExit_ReturnsFirstLine()
    {
        var runner = new FakeRunner { Stdout = "my-secret\nsome-metadata\n" };
        var provider = new PassSecretProvider(runner);

        var result = await provider.ResolveAsync("anthropic/api-key");

        await Assert.That(result).IsEqualTo("my-secret");
    }

    [Test]
    public async Task ResolveAsync_NonZeroExit_Throws()
    {
        var runner = new FakeRunner { Stdout = "", ExitCode = 1, Stderr = "Error: not found" };
        var provider = new PassSecretProvider(runner);

        var act = async () => await provider.ResolveAsync("missing/key");

        await Assert.That(act).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task ResolveAsync_SamePath_UsesCache()
    {
        var runner = new FakeRunner { Stdout = "cached-secret\n" };
        var provider = new PassSecretProvider(runner);

        var r1 = await provider.ResolveAsync("some/path");
        var r2 = await provider.ResolveAsync("some/path");

        await Assert.That(r1).IsEqualTo("cached-secret");
        await Assert.That(r2).IsEqualTo("cached-secret");
        await Assert.That(runner.CallCount).IsEqualTo(1);
    }

    [Test]
    public async Task ResolveAsync_TrailingWhitespace_IsTrimmed()
    {
        var runner = new FakeRunner { Stdout = "secret-value  \n" };
        var provider = new PassSecretProvider(runner);

        var result = await provider.ResolveAsync("some/path");

        await Assert.That(result).IsEqualTo("secret-value");
    }

    [Test]
    public async Task ResolveAsync_PathWithSpaces_PassedAsSingleArgument()
    {
        var runner = new ArgCapturingRunner { Stdout = "ok\n" };
        var provider = new PassSecretProvider(runner);

        await provider.ResolveAsync("path with spaces/key");

        await Assert.That(runner.CapturedArgs).IsEquivalentTo(new[] { "show", "path with spaces/key" });
    }

    private sealed class ArgCapturingRunner : IProcessRunner
    {
        public required string Stdout { get; init; }
        public string Stderr { get; init; } = "";
        public int ExitCode { get; init; } = 0;
        public IReadOnlyList<string>? CapturedArgs { get; private set; }

        public Task<(string Stdout, string Stderr, int ExitCode)> RunAsync(
            string executable, IReadOnlyList<string> arguments, CancellationToken ct)
        {
            CapturedArgs = arguments;
            return Task.FromResult((Stdout, Stderr, ExitCode));
        }
    }
}
