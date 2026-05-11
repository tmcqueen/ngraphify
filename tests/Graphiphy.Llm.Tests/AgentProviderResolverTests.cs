using Microsoft.Extensions.Configuration;
using Graphiphy.Llm;

namespace Graphiphy.Llm.Tests;

public class AgentProviderResolverTests
{
    private static IConfiguration BuildConfig(Dictionary<string, string?> values)
        => new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Test]
    public async Task Resolve_AnthropicProvider_ReturnsAnthropicConfig()
    {
        var config = BuildConfig(new()
        {
            ["Llm:Provider"] = "MyAnthropic",
            ["Providers:MyAnthropic:ApiType"] = "anthropic",
            ["Providers:MyAnthropic:ApiKey"] = "sk-ant-test",
            ["Providers:MyAnthropic:Model"] = "claude-sonnet-4-6",
            ["Providers:MyAnthropic:MaxTokens"] = "2048",
        });
        var resolver = new AgentProviderResolver(config);

        var result = resolver.Resolve();

        var anthropic = result as AnthropicConfig;
        await Assert.That(anthropic).IsNotNull();
        await Assert.That(anthropic!.ApiKey).IsEqualTo("sk-ant-test");
        await Assert.That(anthropic.Model).IsEqualTo("claude-sonnet-4-6");
        await Assert.That(anthropic.MaxTokens).IsEqualTo(2048);
    }

    [Test]
    public async Task Resolve_OpenAiProviderWithEndpoint_ReturnsOpenAiConfigWithEndpoint()
    {
        var config = BuildConfig(new()
        {
            ["Providers:CF:ApiType"] = "openai",
            ["Providers:CF:ApiKey"] = "token-abc",
            ["Providers:CF:Model"] = "@cf/baai/bge-base-en-v1.5",
            ["Providers:CF:Endpoint"] = "https://api.cloudflare.com/client/v4/accounts/123/ai/v1/",
        });
        var resolver = new AgentProviderResolver(config);

        var result = resolver.Resolve("CF");

        var openai = result as OpenAiConfig;
        await Assert.That(openai).IsNotNull();
        await Assert.That(openai!.Endpoint).IsEqualTo("https://api.cloudflare.com/client/v4/accounts/123/ai/v1/");
        await Assert.That(openai.ApiKey).IsEqualTo("token-abc");
    }

    [Test]
    public async Task Resolve_OllamaProvider_ReturnsOllamaConfig()
    {
        var config = BuildConfig(new()
        {
            ["Providers:Local:ApiType"] = "ollama",
            ["Providers:Local:Model"] = "llama3.2",
            ["Providers:Local:Endpoint"] = "http://localhost:11434",
        });
        var resolver = new AgentProviderResolver(config);

        var result = resolver.Resolve("Local");

        var ollama = result as OllamaConfig;
        await Assert.That(ollama).IsNotNull();
        await Assert.That(ollama!.Model).IsEqualTo("llama3.2");
    }

    [Test]
    public async Task Resolve_A2AProvider_UsesEndpointAsAgentUrl()
    {
        var config = BuildConfig(new()
        {
            ["Providers:RemoteAgent:ApiType"] = "a2a",
            ["Providers:RemoteAgent:Endpoint"] = "https://agent.example.com",
            ["Providers:RemoteAgent:ApiKey"] = "bearer-token",
        });
        var resolver = new AgentProviderResolver(config);

        var result = resolver.Resolve("RemoteAgent");

        var a2a = result as A2AConfig;
        await Assert.That(a2a).IsNotNull();
        await Assert.That(a2a!.AgentUrl).IsEqualTo("https://agent.example.com");
        await Assert.That(a2a.ApiKey).IsEqualTo("bearer-token");
    }

    [Test]
    public async Task Resolve_CopilotProvider_ReturnsCopilotConfig()
    {
        var config = BuildConfig(new()
        {
            ["Providers:GHCopilot:ApiType"] = "copilot",
        });
        var resolver = new AgentProviderResolver(config);

        var result = resolver.Resolve("GHCopilot");

        await Assert.That(result).IsTypeOf<CopilotConfig>();
    }

    [Test]
    public async Task Resolve_UnknownProvider_ThrowsInvalidOperationException()
    {
        var config = BuildConfig(new());
        var resolver = new AgentProviderResolver(config);

        var act = () => resolver.Resolve("DoesNotExist");

        await Assert.That(act).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Resolve_NoLlmProviderConfigured_ThrowsInvalidOperationException()
    {
        var config = BuildConfig(new());
        var resolver = new AgentProviderResolver(config);

        var act = () => resolver.Resolve();

        await Assert.That(act).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Resolve_ProviderOverride_IgnoresLlmProvider()
    {
        var config = BuildConfig(new()
        {
            ["Llm:Provider"] = "Anthropic",
            ["Providers:Anthropic:ApiType"] = "anthropic",
            ["Providers:Anthropic:ApiKey"] = "sk-ant-default",
            ["Providers:OpenAI:ApiType"] = "openai",
            ["Providers:OpenAI:ApiKey"] = "sk-openai-override",
            ["Providers:OpenAI:Model"] = "gpt-4o",
        });
        var resolver = new AgentProviderResolver(config);

        var result = resolver.Resolve("OpenAI");

        await Assert.That(result).IsTypeOf<OpenAiConfig>();
    }
}
