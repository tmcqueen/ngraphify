using OpenAI;
using OpenAI.Embeddings;
using System.ClientModel;

namespace Ngraphiphy.Storage.Embedding;

public sealed class CloudflareEmbeddingProvider : IEmbeddingProvider
{
    private readonly OpenAIClient _client;
    private readonly CloudflareEmbeddingConfig _config;

    public int DimensionSize { get; }

    public CloudflareEmbeddingProvider(CloudflareEmbeddingConfig config)
    {
        _config = config;
        DimensionSize = config.GetDimensions();

        // Cloudflare Workers AI endpoint (OpenAI-compatible)
        var endpoint = new Uri($"https://api.cloudflare.com/client/v4/accounts/{config.AccountId}/ai/v1/");
        var credential = new ApiKeyCredential(config.ApiToken);
        var options = new OpenAIClientOptions { Endpoint = endpoint };
        _client = new OpenAIClient(credential, options);
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct)
    {
        var client = _client.GetEmbeddingClient(_config.Model);
        var response = await client.GenerateEmbeddingAsync(text, cancellationToken: ct);

        return response.Value.ToFloats().ToArray();
    }

    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct)
    {
        var tasks = texts.Select(text => EmbedAsync(text, ct)).ToList();
        var results = await Task.WhenAll(tasks);
        return results;
    }
}
