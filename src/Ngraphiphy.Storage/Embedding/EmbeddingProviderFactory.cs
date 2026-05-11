namespace Ngraphiphy.Storage.Embedding;

public static class EmbeddingProviderFactory
{
    public static IEmbeddingProvider Create(IEmbeddingProviderConfig config) => config switch
    {
        CloudflareEmbeddingConfig cf => new CloudflareEmbeddingProvider(cf),
        _ => throw new ArgumentException($"Unknown embedding config type: {config.GetType().Name}", nameof(config))
    };
}
