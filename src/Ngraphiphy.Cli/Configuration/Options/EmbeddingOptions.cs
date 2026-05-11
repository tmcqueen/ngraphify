namespace Ngraphiphy.Cli.Configuration.Options;

public sealed class EmbeddingOptions
{
    public CloudflareEmbeddingProviderOptions Cloudflare { get; set; } = new();
}

public sealed class CloudflareEmbeddingProviderOptions
{
    public string? AccountId { get; set; }
    public string? ApiToken { get; set; }
    public string Model { get; set; } = "@cf/baai/bge-base-en-v1.5";
}
