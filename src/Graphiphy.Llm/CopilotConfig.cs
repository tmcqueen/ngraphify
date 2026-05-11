namespace Graphiphy.Llm;

/// <summary>
/// GitHub Copilot provider. Requires GitHub CLI auth (<c>gh auth login</c>)
/// or GITHUB_TOKEN env var to be set before use.
/// </summary>
public sealed record CopilotConfig() : IAgentConfig;
