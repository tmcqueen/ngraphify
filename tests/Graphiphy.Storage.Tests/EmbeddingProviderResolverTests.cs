using Microsoft.Extensions.Configuration;
using Graphiphy.Storage.Embedding;

namespace Graphiphy.Storage.Tests;

public class EmbeddingProviderResolverTests
{
    private static IConfiguration BuildConfig(Dictionary<string, string?> values)
        => new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Test]
    public async Task Resolve_OpenAiCompatibleProvider_ReturnsProviderWithCorrectDimensions()
    {
        var config = BuildConfig(new()
        {
            ["Embedding:Provider"] = "CloudflareAI",
            ["Providers:CloudflareAI:ApiType"] = "openai",
            ["Providers:CloudflareAI:Endpoint"] = "https://api.cloudflare.com/client/v4/accounts/abc/ai/v1/",
            ["Providers:CloudflareAI:ApiKey"] = "test-token",
            ["Providers:CloudflareAI:Model"] = "@cf/baai/bge-base-en-v1.5",
            ["Providers:CloudflareAI:Dimensions"] = "768",
        });
        var resolver = new EmbeddingProviderResolver(config);

        var provider = resolver.Resolve();

        await Assert.That(provider.DimensionSize).IsEqualTo(768);
    }

    [Test]
    public async Task Resolve_ProviderWithNoDimensions_DefaultsTo768()
    {
        var config = BuildConfig(new()
        {
            ["Providers:MyEmbed:ApiType"] = "openai",
            ["Providers:MyEmbed:Endpoint"] = "https://embed.example.com/v1",
            ["Providers:MyEmbed:ApiKey"] = "key",
            ["Providers:MyEmbed:Model"] = "text-embedding-3-small",
        });
        var resolver = new EmbeddingProviderResolver(config);

        var provider = resolver.Resolve("MyEmbed");

        await Assert.That(provider.DimensionSize).IsEqualTo(768);
    }

    [Test]
    public async Task Resolve_MissingEndpoint_Throws()
    {
        var config = BuildConfig(new()
        {
            ["Providers:Bad:ApiType"] = "openai",
            ["Providers:Bad:ApiKey"] = "key",
            ["Providers:Bad:Model"] = "model",
        });
        var resolver = new EmbeddingProviderResolver(config);

        var act = () => resolver.Resolve("Bad");

        await Assert.That(act).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Resolve_MissingProvider_Throws()
    {
        var config = BuildConfig(new());
        var resolver = new EmbeddingProviderResolver(config);

        var act = () => resolver.Resolve("NotHere");

        await Assert.That(act).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Resolve_NoEmbeddingProvider_Throws()
    {
        var config = BuildConfig(new());
        var resolver = new EmbeddingProviderResolver(config);

        var act = () => resolver.Resolve();

        await Assert.That(act).Throws<InvalidOperationException>();
    }
}
