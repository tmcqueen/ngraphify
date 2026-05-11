namespace Ngraphiphy.Cli.Configuration.Options;

public sealed class LlmOptions
{
    public string Provider { get; set; } = "anthropic";
    public AnthropicLlmOptions Anthropic { get; set; } = new();
    public OpenAiLlmOptions OpenAi { get; set; } = new();
    public OllamaLlmOptions Ollama { get; set; } = new();
    public A2ALlmOptions A2A { get; set; } = new();
    // CopilotConfig() takes no parameters — no options class needed.
}

public sealed class AnthropicLlmOptions
{
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "claude-sonnet-4-6";
    public int MaxTokens { get; set; } = 4096;
}

public sealed class OpenAiLlmOptions
{
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "gpt-4o";
}

public sealed class OllamaLlmOptions
{
    public string Model { get; set; } = "llama3.2";
    public string Endpoint { get; set; } = "http://localhost:11434";
}

public sealed class A2ALlmOptions
{
    public string? AgentUrl { get; set; }
    public string? ApiKey { get; set; }
}
