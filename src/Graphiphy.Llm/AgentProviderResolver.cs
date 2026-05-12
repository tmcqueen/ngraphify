using Microsoft.Extensions.Configuration;
using QuikGraph;
using Graphiphy.Models;

namespace Graphiphy.Llm;

/// <summary>
/// Reads a named provider entry from IConfiguration["Providers:{name}"] and constructs
/// the appropriate IAgentConfig. The provider name defaults to IConfiguration["Llm:Provider"].
/// </summary>
public sealed class AgentProviderResolver
{
    private readonly IConfiguration _configuration;
    private readonly Func<string?, string?>? _resolveSecret;

    /// <param name="resolveSecret">
    /// Optional delegate that resolves pass:// and env:// references to their plain-text values.
    /// When provided, string values read from config are passed through this delegate before use.
    /// When null, values are used as-is (suitable if the startup overlay has already resolved them).
    /// </param>
    public AgentProviderResolver(IConfiguration configuration, Func<string?, string?>? resolveSecret = null)
    {
        _configuration = configuration;
        _resolveSecret = resolveSecret;
    }

    // Resolves a config string value through the secret delegate if one is registered.
    private string? R(string? value) => _resolveSecret is null ? value : _resolveSecret(value);

    /// <summary>
    /// Resolves the named provider to an <see cref="IAgentConfig"/>.
    /// </summary>
    /// <param name="providerOverride">
    /// Explicit provider name (key in Providers section). When null, reads Llm:Provider from config.
    /// </param>
    public IAgentConfig Resolve(string? providerOverride = null)
    {
        var name = providerOverride
            ?? _configuration["Llm:Provider"]
            ?? throw new InvalidOperationException(
                "No LLM provider specified. Set Llm:Provider in appsettings.json or pass --provider.");

        var section = _configuration.GetSection($"Providers:{name}");
        if (!section.Exists())
            throw new InvalidOperationException(
                $"Provider '{name}' not found in the Providers configuration section.");

        var apiType = (section["ApiType"]
            ?? throw new InvalidOperationException(
                $"Provider '{name}': ApiType is required (e.g. \"anthropic\", \"openai\", \"ollama\", \"copilot\", \"a2a\")."))
            .ToLowerInvariant();

        return apiType switch
        {
            "anthropic" => new AnthropicConfig(
                ApiKey: R(section["ApiKey"])
                    ?? throw new InvalidOperationException($"Provider '{name}': ApiKey is required."),
                Model: section["Model"] ?? "claude-sonnet-4-6",
                MaxTokens: int.TryParse(section["MaxTokens"], out var antMaxTokens) ? antMaxTokens : 4096,
                Endpoint: R(section["Endpoint"])),

            "openai" => new OpenAiConfig(
                ApiKey: R(section["ApiKey"])
                    ?? throw new InvalidOperationException($"Provider '{name}': ApiKey is required."),
                Model: section["Model"] ?? "gpt-4o",
                Endpoint: R(section["Endpoint"]),
                MaxTokens: int.TryParse(section["MaxTokens"], out var oaiMaxTokens) ? oaiMaxTokens : null),

            "ollama" => new OllamaConfig(
                Model: section["Model"] ?? "llama3.2",
                Endpoint: section["Endpoint"] ?? "http://localhost:11434"),

            "copilot" => new CopilotConfig(),

            "a2a" => new A2AConfig(
                AgentUrl: R(section["Endpoint"])
                    ?? throw new InvalidOperationException($"Provider '{name}': Endpoint is required for A2A."),
                ApiKey: string.IsNullOrEmpty(R(section["ApiKey"])) ? null : R(section["ApiKey"])),

            _ => throw new InvalidOperationException(
                $"Unknown ApiType '{apiType}' for provider '{name}'. Valid values: anthropic, openai, ollama, copilot, a2a.")
        };
    }

    /// <summary>
    /// Resolves the provider and creates a ready-to-use <see cref="IGraphAgent"/>.
    /// </summary>
    public async Task<IGraphAgent> CreateAgentAsync(
        BidirectionalGraph<Node, TaggedEdge<Node, Edge>> graph,
        string? providerOverride = null,
        CancellationToken ct = default)
    {
        var config = Resolve(providerOverride);
        return await GraphAgentFactory.CreateAsync(config, graph, ct: ct);
    }
}
