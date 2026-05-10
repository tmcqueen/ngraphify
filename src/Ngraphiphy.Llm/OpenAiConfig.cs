namespace Ngraphiphy.Llm;

/// <param name="ApiKey">OpenAI API key (sk-...).</param>
/// <param name="Model">Model name, e.g. "gpt-4o".</param>
public sealed record OpenAiConfig(
    string ApiKey,
    string Model = "gpt-4o") : IAgentConfig;
