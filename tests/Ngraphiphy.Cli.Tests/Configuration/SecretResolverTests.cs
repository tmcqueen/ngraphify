using Microsoft.Extensions.Configuration;
using Ngraphiphy.Cli.Configuration.Secrets;

namespace Ngraphiphy.Cli.Tests.Configuration;

public class SecretResolverTests
{
    private sealed class FakeProvider(string value) : ISecretProvider
    {
        public Task<string> ResolveAsync(string _, CancellationToken ct = default) => Task.FromResult(value);
    }

    private static IReadOnlyDictionary<string, ISecretProvider> MakeProviders() =>
        new Dictionary<string, ISecretProvider>
        {
            ["pass"] = new FakeProvider("resolved-pass"),
            ["env"] = new FakeProvider("resolved-env"),
        };

    [Test]
    public async Task ResolveAndOverlayAsync_PassReference_IsReplaced()
    {
        var configBuilder = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Llm:Anthropic:ApiKey"] = "pass://anthropic/key"
            });

        await SecretResolver.ResolveAndOverlayAsync(configBuilder, configBuilder.Build(), MakeProviders());

        var final = configBuilder.Build();
        await Assert.That(final["Llm:Anthropic:ApiKey"]).IsEqualTo("resolved-pass");
    }

    [Test]
    public async Task ResolveAndOverlayAsync_PlainValue_IsUntouched()
    {
        var configBuilder = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Llm:Anthropic:Model"] = "claude-sonnet-4-6"
            });

        await SecretResolver.ResolveAndOverlayAsync(configBuilder, configBuilder.Build(), MakeProviders());

        var final = configBuilder.Build();
        await Assert.That(final["Llm:Anthropic:Model"]).IsEqualTo("claude-sonnet-4-6");
    }

    [Test]
    public async Task ResolveAndOverlayAsync_UnknownScheme_TreatedAsLiteral()
    {
        // "vault://" is not registered → SecretReference.Parse returns IsReference=false
        // → treated as plain string → not touched
        var configBuilder = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SomeKey"] = "vault://some/path"
            });

        await SecretResolver.ResolveAndOverlayAsync(configBuilder, configBuilder.Build(), MakeProviders());

        var final = configBuilder.Build();
        await Assert.That(final["SomeKey"]).IsEqualTo("vault://some/path");
    }

    [Test]
    public async Task ResolveAndOverlayAsync_EnvReference_IsReplaced()
    {
        var configBuilder = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["A2A:AgentUrl"] = "env://A2A_AGENT_URL"
            });

        await SecretResolver.ResolveAndOverlayAsync(configBuilder, configBuilder.Build(), MakeProviders());

        var final = configBuilder.Build();
        await Assert.That(final["A2A:AgentUrl"]).IsEqualTo("resolved-env");
    }

    [Test]
    public async Task ResolveAndOverlayAsync_ProviderThrowsWin32Exception_DoesNotPropagate()
    {
        var throwingProvider = new Win32ThrowingProvider();
        var providers = new Dictionary<string, ISecretProvider>(StringComparer.Ordinal)
        {
            ["pass"] = throwingProvider,
        };
        var snapshot = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Llm:ApiKey"] = "pass://x" })
            .Build();
        var target = new ConfigurationBuilder();

        var act = async () => await SecretResolver.ResolveAndOverlayAsync(target, snapshot, providers);

        await Assert.That(act).ThrowsNothing();
    }

    private sealed class Win32ThrowingProvider : ISecretProvider
    {
        public Task<string> ResolveAsync(string path, CancellationToken ct = default)
            => throw new System.ComponentModel.Win32Exception(2, "No such file or directory");
    }
}
