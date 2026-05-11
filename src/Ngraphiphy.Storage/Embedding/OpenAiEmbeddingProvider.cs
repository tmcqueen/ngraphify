using OpenAI;
using OpenAI.Embeddings;
using System.ClientModel;

namespace Ngraphiphy.Storage.Embedding;

/// <summary>
/// Embedding provider backed by any OpenAI-compatible embedding endpoint.
/// Works with OpenAI, Cloudflare Workers AI, Azure OpenAI, etc.
/// </summary>
public sealed class OpenAiEmbeddingProvider : IEmbeddingProvider
{
    private readonly OpenAIClient _client;
    private readonly string _model;

    public int DimensionSize { get; }

    public OpenAiEmbeddingProvider(string endpoint, string apiKey, string model, int dimensionSize = 768)
    {
        _model = model;
        DimensionSize = dimensionSize;

        var credential = new ApiKeyCredential(apiKey);
        var options = new OpenAIClientOptions { Endpoint = new Uri(endpoint) };
        _client = new OpenAIClient(credential, options);
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct)
    {
        var client = _client.GetEmbeddingClient(_model);
        var response = await client.GenerateEmbeddingAsync(text, cancellationToken: ct);
        return response.Value.ToFloats().ToArray();
    }

    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct)
    {
        var tasks = texts.Select(t => EmbedAsync(t, ct)).ToList();
        return await Task.WhenAll(tasks);
    }
}
