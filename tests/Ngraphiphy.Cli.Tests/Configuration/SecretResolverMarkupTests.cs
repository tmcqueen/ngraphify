using Microsoft.Extensions.Configuration;
using Ngraphiphy.Cli.Configuration.Secrets;

namespace Ngraphiphy.Cli.Tests.Configuration;

public class SecretResolverMarkupTests
{
    private sealed class BracketMessageProvider : ISecretProvider
    {
        public string Resolve(string path)
            => throw new InvalidOperationException("connection [host:port] failed");
    }

    [Test]
    public async Task ResolveAndOverlay_ExceptionMessageHasBrackets_DoesNotThrowMarkupException()
    {
        var providers = new Dictionary<string, ISecretProvider>(StringComparer.Ordinal)
        {
            ["pass"] = new BracketMessageProvider(),
        };
        var snapshot = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Llm:Key"] = "pass://x" })
            .Build();
        var target = new ConfigurationBuilder();

        var act = () => SecretResolver.ResolveAndOverlay(target, snapshot, providers);

        await Assert.That(act).ThrowsNothing();
    }
}
