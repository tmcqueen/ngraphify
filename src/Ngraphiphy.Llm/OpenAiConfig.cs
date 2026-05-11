namespace Ngraphiphy.Llm;

/// <param name="ApiKey">OpenAI API key (sk-...) or compatible provider key.</param>
/// <param name="Model">Model name, e.g. "gpt-4o".</param>
/// <param name="Endpoint">Custom base URL for OpenAI-compatible endpoints. Null uses default OpenAI endpoint.</param>
/// <param name="MaxTokens">Maximum tokens in the response. Null uses provider default.</param>
public sealed record OpenAiConfig(
    string ApiKey,
    string Model = "gpt-4o",
    string? Endpoint = null,
    int? MaxTokens = null) : IAgentConfig;
