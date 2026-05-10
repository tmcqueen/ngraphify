// src/Ngraphiphy.Llm/GraphAgentFactory.cs
using A2A;
using Anthropic;
using Anthropic.Core;
using GitHub.Copilot.SDK;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Ngraphiphy.Models;
using OllamaSharp;
using OpenAI;
using OpenAI.Chat;
using QuikGraph;

namespace Ngraphiphy.Llm;

public static class GraphAgentFactory
{
    private const string Instructions = """
        You are an expert software architect analyzing a knowledge graph of a codebase.
        Use the provided tools to explore the graph and answer questions accurately.
        Be concise. Format code identifiers in backticks.
        """;

    public static async Task<IGraphAgent> CreateAsync(
        IAgentConfig config,
        BidirectionalGraph<Node, TaggedEdge<Node, Edge>> graph,
        CancellationToken ct = default)
    {
        var plugin = new GraphPlugin(graph);
        var tools = new List<AITool>
        {
            AIFunctionFactory.Create(plugin.GetGodNodes),
            AIFunctionFactory.Create(plugin.GetSurprisingConnections),
            AIFunctionFactory.Create(plugin.GetSummaryStats),
            AIFunctionFactory.Create(plugin.SearchNodes),
        };

        AIAgent agent = config switch
        {
            OpenAiConfig c    => CreateOpenAi(c, tools),
            AnthropicConfig c => CreateAnthropic(c, tools),
            OllamaConfig c    => CreateOllama(c, tools),
            CopilotConfig c   => await CreateCopilotAsync(c, tools, ct),
            A2AConfig c       => await CreateA2AAsync(c, ct),
            _                 => throw new NotSupportedException(
                                     $"Config type {config.GetType().Name} is not supported"),
        };

        return new MafGraphAgent(agent);
    }

    private static ChatClientAgent CreateOpenAi(OpenAiConfig config, IList<AITool> tools)
    {
        var chatClient = new OpenAIClient(config.ApiKey).GetChatClient(config.Model);
        return chatClient.AsAIAgent(Instructions, name: "GraphAnalyst", tools: tools);
    }

    private static ChatClientAgent CreateAnthropic(AnthropicConfig config, IList<AITool> tools)
    {
        var client = new AnthropicClient(new ClientOptions { ApiKey = config.ApiKey });
        return client.AsAIAgent(
            model: config.Model,
            instructions: Instructions,
            name: "GraphAnalyst",
            tools: tools,
            defaultMaxTokens: config.MaxTokens);
    }

    private static AIAgent CreateOllama(OllamaConfig config, IList<AITool> tools)
    {
        // OllamaApiClient implements IChatClient; AsAIAgent is the IChatClient extension from Microsoft.Extensions.AI
        var client = new OllamaApiClient(new Uri(config.Endpoint), config.Model);
        return ((IChatClient)client).AsAIAgent(instructions: Instructions, name: "GraphAnalyst", tools: tools);
    }

    private static async Task<AIAgent> CreateCopilotAsync(
        CopilotConfig config, IList<AITool> tools, CancellationToken ct)
    {
        var copilotClient = new CopilotClient();
        await copilotClient.StartAsync(ct);
        return copilotClient.AsAIAgent(
            ownsClient: true,
            name: "GraphAnalyst",
            instructions: Instructions,
            tools: tools);
    }

    private static async Task<AIAgent> CreateA2AAsync(A2AConfig config, CancellationToken ct)
    {
        HttpClient? httpClient = config.ApiKey is not null
            ? new HttpClient
              {
                  DefaultRequestHeaders =
                  {
                      Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                          "Bearer", config.ApiKey)
                  }
              }
            : null;

        var resolver = new A2ACardResolver(new Uri(config.AgentUrl), httpClient);
        return await resolver.GetAIAgentAsync(httpClient: httpClient, cancellationToken: ct);
    }
}
