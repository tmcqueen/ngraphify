namespace Ngraphiphy.Llm;

/// <param name="ApiKey">Anthropic API key (sk-ant-...).</param>
/// <param name="Model">Model name, e.g. "claude-sonnet-4-6".</param>
/// <param name="MaxTokens">Maximum tokens in the response.</param>
public sealed record AnthropicConfig(
    string ApiKey,
    string Model = "claude-sonnet-4-6",
    int MaxTokens = 4096) : IAgentConfig;
