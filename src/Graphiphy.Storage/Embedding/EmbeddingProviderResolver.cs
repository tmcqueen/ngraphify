using Microsoft.Extensions.Configuration;

namespace Graphiphy.Storage.Embedding;

/// <summary>
/// Reads a named provider entry from IConfiguration["Providers:{name}"] and constructs
/// an IEmbeddingProvider. The provider name defaults to IConfiguration["Embedding:Provider"].
/// </summary>
public sealed class EmbeddingProviderResolver
{
    private readonly IConfiguration _configuration;
    private readonly Func<string?, string?>? _resolveSecret;

    /// <param name="resolveSecret">
    /// Optional delegate that resolves pass:// and env:// references to their plain-text values.
    /// When provided, string values read from config are passed through this delegate before use.
    /// When null, values are used as-is (suitable if the startup overlay has already resolved them).
    /// </param>
    public EmbeddingProviderResolver(IConfiguration configuration, Func<string?, string?>? resolveSecret = null)
    {
        _configuration = configuration;
        _resolveSecret = resolveSecret;
    }

    // Resolves a config string value through the secret delegate if one is registered.
    private string? R(string? value) => _resolveSecret is null ? value : _resolveSecret(value);

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
                endpoint: R(section["Endpoint"])
                    ?? throw new InvalidOperationException($"Provider '{name}': Endpoint is required for OpenAI-compatible embedding."),
                apiKey: R(section["ApiKey"])
                    ?? throw new InvalidOperationException($"Provider '{name}': ApiKey is required."),
                model: section["Model"] ?? "text-embedding-3-small",
                dimensionSize: dimensions),

            _ => throw new InvalidOperationException(
                $"Unknown ApiType '{apiType}' for embedding provider '{name}'. Currently supported: openai.")
        };
    }
}
