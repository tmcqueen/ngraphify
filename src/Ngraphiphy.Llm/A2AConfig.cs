namespace Ngraphiphy.Llm;

/// <param name="AgentUrl">Base URL of the remote A2A agent (resolves its agent card).</param>
/// <param name="ApiKey">Optional bearer token for authentication.</param>
public sealed record A2AConfig(
    string AgentUrl,
    string? ApiKey = null) : IAgentConfig;
