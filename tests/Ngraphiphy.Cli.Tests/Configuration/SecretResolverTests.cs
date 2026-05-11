using Microsoft.Extensions.Configuration;
using Ngraphiphy.Cli.Configuration.Secrets;

namespace Ngraphiphy.Cli.Tests.Configuration;

public class SecretResolverTests
{
    private sealed class FakeProvider(string value) : ISecretProvider
    {
        public string Resolve(string _) => value;
    }

    private static IReadOnlyDictionary<string, ISecretProvider> MakeProviders() =>
        new Dictionary<string, ISecretProvider>
        {
            ["pass"] = new FakeProvider("resolved-pass"),
            ["env"] = new FakeProvider("resolved-env"),
        };

    [Test]
    public async Task ResolveAndOverlay_PassReference_IsReplaced()
    {
        var configBuilder = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Llm:Anthropic:ApiKey"] = "pass://anthropic/key"
            });

        SecretResolver.ResolveAndOverlay(configBuilder, configBuilder.Build(), MakeProviders());

        var final = configBuilder.Build();
        await Assert.That(final["Llm:Anthropic:ApiKey"]).IsEqualTo("resolved-pass");
    }

    [Test]
    public async Task ResolveAndOverlay_PlainValue_IsUntouched()
    {
        var configBuilder = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Llm:Anthropic:Model"] = "claude-sonnet-4-6"
            });

        SecretResolver.ResolveAndOverlay(configBuilder, configBuilder.Build(), MakeProviders());

        var final = configBuilder.Build();
        await Assert.That(final["Llm:Anthropic:Model"]).IsEqualTo("claude-sonnet-4-6");
    }

    [Test]
    public async Task ResolveAndOverlay_UnknownScheme_TreatedAsLiteral()
    {
        // "vault://" is not registered → SecretReference.Parse returns IsReference=false
        // → treated as plain string → not touched
        var configBuilder = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SomeKey"] = "vault://some/path"
            });

        SecretResolver.ResolveAndOverlay(configBuilder, configBuilder.Build(), MakeProviders());

        var final = configBuilder.Build();
        await Assert.That(final["SomeKey"]).IsEqualTo("vault://some/path");
    }

    [Test]
    public async Task ResolveAndOverlay_EnvReference_IsReplaced()
    {
        var configBuilder = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["A2A:AgentUrl"] = "env://A2A_AGENT_URL"
            });

        SecretResolver.ResolveAndOverlay(configBuilder, configBuilder.Build(), MakeProviders());

        var final = configBuilder.Build();
        await Assert.That(final["A2A:AgentUrl"]).IsEqualTo("resolved-env");
    }
}
