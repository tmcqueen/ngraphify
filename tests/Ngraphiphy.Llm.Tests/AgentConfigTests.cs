using Ngraphiphy.Llm;

namespace Ngraphiphy.Llm.Tests;

public class AgentConfigTests
{
    [Test]
    public async Task OpenAiConfig_DefaultModel_IsGpt4o()
    {
        var config = new OpenAiConfig(ApiKey: "sk-fake");
        await Assert.That(config.Model).IsEqualTo("gpt-4o");
    }

    [Test]
    public async Task AnthropicConfig_DefaultModel_IsClaudeSonnet()
    {
        var config = new AnthropicConfig(ApiKey: "sk-ant-fake");
        await Assert.That(config.Model).IsEqualTo("claude-sonnet-4-6");
    }

    [Test]
    public async Task AnthropicConfig_DefaultMaxTokens_Is4096()
    {
        var config = new AnthropicConfig(ApiKey: "sk-ant-fake");
        await Assert.That(config.MaxTokens).IsEqualTo(4096);
    }

    [Test]
    public async Task OllamaConfig_DefaultEndpoint_IsLocalhost()
    {
        var config = new OllamaConfig(Model: "llama3.2");
        await Assert.That(config.Endpoint).IsEqualTo("http://localhost:11434");
    }

    [Test]
    public async Task A2AConfig_NoApiKey_DefaultsToNull()
    {
        var config = new A2AConfig(AgentUrl: "http://localhost:8080");
        await Assert.That(config.ApiKey).IsNull();
    }

    [Test]
    public async Task AllConfigs_ImplementIAgentConfig()
    {
        IAgentConfig[] configs =
        [
            new OpenAiConfig("sk-fake"),
            new AnthropicConfig("sk-ant-fake"),
            new OllamaConfig("llama3.2"),
            new CopilotConfig(),
            new A2AConfig("http://localhost:8080"),
        ];
        await Assert.That(configs.Length).IsEqualTo(5);
    }
}
