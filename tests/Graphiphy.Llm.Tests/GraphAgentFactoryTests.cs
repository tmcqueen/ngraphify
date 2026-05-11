// tests/Graphiphy.Llm.Tests/GraphAgentFactoryTests.cs
using Graphiphy.Build;
using Graphiphy.Llm;
using QuikGraph;

namespace Graphiphy.Llm.Tests;

public class GraphAgentFactoryTests
{
    private static BidirectionalGraph<Graphiphy.Models.Node, TaggedEdge<Graphiphy.Models.Node, Graphiphy.Models.Edge>>
        EmptyGraph() => GraphBuilder.Build([]);

    [Test]
    public async Task CreateAsync_OpenAi_ReturnsMafGraphAgent()
    {
        var config = new OpenAiConfig(ApiKey: "sk-fake");
        var agent = await GraphAgentFactory.CreateAsync(config, EmptyGraph());
        await Assert.That(agent).IsNotNull();
        await Assert.That(agent).IsTypeOf<MafGraphAgent>();
    }

    [Test]
    public async Task CreateAsync_Anthropic_ReturnsMafGraphAgent()
    {
        var config = new AnthropicConfig(ApiKey: "sk-ant-fake");
        var agent = await GraphAgentFactory.CreateAsync(config, EmptyGraph());
        await Assert.That(agent).IsNotNull();
        await Assert.That(agent).IsTypeOf<MafGraphAgent>();
    }

    [Test]
    public async Task CreateAsync_Ollama_ReturnsMafGraphAgent()
    {
        var config = new OllamaConfig(Model: "llama3.2");
        var agent = await GraphAgentFactory.CreateAsync(config, EmptyGraph());
        await Assert.That(agent).IsNotNull();
        await Assert.That(agent).IsTypeOf<MafGraphAgent>();
    }

    [Test]
    public async Task CreateAsync_UnknownConfig_ThrowsNotSupported()
    {
        var config = new UnknownConfig();
        await Assert.ThrowsAsync<NotSupportedException>(
            async () => { await GraphAgentFactory.CreateAsync(config, EmptyGraph()); });
    }

    private sealed record UnknownConfig : IAgentConfig;
}
