using Graphiphy.Storage.Embedding;

namespace Graphiphy.Storage.Tests;

public class OpenAiEmbeddingProviderTests
{
    [Test]
    public async Task Constructor_SetsCorrectDimensions_WhenProvided()
    {
        var provider = new OpenAiEmbeddingProvider(
            endpoint: "https://api.openai.com/v1",
            apiKey: "test-key",
            model: "text-embedding-3-small",
            dimensionSize: 1536);

        await Assert.That(provider.DimensionSize).IsEqualTo(1536);
    }

    [Test]
    public async Task Constructor_UsesDefaultDimensions_WhenNotProvided()
    {
        var provider = new OpenAiEmbeddingProvider(
            endpoint: "https://api.openai.com/v1",
            apiKey: "test-key",
            model: "text-embedding-3-small");

        await Assert.That(provider.DimensionSize).IsEqualTo(768);
    }

    [Test]
    [Skip("Requires live OpenAI API key")]
    public async Task EmbedAsync_ReturnsVector_WithCorrectDimensions()
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "";
        if (string.IsNullOrEmpty(apiKey))
            return;

        var provider = new OpenAiEmbeddingProvider(
            endpoint: "https://api.openai.com/v1",
            apiKey: apiKey,
            model: "text-embedding-3-small",
            dimensionSize: 1536);

        var result = await provider.EmbedAsync("test query", CancellationToken.None);

        await Assert.That(result.Length).IsEqualTo(1536);
        await Assert.That(result[0]).IsNotEqualTo(0f);
    }
}
