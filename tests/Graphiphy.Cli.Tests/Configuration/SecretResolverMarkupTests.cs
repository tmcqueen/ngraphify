using Microsoft.Extensions.Configuration;
using Graphiphy.Cli.Configuration.Secrets;

namespace Graphiphy.Cli.Tests.Configuration;

public class SecretResolverMarkupTests
{
    private sealed class BracketMessageProvider : ISecretProvider
    {
        public Task<string> ResolveAsync(string path, CancellationToken ct = default)
            => throw new InvalidOperationException("connection [host:port] failed");
    }

    [Test]
    public async Task ResolveAndOverlayAsync_ExceptionMessageHasBrackets_DoesNotThrowMarkupException()
    {
        var providers = new Dictionary<string, ISecretProvider>(StringComparer.Ordinal)
        {
            ["pass"] = new BracketMessageProvider(),
        };
        var snapshot = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Llm:Key"] = "pass://x" })
            .Build();
        var target = new ConfigurationBuilder();

        var act = async () => await SecretResolver.ResolveAndOverlayAsync(target, snapshot, providers);

        await Assert.That(act).ThrowsNothing();
    }
}
