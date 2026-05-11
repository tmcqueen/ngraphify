namespace Graphiphy.Llm;

/// <param name="Model">Model name, e.g. "llama3.2".</param>
/// <param name="Endpoint">Ollama base URI (without /v1).</param>
public sealed record OllamaConfig(
    string Model,
    string Endpoint = "http://localhost:11434") : IAgentConfig;
