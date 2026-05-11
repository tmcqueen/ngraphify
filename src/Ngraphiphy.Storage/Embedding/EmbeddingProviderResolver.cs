using Microsoft.Extensions.Configuration;

namespace Ngraphiphy.Storage.Embedding;

/// <summary>
/// Reads a named provider entry from IConfiguration["Providers:{name}"] and constructs
/// an IEmbeddingProvider. The provider name defaults to IConfiguration["Embedding:Provider"].
/// </summary>
public sealed class EmbeddingProviderResolver
{
    private readonly IConfiguration _configuration;

    public EmbeddingProviderResolver(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Resolves the named provider to an <see cref="IEmbeddingProvider"/>.
    /// </summary>
    public IEmbeddingProvider Resolve(string? providerOverride = null)
    {
        var name = providerOverride
            ?? _configuration["Embedding:Provider"]
            ?? throw new InvalidOperationException(
                "No embedding provider specified. Set Embedding:Provider in appsettings.json or pass --embed-provider.");

        var section = _configuration.GetSection($"Providers:{name}");
        if (!section.Exists())
            throw new InvalidOperationException(
                $"Provider '{name}' not found in the Providers configuration section.");

        var apiType = (section["ApiType"]
            ?? throw new InvalidOperationException(
                $"Provider '{name}': ApiType is required (currently supported: \"openai\")."))
            .ToLowerInvariant();

        int dimensions = 768;
        var dimStr = section["Dimensions"];
        if (dimStr is not null && int.TryParse(dimStr, out var parsed))
            dimensions = parsed;

        return apiType switch
        {
            "openai" => new OpenAiEmbeddingProvider(
                endpoint: section["Endpoint"]
                    ?? throw new InvalidOperationException($"Provider '{name}': Endpoint is required for OpenAI-compatible embedding."),
                apiKey: section["ApiKey"]
                    ?? throw new InvalidOperationException($"Provider '{name}': ApiKey is required."),
                model: section["Model"] ?? "text-embedding-3-small",
                dimensionSize: dimensions),

            _ => throw new InvalidOperationException(
                $"Unknown ApiType '{apiType}' for embedding provider '{name}'. Currently supported: openai.")
        };
    }
}
