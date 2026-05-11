using Ngraphiphy.Storage.Embedding;

namespace Ngraphiphy.Storage.Tests;

public class CloudflareEmbeddingProviderTests
{
    [Test]
    public async Task GetDimensions_ReturnsCorrectDim_ForEachModel()
    {
        var models = new[]
        {
            ("@cf/baai/bge-small-en-v1.5", 384),
            ("@cf/baai/bge-base-en-v1.5", 768),
            ("@cf/baai/bge-large-en-v1.5", 1024),
        };

        foreach (var (model, expectedDims) in models)
        {
            var config = new CloudflareEmbeddingConfig("test-account", "test-token", model);
            await Assert.That(config.GetDimensions()).IsEqualTo(expectedDims);
        }
    }

    [Test]
    public async Task Constructor_SetsCorrectDimensions()
    {
        var config = new CloudflareEmbeddingConfig("test", "test", "@cf/baai/bge-base-en-v1.5");
        var provider = new CloudflareEmbeddingProvider(config);

        await Assert.That(provider.DimensionSize).IsEqualTo(768);
    }

    [Test]
    [Skip("Requires live Cloudflare API token")]
    public async Task EmbedAsync_CallsCloudflareAndReturnsVector()
    {
        var token = Environment.GetEnvironmentVariable("CF_API_TOKEN") ?? "";
        if (string.IsNullOrEmpty(token))
            return;

        var accountId = Environment.GetEnvironmentVariable("CF_ACCOUNT_ID") ?? "";
        var config = new CloudflareEmbeddingConfig(accountId, token);
        var provider = new CloudflareEmbeddingProvider(config);

        var result = await provider.EmbedAsync("test query", CancellationToken.None);

        await Assert.That(result.Length).IsEqualTo(768);
        await Assert.That(result[0]).IsNotEqualTo(0f); // At least one non-zero value
    }
}
