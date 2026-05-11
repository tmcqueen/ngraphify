using Ngraphiphy.Cli.Configuration.Secrets;

namespace Ngraphiphy.Cli.Tests.Configuration;

public class PassSecretProviderTests
{
    private sealed class FakeRunner : IProcessRunner
    {
        public required string Stdout { get; init; }
        public string Stderr { get; init; } = "";
        public int ExitCode { get; init; } = 0;
        public int CallCount { get; private set; }

        public (string, string, int) Run(string exe, string args)
        {
            CallCount++;
            return (Stdout, Stderr, ExitCode);
        }
    }

    [Test]
    public async Task Resolve_SuccessfulExit_ReturnsFirstLine()
    {
        var runner = new FakeRunner { Stdout = "my-secret\nsome-metadata\n" };
        var provider = new PassSecretProvider(runner);

        var result = provider.Resolve("anthropic/api-key");

        await Assert.That(result).IsEqualTo("my-secret");
    }

    [Test]
    public async Task Resolve_NonZeroExit_ThrowsWithPath()
    {
        var runner = new FakeRunner { Stdout = "", ExitCode = 1, Stderr = "Error: not found" };
        var provider = new PassSecretProvider(runner);

        await Assert.That(() => provider.Resolve("missing/key"))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("missing/key");
    }

    [Test]
    public async Task Resolve_SamePath_UsesCache()
    {
        var runner = new FakeRunner { Stdout = "cached-secret\n" };
        var provider = new PassSecretProvider(runner);

        var r1 = provider.Resolve("some/path");
        var r2 = provider.Resolve("some/path");

        await Assert.That(r1).IsEqualTo("cached-secret");
        await Assert.That(r2).IsEqualTo("cached-secret");
        await Assert.That(runner.CallCount).IsEqualTo(1);
    }

    [Test]
    public async Task Resolve_TrailingWhitespace_IsTrimmed()
    {
        var runner = new FakeRunner { Stdout = "secret-value  \n" };
        var provider = new PassSecretProvider(runner);

        var result = provider.Resolve("some/path");

        await Assert.That(result).IsEqualTo("secret-value");
    }
}
