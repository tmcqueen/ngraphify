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

        AIAgent agent;
        IDisposable? resource = null;

        switch (config)
        {
            case OpenAiConfig c:
                agent = CreateOpenAi(c, tools);
                break;
            case AnthropicConfig c:
                agent = CreateAnthropic(c, tools);
                break;
            case OllamaConfig c:
                agent = CreateOllama(c, tools);
                break;
            case CopilotConfig c:
                agent = await CreateCopilotAsync(c, tools, ct);
                break;
            case A2AConfig c:
                (agent, resource) = await CreateA2AAsync(c, ct);
                break;
            default:
                throw new NotSupportedException(
                    $"Config type {config.GetType().Name} is not supported");
        }

        return new MafGraphAgent(agent, resource);
    }

    private static ChatClientAgent CreateOpenAi(OpenAiConfig config, IList<AITool> tools)
    {
        OpenAIClient client = config.Endpoint is not null
            ? new OpenAIClient(
                new System.ClientModel.ApiKeyCredential(config.ApiKey),
                new OpenAIClientOptions { Endpoint = new Uri(config.Endpoint) })
            : new OpenAIClient(config.ApiKey);
        var chatClient = client.GetChatClient(config.Model);
        return chatClient.AsAIAgent(Instructions, name: "GraphAnalyst", tools: tools);
    }

    private static ChatClientAgent CreateAnthropic(AnthropicConfig config, IList<AITool> tools)
    {
        var options = new ClientOptions { ApiKey = config.ApiKey };
        if (config.Endpoint is not null)
            options.BaseUrl = config.Endpoint;
        var client = new AnthropicClient(options);
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

    private static async Task<(AIAgent agent, HttpClient? resource)> CreateA2AAsync(
        A2AConfig config, CancellationToken ct)
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
        var agent = await resolver.GetAIAgentAsync(httpClient: httpClient, cancellationToken: ct);
        return (agent, httpClient);
    }
}
