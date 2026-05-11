namespace Ngraphiphy.Storage.Embedding;

public sealed record CloudflareEmbeddingConfig(
    string AccountId,
    string ApiToken,
    string Model = "@cf/baai/bge-base-en-v1.5") : IEmbeddingProviderConfig
{
    public int GetDimensions() => Model switch
    {
        "@cf/baai/bge-small-en-v1.5" => 384,
        "@cf/baai/bge-base-en-v1.5" => 768,
        "@cf/baai/bge-large-en-v1.5" => 1024,
        _ => 768
    };
}
