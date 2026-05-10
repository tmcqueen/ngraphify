# Ngraphiphy Phase 2 (v2): MAF LLM Backends, Pipeline, CLI, and MCP Server

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Semantic Kernel with Microsoft Agent Framework v1.5.0, add provider-specific configs for OpenAI/Anthropic/Ollama/Copilot/A2A, implement the full pipeline library, Spectre.Console CLI commands, and a ModelContextProtocol MCP stdio server.

**Architecture:** `Ngraphiphy.Llm` (already scaffolded with SK) gets SK replaced with MAF; `IGraphAgent` becomes session-aware via a `GraphSession` wrapper; `GraphPlugin` uses plain methods with `[Description]` attributes registered via `AIFunctionFactory.Create()`. `Ngraphiphy.Pipeline` (new library) holds the full Detect→Extract→Dedup→Cluster→Report pipeline. `Ngraphiphy.Cli` gets its stub commands implemented and gains an MCP stdio server via `ModelContextProtocol` 1.3.0.

**Tech Stack:**
- Microsoft Agent Framework 1.5.0 — `Microsoft.Agents.AI`, `Microsoft.Agents.AI.OpenAI` (stable); `Microsoft.Agents.AI.Anthropic`, `.A2A`, `.GitHub.Copilot` (preview)
- OllamaSharp 5.4.8 — Ollama local models (implements `IChatClient`)
- Spectre.Console.Cli 0.55.0 — CLI (already in project)
- ModelContextProtocol 1.3.0 — MCP server (already in project)
- TUnit 1.43.41 — all tests

---

**Before starting Task 1: clean up dead worktrees from aborted prior work**
```bash
cd /home/timm/ngraphiphy
git worktree remove /home/timm/ngraphiphy-p2t3 --force 2>/dev/null; true
git worktree remove /home/timm/ngraphiphy-p2t6 --force 2>/dev/null; true
git branch -D p2/task3-kernel-factory p2/task6-pipeline 2>/dev/null; true
```

---

## File Structure

```
src/
  Ngraphiphy.Llm/                         ← MODIFY throughout Tasks 1-3
    Ngraphiphy.Llm.csproj                 ← replace SK 1.75.0 with MAF packages
    IGraphAgent.cs                        ← rewrite: session-aware interface
    GraphSession.cs                       ← NEW: wraps AgentSession
    GraphPlugin.cs                        ← rewrite: plain methods + [Description], no SK attrs
    IAgentConfig.cs                       ← NEW: marker interface
    OpenAiConfig.cs                       ← NEW
    AnthropicConfig.cs                    ← NEW
    OllamaConfig.cs                       ← NEW
    CopilotConfig.cs                      ← NEW
    A2AConfig.cs                          ← NEW
    MafGraphAgent.cs                      ← NEW: IGraphAgent impl wrapping AIAgent
    GraphAgentFactory.cs                  ← NEW: async factory, 5 providers

  Ngraphiphy.Pipeline/                    ← NEW library (Task 4)
    Ngraphiphy.Pipeline.csproj
    RepositoryAnalysis.cs
    GraphTools.cs

  Ngraphiphy.Cli/                         ← MODIFY: implement stub commands (Tasks 5-7)
    Commands/
      AnalyzeCommand.cs
      ReportCommand.cs
      QueryCommand.cs
      ServeCommand.cs
    Mcp/
      McpGraphToolsWrapper.cs             ← NEW
      GraphMcpServer.cs                   ← NEW

tests/
  Ngraphiphy.Llm.Tests/
    GraphPluginTests.cs                   ← rewrite: plain method calls, no SK
    AgentConfigTests.cs                   ← NEW
    GraphAgentFactoryTests.cs             ← NEW
  Ngraphiphy.Cli.Tests/
    Pipeline/RepositoryAnalysisTests.cs   ← NEW
    Commands/AnalyzeCommandTests.cs       ← NEW
    Mcp/GraphToolsTests.cs                ← NEW
    Mcp/McpToolDescriptionTests.cs        ← NEW

docs/
  mcp-config.md                           ← NEW (Task 8)
```

---

## Task 1: Replace Ngraphiphy.Llm — swap SK for MAF, update IGraphAgent

**Context:** Main branch has `src/Ngraphiphy.Llm/` with SK 1.75.0. This task replaces csproj packages, rewrites `IGraphAgent` to be session-aware, adds `GraphSession`, rewrites `GraphPlugin` with plain methods, and rewrites `GraphPluginTests`. Provider configs and the agent implementation come in Tasks 2 and 3.

**Files:**
- Modify: `src/Ngraphiphy.Llm/Ngraphiphy.Llm.csproj`
- Rewrite: `src/Ngraphiphy.Llm/IGraphAgent.cs`
- Create: `src/Ngraphiphy.Llm/GraphSession.cs`
- Rewrite: `src/Ngraphiphy.Llm/GraphPlugin.cs`
- Rewrite: `tests/Ngraphiphy.Llm.Tests/GraphPluginTests.cs`

**How to run tests:** `dotnet run --project tests/Ngraphiphy.Llm.Tests/`

- [ ] **Step 1: Clean up dead worktrees**

```bash
cd /home/timm/ngraphiphy
git worktree remove /home/timm/ngraphiphy-p2t3 --force 2>/dev/null; true
git worktree remove /home/timm/ngraphiphy-p2t6 --force 2>/dev/null; true
git branch -D p2/task3-kernel-factory p2/task6-pipeline 2>/dev/null; true
```

Expected: No errors (or "not found" messages are fine).

- [ ] **Step 2: Replace Ngraphiphy.Llm.csproj**

```xml
<!-- src/Ngraphiphy.Llm/Ngraphiphy.Llm.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>Ngraphiphy.Llm</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\Ngraphiphy\Ngraphiphy.csproj" />
  </ItemGroup>

  <ItemGroup>
    <!-- MAF stable releases -->
    <PackageReference Include="Microsoft.Agents.AI" Version="1.5.0" />
    <PackageReference Include="Microsoft.Agents.AI.OpenAI" Version="1.5.0" />
    <!-- Ollama: OllamaApiClient implements IChatClient, AsAIAgent() comes from Microsoft.Agents.AI -->
    <PackageReference Include="OllamaSharp" Version="5.4.8" />
  </ItemGroup>
</Project>
```

Then add the three preview packages (this writes the exact resolved version into the csproj):
```bash
cd /home/timm/ngraphiphy
dotnet add src/Ngraphiphy.Llm/Ngraphiphy.Llm.csproj package Microsoft.Agents.AI.Anthropic --prerelease
dotnet add src/Ngraphiphy.Llm/Ngraphiphy.Llm.csproj package Microsoft.Agents.AI.A2A --prerelease
dotnet add src/Ngraphiphy.Llm/Ngraphiphy.Llm.csproj package Microsoft.Agents.AI.GitHub.Copilot --prerelease
```

Verify build:
```bash
dotnet build src/Ngraphiphy.Llm/
```
Expected: Build succeeded.

- [ ] **Step 3: Rewrite IGraphAgent.cs**

```csharp
// src/Ngraphiphy.Llm/IGraphAgent.cs
using Ngraphiphy.Models;
using QuikGraph;

namespace Ngraphiphy.Llm;

/// <summary>
/// Session-aware LLM agent that answers questions about a repository knowledge graph.
/// Create one agent per repository (graph is fixed at construction), then create
/// sessions per conversation to maintain independent message history.
/// </summary>
public interface IGraphAgent : IAsyncDisposable
{
    /// <summary>Creates a new independent conversation session.</summary>
    Task<GraphSession> CreateSessionAsync(CancellationToken ct = default);

    /// <summary>Ask a question, maintaining history within the given session.</summary>
    Task<string> AnswerAsync(string question, GraphSession session, CancellationToken ct = default);

    /// <summary>Produce a 3-5 bullet-point summary using the given session.</summary>
    Task<string> SummarizeAsync(GraphSession session, CancellationToken ct = default);

    /// <summary>Stream the answer token by token.</summary>
    IAsyncEnumerable<string> AnswerStreamingAsync(
        string question, GraphSession session, CancellationToken ct = default);
}
```

- [ ] **Step 4: Create GraphSession.cs**

```csharp
// src/Ngraphiphy.Llm/GraphSession.cs
using Microsoft.Agents.AI;

namespace Ngraphiphy.Llm;

/// <summary>
/// Represents a single conversation with an <see cref="IGraphAgent"/>.
/// Maintains message history across multiple AnswerAsync calls.
/// Dispose when the conversation is complete.
/// </summary>
public sealed class GraphSession : IAsyncDisposable
{
    internal AgentSession AgentSession { get; }

    internal GraphSession(AgentSession agentSession) => AgentSession = agentSession;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

- [ ] **Step 5: Rewrite GraphPlugin.cs**

```csharp
// src/Ngraphiphy.Llm/GraphPlugin.cs
using System.ComponentModel;
using System.Text.Json;
using Ngraphiphy.Analysis;
using Ngraphiphy.Models;
using QuikGraph;

namespace Ngraphiphy.Llm;

/// <summary>
/// Graph query methods registered as AITool functions via AIFunctionFactory.Create().
/// [Description] attributes on methods and parameters become LLM tool documentation.
/// No Semantic Kernel dependency — this class has no special base class or attributes.
/// </summary>
public sealed class GraphPlugin
{
    private readonly BidirectionalGraph<Node, TaggedEdge<Node, Edge>> _graph;

    public GraphPlugin(BidirectionalGraph<Node, TaggedEdge<Node, Edge>> graph) => _graph = graph;

    [Description("Return the most connected nodes (god nodes) in the graph as JSON.")]
    public string GetGodNodes(
        [Description("Maximum number of nodes to return")] int topN = 5)
    {
        var nodes = GraphAnalyzer.GodNodes(_graph, topN);
        return JsonSerializer.Serialize(nodes.Select(n => new
        {
            n.Id, n.Label, n.SourceFile,
            Connections = _graph.InDegree(n) + _graph.OutDegree(n),
        }));
    }

    [Description("Return the most surprising cross-file or high-confidence edges as JSON.")]
    public string GetSurprisingConnections(
        [Description("Maximum connections to return")] int topN = 10)
    {
        var connections = GraphAnalyzer.SurprisingConnections(_graph, topN);
        return JsonSerializer.Serialize(connections.Select(c => new
        {
            Source = c.Source.Label,
            Target = c.Target.Label,
            c.Edge.Relation,
            c.Edge.ConfidenceString,
            c.Score,
        }));
    }

    [Description("Return summary statistics about the graph as JSON.")]
    public string GetSummaryStats()
    {
        var byFile = _graph.Vertices
            .GroupBy(n => n.SourceFile)
            .Select(g => new { File = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count).Take(10);
        return JsonSerializer.Serialize(new
        {
            NodeCount = _graph.VertexCount,
            EdgeCount = _graph.EdgeCount,
            TopFiles = byFile,
        });
    }

    [Description("Search for nodes whose label contains the given query string. Returns JSON.")]
    public string SearchNodes(
        [Description("Search term to match against node labels")] string query)
    {
        var matches = _graph.Vertices
            .Where(n => n.Label.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(20)
            .Select(n => new { n.Id, n.Label, n.SourceFile, n.FileTypeString });
        return JsonSerializer.Serialize(matches);
    }
}
```

- [ ] **Step 6: Rewrite GraphPluginTests.cs**

```csharp
// tests/Ngraphiphy.Llm.Tests/GraphPluginTests.cs
using Ngraphiphy.Build;
using Ngraphiphy.Llm;
using QuikGraph;
using ExtractionModel = Ngraphiphy.Models.Extraction;

namespace Ngraphiphy.Llm.Tests;

public class GraphPluginTests
{
    private static BidirectionalGraph<Ngraphiphy.Models.Node, TaggedEdge<Ngraphiphy.Models.Node, Ngraphiphy.Models.Edge>> MakeGraph()
    {
        var ext = new ExtractionModel
        {
            Nodes =
            [
                new() { Id = "a::Hub",   Label = "Hub",   FileTypeString = "code", SourceFile = "a.py" },
                new() { Id = "a::Spoke", Label = "Spoke", FileTypeString = "code", SourceFile = "a.py" },
                new() { Id = "b::Other", Label = "Other", FileTypeString = "code", SourceFile = "b.py" },
            ],
            Edges =
            [
                new() { Source = "a::Hub", Target = "a::Spoke", Relation = "calls",
                        ConfidenceString = "EXTRACTED", SourceFile = "a.py" },
                new() { Source = "a::Hub", Target = "b::Other", Relation = "calls",
                        ConfidenceString = "AMBIGUOUS", SourceFile = "a.py" },
            ]
        };
        return GraphBuilder.Build([ext]);
    }

    [Test]
    public async Task GetGodNodes_ContainsHub()
    {
        var plugin = new GraphPlugin(MakeGraph());
        var result = plugin.GetGodNodes(topN: 1);
        await Assert.That(result).IsNotNullOrEmpty();
        await Assert.That(result).Contains("Hub");
    }

    [Test]
    public async Task GetSurprisingConnections_ReturnsNonEmptyJson()
    {
        var plugin = new GraphPlugin(MakeGraph());
        var result = plugin.GetSurprisingConnections(topN: 5);
        await Assert.That(result).IsNotNullOrEmpty();
    }

    [Test]
    public async Task GetSummaryStats_ContainsNodeAndEdgeCounts()
    {
        var plugin = new GraphPlugin(MakeGraph());
        var result = plugin.GetSummaryStats();
        await Assert.That(result).Contains("3");  // NodeCount
        await Assert.That(result).Contains("2");  // EdgeCount
    }

    [Test]
    public async Task SearchNodes_ReturnsMatchAndExcludesNonMatch()
    {
        var plugin = new GraphPlugin(MakeGraph());
        var result = plugin.SearchNodes("Hub");
        await Assert.That(result).Contains("Hub");
        await Assert.That(result).DoesNotContain("Spoke");
    }
}
```

- [ ] **Step 7: Run tests**

```bash
dotnet run --project tests/Ngraphiphy.Llm.Tests/
```
Expected: 4 tests pass.

- [ ] **Step 8: Commit**

```bash
cd /home/timm/ngraphiphy
git add src/Ngraphiphy.Llm/ tests/Ngraphiphy.Llm.Tests/GraphPluginTests.cs
git commit -m "feat: replace Semantic Kernel with Microsoft Agent Framework in Ngraphiphy.Llm"
```

---

## Task 2: Provider config types

**Files:**
- Create: `src/Ngraphiphy.Llm/IAgentConfig.cs`
- Create: `src/Ngraphiphy.Llm/OpenAiConfig.cs`
- Create: `src/Ngraphiphy.Llm/AnthropicConfig.cs`
- Create: `src/Ngraphiphy.Llm/OllamaConfig.cs`
- Create: `src/Ngraphiphy.Llm/CopilotConfig.cs`
- Create: `src/Ngraphiphy.Llm/A2AConfig.cs`
- Create: `tests/Ngraphiphy.Llm.Tests/AgentConfigTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Ngraphiphy.Llm.Tests/AgentConfigTests.cs
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
```

Run: `dotnet run --project tests/Ngraphiphy.Llm.Tests/`
Expected: Build error — types not found.

- [ ] **Step 2: Implement all config files**

```csharp
// src/Ngraphiphy.Llm/IAgentConfig.cs
namespace Ngraphiphy.Llm;

/// <summary>Marker interface for LLM provider configuration records.</summary>
public interface IAgentConfig { }
```

```csharp
// src/Ngraphiphy.Llm/OpenAiConfig.cs
namespace Ngraphiphy.Llm;

/// <param name="ApiKey">OpenAI API key (sk-...).</param>
/// <param name="Model">Model name, e.g. "gpt-4o".</param>
public sealed record OpenAiConfig(
    string ApiKey,
    string Model = "gpt-4o") : IAgentConfig;
```

```csharp
// src/Ngraphiphy.Llm/AnthropicConfig.cs
namespace Ngraphiphy.Llm;

/// <param name="ApiKey">Anthropic API key (sk-ant-...).</param>
/// <param name="Model">Model name, e.g. "claude-sonnet-4-6".</param>
/// <param name="MaxTokens">Maximum tokens in the response.</param>
public sealed record AnthropicConfig(
    string ApiKey,
    string Model = "claude-sonnet-4-6",
    int MaxTokens = 4096) : IAgentConfig;
```

```csharp
// src/Ngraphiphy.Llm/OllamaConfig.cs
namespace Ngraphiphy.Llm;

/// <param name="Model">Model name, e.g. "llama3.2".</param>
/// <param name="Endpoint">Ollama base URI (without /v1).</param>
public sealed record OllamaConfig(
    string Model,
    string Endpoint = "http://localhost:11434") : IAgentConfig;
```

```csharp
// src/Ngraphiphy.Llm/CopilotConfig.cs
namespace Ngraphiphy.Llm;

/// <summary>
/// GitHub Copilot provider. Requires GitHub CLI auth (<c>gh auth login</c>)
/// or GITHUB_TOKEN env var to be set before use.
/// </summary>
public sealed record CopilotConfig() : IAgentConfig;
```

```csharp
// src/Ngraphiphy.Llm/A2AConfig.cs
namespace Ngraphiphy.Llm;

/// <param name="AgentUrl">Base URL of the remote A2A agent (resolves its agent card).</param>
/// <param name="ApiKey">Optional bearer token for authentication.</param>
public sealed record A2AConfig(
    string AgentUrl,
    string? ApiKey = null) : IAgentConfig;
```

- [ ] **Step 3: Run tests**

```bash
dotnet run --project tests/Ngraphiphy.Llm.Tests/
```
Expected: 10 tests pass (4 GraphPlugin + 6 AgentConfig).

- [ ] **Step 4: Commit**

```bash
cd /home/timm/ngraphiphy
git add src/Ngraphiphy.Llm/IAgentConfig.cs src/Ngraphiphy.Llm/OpenAiConfig.cs \
    src/Ngraphiphy.Llm/AnthropicConfig.cs src/Ngraphiphy.Llm/OllamaConfig.cs \
    src/Ngraphiphy.Llm/CopilotConfig.cs src/Ngraphiphy.Llm/A2AConfig.cs \
    tests/Ngraphiphy.Llm.Tests/AgentConfigTests.cs
git commit -m "feat: add provider-specific config types for MAF agent factory"
```

---

## Task 3: MafGraphAgent and GraphAgentFactory

**Files:**
- Create: `src/Ngraphiphy.Llm/MafGraphAgent.cs`
- Create: `src/Ngraphiphy.Llm/GraphAgentFactory.cs`
- Create: `tests/Ngraphiphy.Llm.Tests/GraphAgentFactoryTests.cs`

**MAF API facts (from local clone at `.external/agent-framework`):**
- `AIAgent.CreateSessionAsync()` → `AgentSession`
- `AIAgent.RunAsync(string message, AgentSession? session, ...)` → `AgentResponse` with `.Text`
- `AIAgent.RunStreamingAsync(string message, AgentSession? session, ...)` → `IAsyncEnumerable<AgentResponseUpdate>`, each has `.Text`
- `ChatClient.AsAIAgent(instructions, name, tools)` — extension in `OpenAI.Chat` namespace (from `Microsoft.Agents.AI.OpenAI`)
- `IAnthropicClient.AsAIAgent(model, instructions, name, tools, defaultMaxTokens)` — extension in `Anthropic` namespace
- `OllamaApiClient.AsAIAgent(instructions, name)` — uses `IChatClient` extension from `Microsoft.Agents.AI`
- `CopilotClient.AsAIAgent(ownsClient, name, instructions, tools)` — extension in `GitHub.Copilot.SDK`; `StartAsync()` must be called first
- `A2ACardResolver(Uri).GetAIAgentAsync()` — resolves remote agent card; from `A2A` namespace
- `AIFunctionFactory.Create(methodRef)` — from `Microsoft.Extensions.AI`; wraps a method as `AITool`
- `AnthropicClient` constructor: `new AnthropicClient(new ClientOptions { ApiKey = ... })` — `ClientOptions` is in `Anthropic` namespace (verify: if it's in `Anthropic.Core`, add that using)

**Factory is async** because Copilot requires `StartAsync()` and A2A requires `GetAIAgentAsync()`. Tests for Copilot and A2A are omitted (require live services); OpenAI/Anthropic/Ollama create agents without network calls.

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Ngraphiphy.Llm.Tests/GraphAgentFactoryTests.cs
using Ngraphiphy.Build;
using Ngraphiphy.Llm;
using QuikGraph;

namespace Ngraphiphy.Llm.Tests;

public class GraphAgentFactoryTests
{
    private static BidirectionalGraph<Ngraphiphy.Models.Node, TaggedEdge<Ngraphiphy.Models.Node, Ngraphiphy.Models.Edge>>
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
        await Assert.That(async () => await GraphAgentFactory.CreateAsync(config, EmptyGraph()))
            .ThrowsException().OfType<NotSupportedException>();
    }

    private sealed record UnknownConfig : IAgentConfig;
}
```

Run: `dotnet run --project tests/Ngraphiphy.Llm.Tests/`
Expected: Build error — GraphAgentFactory, MafGraphAgent not found.

- [ ] **Step 2: Implement MafGraphAgent.cs**

```csharp
// src/Ngraphiphy.Llm/MafGraphAgent.cs
using Microsoft.Agents.AI;
using System.Runtime.CompilerServices;

namespace Ngraphiphy.Llm;

public sealed class MafGraphAgent : IGraphAgent
{
    private readonly AIAgent _agent;

    internal MafGraphAgent(AIAgent agent) => _agent = agent;

    public async Task<GraphSession> CreateSessionAsync(CancellationToken ct = default)
    {
        var agentSession = await _agent.CreateSessionAsync(ct);
        return new GraphSession(agentSession);
    }

    public async Task<string> AnswerAsync(string question, GraphSession session, CancellationToken ct = default)
    {
        var response = await _agent.RunAsync(question, session.AgentSession, cancellationToken: ct);
        return response.Text;
    }

    public async Task<string> SummarizeAsync(GraphSession session, CancellationToken ct = default)
        => await AnswerAsync(
            "Summarize the most interesting aspects of this codebase graph in 3-5 bullet points.",
            session, ct);

    public async IAsyncEnumerable<string> AnswerStreamingAsync(
        string question,
        GraphSession session,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var update in _agent.RunStreamingAsync(question, session.AgentSession, cancellationToken: ct))
        {
            if (!string.IsNullOrEmpty(update.Text))
                yield return update.Text;
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

- [ ] **Step 3: Implement GraphAgentFactory.cs**

```csharp
// src/Ngraphiphy.Llm/GraphAgentFactory.cs
using A2A;
using Anthropic;
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
        // ClientOptions may be in Anthropic or Anthropic.Core namespace — check compile error
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
        // OllamaApiClient implements IChatClient; AsAIAgent is the IChatClient extension from Microsoft.Agents.AI
        var client = new OllamaApiClient(new Uri(config.Endpoint), config.Model);
        return client.AsAIAgent(instructions: Instructions, name: "GraphAnalyst");
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
```

**If compilation fails:** The most likely issues are:
1. `ClientOptions` not found → try `using Anthropic.Core;` or check the Anthropic package for the correct options class name
2. `OllamaApiClient.AsAIAgent` not found → the extension might require `using Microsoft.Agents.AI;` or the Ollama client might need `.AsChatClient()` first: `client.AsChatClient().AsAIAgent(...)`
3. `A2ACardResolver` constructor signature → check `/home/timm/ngraphiphy/.external/agent-framework/dotnet/src/Microsoft.Agents.AI.A2A/Extensions/A2ACardResolverExtensions.cs`

- [ ] **Step 4: Run tests**

```bash
dotnet run --project tests/Ngraphiphy.Llm.Tests/
```
Expected: 14 tests pass (4 GraphPlugin + 6 AgentConfig + 4 GraphAgentFactory).

- [ ] **Step 5: Commit**

```bash
cd /home/timm/ngraphiphy
git add src/Ngraphiphy.Llm/MafGraphAgent.cs src/Ngraphiphy.Llm/GraphAgentFactory.cs \
    tests/Ngraphiphy.Llm.Tests/GraphAgentFactoryTests.cs
git commit -m "feat: add MafGraphAgent and GraphAgentFactory with 5-provider support"
```

---

## Task 4: RepositoryAnalysis pipeline and GraphTools

**Files:**
- Create: `src/Ngraphiphy.Pipeline/Ngraphiphy.Pipeline.csproj`
- Create: `src/Ngraphiphy.Pipeline/RepositoryAnalysis.cs`
- Create: `src/Ngraphiphy.Pipeline/GraphTools.cs`
- Modify: `src/Ngraphiphy.Cli/Ngraphiphy.Cli.csproj`
- Modify: `tests/Ngraphiphy.Cli.Tests/Ngraphiphy.Cli.Tests.csproj`
- Modify: `Ngraphiphy.sln`
- Create: `tests/Ngraphiphy.Cli.Tests/Pipeline/RepositoryAnalysisTests.cs`

**Key API facts (verified against actual source):**
- `FileDetector.Detect(string rootDir)` → `List<DetectedFile>` (property: `AbsolutePath`)
- `ExtractionCache(string cacheDir)` + static `ExtractionCache.FileHash(filePath, rootDir)` + `Load(hash)` + `Save(hash, extraction)`
- `LanguageRegistry.CreateDefault()` + `registry.GetExtractor(filePath)` + `extractor.Extract(filePath, source)`
- `GraphBuilder.Build(IEnumerable<Extraction>)` + `GraphBuilder.FromGraphData(GraphData)`
- `EntityDeduplicator.Deduplicate(List<Node>, List<Edge>)` → `(List<Node> Nodes, List<Edge> Edges)`
- `GraphData { Nodes = List<Node>, Edges = List<Edge> }`
- `LeidenClustering.FindCommunities(nNodes, IEnumerable<(int,int)> edges, PartitionType, seed)` → `CommunityResult` with `Membership: int[]`; `Node.Community: int?`
- `ReportGenerator.Generate(graph)` → `string`
- **Namespace alias required:** `using ExtractionModel = Ngraphiphy.Models.Extraction;` because `Ngraphiphy.Extraction` is a namespace AND `Ngraphiphy.Models.Extraction` is a class

**How to run tests:** `dotnet run --project tests/Ngraphiphy.Cli.Tests/`

- [ ] **Step 1: Create Ngraphiphy.Pipeline.csproj and add to solution**

```xml
<!-- src/Ngraphiphy.Pipeline/Ngraphiphy.Pipeline.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>Ngraphiphy.Pipeline</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\Ngraphiphy\Ngraphiphy.csproj" />
  </ItemGroup>
</Project>
```

```bash
cd /home/timm/ngraphiphy
dotnet sln Ngraphiphy.sln add src/Ngraphiphy.Pipeline/Ngraphiphy.Pipeline.csproj
```

- [ ] **Step 2: Add Pipeline reference to CLI and test csprojs**

Read `src/Ngraphiphy.Cli/Ngraphiphy.Cli.csproj`. Add inside the ProjectReferences ItemGroup:
```xml
<ProjectReference Include="..\Ngraphiphy.Pipeline\Ngraphiphy.Pipeline.csproj" />
```

Read `tests/Ngraphiphy.Cli.Tests/Ngraphiphy.Cli.Tests.csproj`. Add:
```xml
<ProjectReference Include="..\..\src\Ngraphiphy.Pipeline\Ngraphiphy.Pipeline.csproj" />
```

- [ ] **Step 3: Write failing tests**

```csharp
// tests/Ngraphiphy.Cli.Tests/Pipeline/RepositoryAnalysisTests.cs
using Ngraphiphy.Pipeline;

namespace Ngraphiphy.Cli.Tests.Pipeline;

public class RepositoryAnalysisTests
{
    [Test]
    public async Task RunAsync_OnEmptyDir_ReturnsEmptyGraph()
    {
        var dir = CreateTempDir();
        var result = await RepositoryAnalysis.RunAsync(dir);
        await Assert.That(result.Graph.VertexCount).IsEqualTo(0);
        await Assert.That(result.Report).Contains("# Graph Report");
    }

    [Test]
    public async Task RunAsync_OnPythonFile_FindsNodes()
    {
        var dir = CreateTempDir();
        File.WriteAllText(Path.Combine(dir, "app.py"), """
            class Foo:
                def bar(self): pass

            def main():
                Foo().bar()
            """);
        var result = await RepositoryAnalysis.RunAsync(dir);
        var labels = result.Graph.Vertices.Select(n => n.Label).ToList();
        await Assert.That(labels).Contains("Foo");
        await Assert.That(labels).Contains("main");
    }

    [Test]
    public async Task RunAsync_PopulatesReport()
    {
        var dir = CreateTempDir();
        File.WriteAllText(Path.Combine(dir, "a.py"), "class X: pass");
        var result = await RepositoryAnalysis.RunAsync(dir);
        await Assert.That(result.Report).IsNotNullOrEmpty();
    }

    [Test]
    public async Task RunAsync_UsesCache_SecondCallProducesSameCount()
    {
        var dir = CreateTempDir();
        var cacheDir = Path.Combine(dir, ".ngraphiphy-cache");
        File.WriteAllText(Path.Combine(dir, "b.py"), "class B: pass");
        var r1 = await RepositoryAnalysis.RunAsync(dir, cacheDir: cacheDir);
        var r2 = await RepositoryAnalysis.RunAsync(dir, cacheDir: cacheDir);
        await Assert.That(r2.Graph.VertexCount).IsEqualTo(r1.Graph.VertexCount);
    }

    private static string CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "ngraphiphy_test_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(path);
        return path;
    }
}
```

Run: `dotnet run --project tests/Ngraphiphy.Cli.Tests/`
Expected: Build error — RepositoryAnalysis not found.

- [ ] **Step 4: Implement RepositoryAnalysis.cs**

```csharp
// src/Ngraphiphy.Pipeline/RepositoryAnalysis.cs
using Ngraphiphy.Build;
using Ngraphiphy.Cache;
using Ngraphiphy.Cluster;
using Ngraphiphy.Dedup;
using Ngraphiphy.Detection;
using Ngraphiphy.Extraction;
using Ngraphiphy.Models;
using Ngraphiphy.Report;
using QuikGraph;
using ExtractionModel = Ngraphiphy.Models.Extraction;

namespace Ngraphiphy.Pipeline;

public sealed class RepositoryAnalysis
{
    public string RootPath { get; }
    public List<DetectedFile> Files { get; }
    public BidirectionalGraph<Node, TaggedEdge<Node, Edge>> Graph { get; }
    public string Report { get; }

    private RepositoryAnalysis(string rootPath, List<DetectedFile> files,
        BidirectionalGraph<Node, TaggedEdge<Node, Edge>> graph, string report)
    {
        RootPath = rootPath; Files = files; Graph = graph; Report = report;
    }

    public static async Task<RepositoryAnalysis> RunAsync(
        string rootPath,
        string? cacheDir = null,
        Action<string>? onProgress = null,
        CancellationToken ct = default)
    {
        cacheDir ??= Path.Combine(rootPath, ".ngraphiphy-cache");
        var cache = new ExtractionCache(cacheDir);
        var registry = LanguageRegistry.CreateDefault();

        onProgress?.Invoke("Detecting files...");
        var files = FileDetector.Detect(rootPath);

        onProgress?.Invoke($"Extracting {files.Count} files...");
        var extractions = new List<ExtractionModel>();
        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            var extractor = registry.GetExtractor(file.AbsolutePath);
            if (extractor is null) continue;

            var hash = ExtractionCache.FileHash(file.AbsolutePath, rootPath);
            var cached = cache.Load(hash);
            if (cached is not null) { extractions.Add(cached); continue; }

            var source = await File.ReadAllTextAsync(file.AbsolutePath, ct);
            var extraction = extractor.Extract(file.AbsolutePath, source);
            cache.Save(hash, extraction);
            extractions.Add(extraction);
        }

        onProgress?.Invoke("Building graph...");
        var rawGraph = GraphBuilder.Build(extractions);

        onProgress?.Invoke("Deduplicating entities...");
        var (dedupNodes, dedupEdges) = EntityDeduplicator.Deduplicate(
            rawGraph.Vertices.ToList(),
            rawGraph.Edges.Select(e => e.Tag).ToList());
        var graph = GraphBuilder.FromGraphData(new GraphData { Nodes = dedupNodes, Edges = dedupEdges });

        try
        {
            onProgress?.Invoke("Clustering communities...");
            var nodeList = graph.Vertices.ToList();
            if (nodeList.Count > 0)
            {
                var nodeIndex = nodeList.Select((n, i) => (n, i)).ToDictionary(x => x.n, x => x.i);
                var edgeTuples = graph.Edges.Select(e => (nodeIndex[e.Source], nodeIndex[e.Target]));
                var communities = LeidenClustering.FindCommunities(
                    nodeList.Count, edgeTuples, PartitionType.Modularity, seed: 42);
                for (int i = 0; i < nodeList.Count; i++)
                    nodeList[i].Community = communities.Membership[i];
            }
        }
        catch (DllNotFoundException)
        {
            onProgress?.Invoke("Warning: native Leiden library not found — skipping clustering.");
        }

        onProgress?.Invoke("Generating report...");
        var report = ReportGenerator.Generate(graph);
        return new RepositoryAnalysis(rootPath, files, graph, report);
    }
}
```

- [ ] **Step 5: Implement GraphTools.cs**

```csharp
// src/Ngraphiphy.Pipeline/GraphTools.cs
using System.Text.Json;
using Ngraphiphy.Analysis;
using Ngraphiphy.Models;
using QuikGraph;

namespace Ngraphiphy.Pipeline;

/// <summary>
/// Pure graph query logic with no MCP/HTTP dependency.
/// Used by MCP wrapper in Ngraphiphy.Cli and by tests in Ngraphiphy.Cli.Tests.
/// </summary>
public sealed class GraphTools
{
    private readonly RepositoryAnalysis _analysis;

    public GraphTools(RepositoryAnalysis analysis) => _analysis = analysis;

    public string GetGodNodes(int topN = 5)
    {
        var graph = _analysis.Graph;
        if (graph.VertexCount == 0) return "[]";
        return JsonSerializer.Serialize(
            GraphAnalyzer.GodNodes(graph, topN).Select(n => new
            {
                n.Id, n.Label, n.SourceFile,
                Connections = graph.InDegree(n) + graph.OutDegree(n),
            }));
    }

    public string GetSurprisingConnections(int topN = 10)
        => JsonSerializer.Serialize(
            GraphAnalyzer.SurprisingConnections(_analysis.Graph, topN).Select(c => new
            {
                Source = c.Source.Label,
                Target = c.Target.Label,
                c.Edge.Relation,
                c.Edge.ConfidenceString,
                c.Score,
            }));

    public string GetSummaryStats()
    {
        var graph = _analysis.Graph;
        var byFile = graph.Vertices
            .GroupBy(n => n.SourceFile)
            .Select(g => new { File = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count).Take(10);
        var communities = graph.Vertices
            .Where(n => n.Community.HasValue)
            .Select(n => n.Community!.Value).Distinct().Count();
        return JsonSerializer.Serialize(new
        {
            NodeCount = graph.VertexCount,
            EdgeCount = graph.EdgeCount,
            Communities = communities,
            TopFiles = byFile,
        });
    }

    public string SearchNodes(string query, int limit = 20)
        => JsonSerializer.Serialize(
            _analysis.Graph.Vertices
                .Where(n => n.Label.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Take(limit)
                .Select(n => new { n.Id, n.Label, n.SourceFile, n.FileTypeString }));

    public string GetReport() => _analysis.Report;
}
```

- [ ] **Step 6: Run tests**

```bash
dotnet run --project tests/Ngraphiphy.Cli.Tests/
dotnet run --project tests/Ngraphiphy.Tests/
```
Expected: 4 pipeline tests pass; 116 core tests still pass.

- [ ] **Step 7: Commit**

```bash
cd /home/timm/ngraphiphy
git add src/Ngraphiphy.Pipeline/ tests/Ngraphiphy.Cli.Tests/Pipeline/ \
    src/Ngraphiphy.Cli/Ngraphiphy.Cli.csproj \
    tests/Ngraphiphy.Cli.Tests/Ngraphiphy.Cli.Tests.csproj Ngraphiphy.sln
git commit -m "feat: add RepositoryAnalysis pipeline and GraphTools in Ngraphiphy.Pipeline"
```

---

## Task 5: AnalyzeCommand

**Files:**
- Modify: `src/Ngraphiphy.Cli/Commands/AnalyzeCommand.cs`
- Create: `tests/Ngraphiphy.Cli.Tests/Commands/AnalyzeCommandTests.cs`

**Spectre 0.55.0 API:** `AsyncCommand<T>.ExecuteAsync` signature is `protected override Task<int> ExecuteAsync(CommandContext context, TSettings settings, CancellationToken cancellationToken)`.

- [ ] **Step 1: Write tests (they test RepositoryAnalysis, not Spectre plumbing)**

```csharp
// tests/Ngraphiphy.Cli.Tests/Commands/AnalyzeCommandTests.cs
using Ngraphiphy.Pipeline;

namespace Ngraphiphy.Cli.Tests.Commands;

public class AnalyzeCommandTests
{
    [Test]
    public async Task AnalyzePythonRepo_FindsNodes()
    {
        var dir = CreateTempDir();
        File.WriteAllText(Path.Combine(dir, "main.py"), "class App:\n    def run(self): pass\n");
        var result = await RepositoryAnalysis.RunAsync(dir);
        await Assert.That(result.Graph.VertexCount).IsGreaterThan(0);
    }

    [Test]
    public async Task AnalyzeEmptyDir_ReturnsZeroFiles()
    {
        var dir = CreateTempDir();
        var result = await RepositoryAnalysis.RunAsync(dir);
        await Assert.That(result.Files.Count).IsEqualTo(0);
    }

    [Test]
    public async Task AnalyzeWithCache_SecondRunSameCount()
    {
        var dir = CreateTempDir();
        var cacheDir = Path.Combine(dir, ".cache");
        File.WriteAllText(Path.Combine(dir, "app.py"), "class X:\n    def y(self): pass");
        var r1 = await RepositoryAnalysis.RunAsync(dir, cacheDir: cacheDir);
        var r2 = await RepositoryAnalysis.RunAsync(dir, cacheDir: cacheDir);
        await Assert.That(r2.Graph.VertexCount).IsEqualTo(r1.Graph.VertexCount);
    }

    private static string CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "ngraphiphy_analyze_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(path);
        return path;
    }
}
```

Run: `dotnet run --project tests/Ngraphiphy.Cli.Tests/`
Expected: All 3 tests pass (they use RepositoryAnalysis from Task 4).

- [ ] **Step 2: Implement AnalyzeCommand.cs**

```csharp
// src/Ngraphiphy.Cli/Commands/AnalyzeCommand.cs
using System.ComponentModel;
using Ngraphiphy.Analysis;
using Ngraphiphy.Pipeline;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Ngraphiphy.Cli.Commands;

public sealed class AnalyzeSettings : CommandSettings
{
    [CommandArgument(0, "<path>")]
    [Description("Path to the repository root directory.")]
    public required string Path { get; init; }

    [CommandOption("--out|-o <file>")]
    [Description("Write the Markdown report to this file.")]
    public string? OutputFile { get; init; }

    [CommandOption("--cache <dir>")]
    [Description("Cache directory. Default: <path>/.ngraphiphy-cache")]
    public string? CacheDir { get; init; }
}

public sealed class AnalyzeCommand : AsyncCommand<AnalyzeSettings>
{
    protected override async Task<int> ExecuteAsync(
        CommandContext context, AnalyzeSettings settings, CancellationToken cancellationToken)
    {
        RepositoryAnalysis? result = null;
        await AnsiConsole.Status().Spinner(Spinner.Known.Dots)
            .StartAsync("Analyzing repository...", async ctx =>
            {
                result = await RepositoryAnalysis.RunAsync(
                    settings.Path, cacheDir: settings.CacheDir,
                    onProgress: msg => ctx.Status(msg), ct: cancellationToken);
            });

        if (result is null) return 1;

        var table = new Table()
            .AddColumn("Metric")
            .AddColumn(new TableColumn("Value").RightAligned());
        table.AddRow("Files detected", result.Files.Count.ToString());
        table.AddRow("Nodes (entities)", result.Graph.VertexCount.ToString());
        table.AddRow("Edges (relations)", result.Graph.EdgeCount.ToString());

        var godNodes = GraphAnalyzer.GodNodes(result.Graph, topN: 3);
        if (godNodes.Count > 0)
            table.AddRow("Top entity", $"{godNodes[0].Label} ({godNodes[0].SourceFile})");

        AnsiConsole.Write(table);

        if (settings.OutputFile is not null)
        {
            await File.WriteAllTextAsync(settings.OutputFile, result.Report, cancellationToken);
            AnsiConsole.MarkupLine($"[green]Report written to {settings.OutputFile}[/]");
        }

        return 0;
    }
}
```

- [ ] **Step 3: Smoke-test analyze**

```bash
dotnet run --project src/Ngraphiphy.Cli/ -- analyze src/Ngraphiphy/
```
Expected: Table with file/node/edge counts for the Ngraphiphy source.

- [ ] **Step 4: Commit**

```bash
cd /home/timm/ngraphiphy
git add src/Ngraphiphy.Cli/Commands/AnalyzeCommand.cs \
    tests/Ngraphiphy.Cli.Tests/Commands/AnalyzeCommandTests.cs
git commit -m "feat: implement analyze command with progress spinner and summary table"
```

---

## Task 6: ReportCommand and QueryCommand

**Files:**
- Modify: `src/Ngraphiphy.Cli/Commands/ReportCommand.cs`
- Modify: `src/Ngraphiphy.Cli/Commands/QueryCommand.cs`
- Modify: `src/Ngraphiphy.Cli/Ngraphiphy.Cli.csproj` (add Ngraphiphy.Llm ref if missing)

- [ ] **Step 1: Ensure Ngraphiphy.Cli.csproj references Ngraphiphy.Llm**

Read `src/Ngraphiphy.Cli/Ngraphiphy.Cli.csproj`. If `Ngraphiphy.Llm.csproj` is not in the ProjectReferences, add:
```xml
<ProjectReference Include="..\Ngraphiphy.Llm\Ngraphiphy.Llm.csproj" />
```

- [ ] **Step 2: Implement ReportCommand.cs**

```csharp
// src/Ngraphiphy.Cli/Commands/ReportCommand.cs
using System.ComponentModel;
using Ngraphiphy.Pipeline;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Ngraphiphy.Cli.Commands;

public sealed class ReportSettings : CommandSettings
{
    [CommandArgument(0, "<path>")]
    [Description("Repository root directory.")]
    public required string Path { get; init; }

    [CommandOption("--out|-o <file>")]
    [Description("Write report to this file. Prints to stdout if omitted.")]
    public string? OutputFile { get; init; }

    [CommandOption("--cache <dir>")]
    public string? CacheDir { get; init; }
}

public sealed class ReportCommand : AsyncCommand<ReportSettings>
{
    protected override async Task<int> ExecuteAsync(
        CommandContext context, ReportSettings settings, CancellationToken cancellationToken)
    {
        RepositoryAnalysis? result = null;
        await AnsiConsole.Status().Spinner(Spinner.Known.Dots)
            .StartAsync("Analyzing...", async ctx =>
            {
                result = await RepositoryAnalysis.RunAsync(
                    settings.Path, cacheDir: settings.CacheDir,
                    onProgress: msg => ctx.Status(msg), ct: cancellationToken);
            });

        if (result is null) return 1;

        if (settings.OutputFile is not null)
        {
            await File.WriteAllTextAsync(settings.OutputFile, result.Report, cancellationToken);
            AnsiConsole.MarkupLine($"[green]Report written to {settings.OutputFile}[/]");
        }
        else
        {
            Console.Write(result.Report);
        }

        return 0;
    }
}
```

- [ ] **Step 3: Implement QueryCommand.cs**

```csharp
// src/Ngraphiphy.Cli/Commands/QueryCommand.cs
using System.ComponentModel;
using Ngraphiphy.Llm;
using Ngraphiphy.Pipeline;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Ngraphiphy.Cli.Commands;

public sealed class QuerySettings : CommandSettings
{
    [CommandArgument(0, "<path>")]
    [Description("Repository root directory.")]
    public required string Path { get; init; }

    [CommandArgument(1, "<question>")]
    [Description("Question to ask about the repository graph.")]
    public required string Question { get; init; }

    [CommandOption("--provider <name>")]
    [Description("LLM provider: anthropic (default), openai, ollama, copilot, a2a.")]
    public string Provider { get; init; } = "anthropic";

    [CommandOption("--key <apiKey>")]
    [Description("API key. Falls back to ANTHROPIC_API_KEY or OPENAI_API_KEY env vars.")]
    public string? ApiKey { get; init; }

    [CommandOption("--model <name>")]
    [Description("Model name. Defaults: claude-sonnet-4-6 / gpt-4o / llama3.2")]
    public string? Model { get; init; }

    [CommandOption("--agent-url <url>")]
    [Description("Remote agent URL (A2A provider only).")]
    public string? AgentUrl { get; init; }

    [CommandOption("--cache <dir>")]
    public string? CacheDir { get; init; }
}

public sealed class QueryCommand : AsyncCommand<QuerySettings>
{
    protected override async Task<int> ExecuteAsync(
        CommandContext context, QuerySettings settings, CancellationToken cancellationToken)
    {
        IAgentConfig config = settings.Provider.ToLowerInvariant() switch
        {
            "openai"  => new OpenAiConfig(
                ApiKey: settings.ApiKey ?? Env("OPENAI_API_KEY") ?? Error("--key or OPENAI_API_KEY required"),
                Model: settings.Model ?? "gpt-4o"),
            "ollama"  => new OllamaConfig(Model: settings.Model ?? "llama3.2"),
            "copilot" => new CopilotConfig(),
            "a2a"     => new A2AConfig(
                AgentUrl: settings.AgentUrl ?? Error("--agent-url required for a2a"),
                ApiKey: settings.ApiKey),
            _         => new AnthropicConfig(
                ApiKey: settings.ApiKey ?? Env("ANTHROPIC_API_KEY") ?? Error("--key or ANTHROPIC_API_KEY required"),
                Model: settings.Model ?? "claude-sonnet-4-6"),
        };

        RepositoryAnalysis? repo = null;
        await AnsiConsole.Status().Spinner(Spinner.Known.Dots)
            .StartAsync("Analyzing repository...", async ctx =>
            {
                repo = await RepositoryAnalysis.RunAsync(
                    settings.Path, cacheDir: settings.CacheDir,
                    onProgress: msg => ctx.Status(msg), ct: cancellationToken);
            });

        if (repo is null) return 1;

        await using var agent = await GraphAgentFactory.CreateAsync(config, repo.Graph, cancellationToken);
        await using var session = await agent.CreateSessionAsync(cancellationToken);

        AnsiConsole.MarkupLine("[bold]Querying LLM...[/]");
        var answer = await agent.AnswerAsync(settings.Question, session, cancellationToken);

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Panel(answer).Header("Answer").Expand());
        return 0;
    }

    private static string? Env(string name) => Environment.GetEnvironmentVariable(name);

    private static string Error(string message)
    {
        AnsiConsole.MarkupLine($"[red]{message}[/]");
        throw new InvalidOperationException(message);
    }
}
```

- [ ] **Step 4: Verify help and smoke-test report**

```bash
dotnet run --project src/Ngraphiphy.Cli/ -- report --help
dotnet run --project src/Ngraphiphy.Cli/ -- query --help
dotnet run --project src/Ngraphiphy.Cli/ -- report src/Ngraphiphy/
```
Expected: Help shown for both; report prints with `# Graph Report` header.

- [ ] **Step 5: Commit**

```bash
cd /home/timm/ngraphiphy
git add src/Ngraphiphy.Cli/Commands/ReportCommand.cs \
    src/Ngraphiphy.Cli/Commands/QueryCommand.cs \
    src/Ngraphiphy.Cli/Ngraphiphy.Cli.csproj
git commit -m "feat: implement report and query commands"
```

---

## Task 7: GraphTools tests, MCP server, and ServeCommand

**Files:**
- Create: `tests/Ngraphiphy.Cli.Tests/Mcp/GraphToolsTests.cs`
- Create: `src/Ngraphiphy.Cli/Mcp/McpGraphToolsWrapper.cs`
- Create: `src/Ngraphiphy.Cli/Mcp/GraphMcpServer.cs`
- Modify: `src/Ngraphiphy.Cli/Commands/ServeCommand.cs`

`ModelContextProtocol` 1.3.0 is already in `Ngraphiphy.Cli.csproj`. `[McpServerToolType]` and `[McpServerTool]` are in `ModelContextProtocol.Server`. `AddMcpServer().WithStdioServerTransport().WithTools<T>()` sets up the host.

- [ ] **Step 1: Write GraphTools tests**

```csharp
// tests/Ngraphiphy.Cli.Tests/Mcp/GraphToolsTests.cs
using Ngraphiphy.Pipeline;

namespace Ngraphiphy.Cli.Tests.Mcp;

public class GraphToolsTests
{
    private static async Task<GraphTools> MakeToolsAsync()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ngraphiphy_mcp_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "app.py"), """
            class Server:
                def start(self): pass
                def stop(self): pass

            class Client:
                def connect(self): pass

            def main():
                s = Server()
                s.start()
            """);
        return new GraphTools(await RepositoryAnalysis.RunAsync(dir));
    }

    [Test]
    public async Task GetGodNodes_ContainsServer()
    {
        var result = (await MakeToolsAsync()).GetGodNodes(topN: 2);
        await Assert.That(result).Contains("Server");
    }

    [Test]
    public async Task GetReport_ContainsHeader()
    {
        var result = (await MakeToolsAsync()).GetReport();
        await Assert.That(result).Contains("# Graph Report");
    }

    [Test]
    public async Task GetSummaryStats_ContainsNodeCount()
    {
        var result = (await MakeToolsAsync()).GetSummaryStats();
        await Assert.That(result).Contains("NodeCount");
    }

    [Test]
    public async Task SearchNodes_FindsServer_NotClient()
    {
        var tools = await MakeToolsAsync();
        var result = tools.SearchNodes("Server");
        await Assert.That(result).Contains("Server");
        await Assert.That(result).DoesNotContain("Client");
    }

    [Test]
    public async Task GetGodNodes_EmptyGraph_ReturnsEmptyArray()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ngraphiphy_mcp_empty_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        var tools = new GraphTools(await RepositoryAnalysis.RunAsync(dir));
        await Assert.That(tools.GetGodNodes()).IsEqualTo("[]");
    }
}
```

Run: `dotnet run --project tests/Ngraphiphy.Cli.Tests/`
Expected: All 5 tests pass (GraphTools implemented in Task 4).

- [ ] **Step 2: Create McpGraphToolsWrapper.cs**

```csharp
// src/Ngraphiphy.Cli/Mcp/McpGraphToolsWrapper.cs
using System.ComponentModel;
using ModelContextProtocol.Server;
using Ngraphiphy.Pipeline;

namespace Ngraphiphy.Cli.Mcp;

[McpServerToolType]
internal sealed class McpGraphToolsWrapper(GraphTools tools)
{
    [McpServerTool(Name = "get_god_nodes")]
    [Description("Return the most connected nodes (god nodes) in the repository graph as JSON.")]
    public string GetGodNodes([Description("Maximum nodes to return (default 5)")] int topN = 5)
        => tools.GetGodNodes(topN);

    [McpServerTool(Name = "get_surprising_connections")]
    [Description("Return the most surprising cross-file or ambiguous edges as JSON.")]
    public string GetSurprisingConnections([Description("Maximum connections (default 10)")] int topN = 10)
        => tools.GetSurprisingConnections(topN);

    [McpServerTool(Name = "get_summary_stats")]
    [Description("Return summary statistics: node count, edge count, communities, top files.")]
    public string GetSummaryStats() => tools.GetSummaryStats();

    [McpServerTool(Name = "search_nodes")]
    [Description("Search for nodes whose label contains the query string. Returns JSON.")]
    public string SearchNodes(
        [Description("Search term")] string query,
        [Description("Maximum results (default 20)")] int limit = 20)
        => tools.SearchNodes(query, limit);

    [McpServerTool(Name = "get_report")]
    [Description("Return the full Markdown analysis report for the repository.")]
    public string GetReport() => tools.GetReport();
}
```

- [ ] **Step 3: Create GraphMcpServer.cs**

```csharp
// src/Ngraphiphy.Cli/Mcp/GraphMcpServer.cs
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;
using Ngraphiphy.Pipeline;

namespace Ngraphiphy.Cli.Mcp;

public static class GraphMcpServer
{
    /// <summary>
    /// Run an MCP server over stdio for the given repository analysis.
    /// Blocks until the MCP client disconnects (stdin closes).
    /// </summary>
    public static async Task RunAsync(RepositoryAnalysis analysis, CancellationToken ct = default)
    {
        var tools = new GraphTools(analysis);
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton(tools);
        builder.Services.AddMcpServer()
            .WithStdioServerTransport()
            .WithTools<McpGraphToolsWrapper>();
        await builder.Build().RunAsync(ct);
    }
}
```

- [ ] **Step 4: Implement ServeCommand.cs**

```csharp
// src/Ngraphiphy.Cli/Commands/ServeCommand.cs
using System.ComponentModel;
using Ngraphiphy.Cli.Mcp;
using Ngraphiphy.Pipeline;
using Spectre.Console.Cli;

namespace Ngraphiphy.Cli.Commands;

public sealed class ServeSettings : CommandSettings
{
    [CommandArgument(0, "[path]")]
    [Description("Repository root to analyze. Defaults to current directory.")]
    public string Path { get; init; } = Directory.GetCurrentDirectory();

    [CommandOption("--cache <dir>")]
    [Description("Cache directory. Default: <path>/.ngraphiphy-cache")]
    public string? CacheDir { get; init; }
}

public sealed class ServeCommand : AsyncCommand<ServeSettings>
{
    protected override async Task<int> ExecuteAsync(
        CommandContext context, ServeSettings settings, CancellationToken cancellationToken)
    {
        // stdout is the MCP channel — all diagnostics go to stderr
        Console.Error.WriteLine($"[ngraphiphy] Analyzing {settings.Path}...");
        RepositoryAnalysis analysis;
        try
        {
            analysis = await RepositoryAnalysis.RunAsync(
                settings.Path, cacheDir: settings.CacheDir,
                onProgress: msg => Console.Error.WriteLine($"[ngraphiphy] {msg}"),
                ct: cancellationToken);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ngraphiphy] Analysis failed: {ex.Message}");
            return 1;
        }

        Console.Error.WriteLine(
            $"[ngraphiphy] Ready — {analysis.Graph.VertexCount} nodes, {analysis.Graph.EdgeCount} edges.");
        Console.Error.WriteLine("[ngraphiphy] Starting MCP server on stdio...");
        await GraphMcpServer.RunAsync(analysis, cancellationToken);
        return 0;
    }
}
```

- [ ] **Step 5: Verify build and help**

```bash
dotnet build src/Ngraphiphy.Cli/
dotnet run --project src/Ngraphiphy.Cli/ -- serve --help
```
Expected: Build succeeded; help shows `[path]` and `--cache`.

- [ ] **Step 6: Run all test suites**

```bash
dotnet run --project tests/Ngraphiphy.Tests/
dotnet run --project tests/Ngraphiphy.Llm.Tests/
dotnet run --project tests/Ngraphiphy.Cli.Tests/
```
Expected: All pass.

- [ ] **Step 7: Commit**

```bash
cd /home/timm/ngraphiphy
git add tests/Ngraphiphy.Cli.Tests/Mcp/GraphToolsTests.cs \
    src/Ngraphiphy.Cli/Mcp/ src/Ngraphiphy.Cli/Commands/ServeCommand.cs
git commit -m "feat: implement MCP server and serve command using ModelContextProtocol stdio transport"
```

---

## Task 8: MCP docs and final integration

**Files:**
- Create: `tests/Ngraphiphy.Cli.Tests/Mcp/McpToolDescriptionTests.cs`
- Create: `docs/mcp-config.md`

- [ ] **Step 1: Write MCP smoke test**

```csharp
// tests/Ngraphiphy.Cli.Tests/Mcp/McpToolDescriptionTests.cs
using Ngraphiphy.Pipeline;

namespace Ngraphiphy.Cli.Tests.Mcp;

public class McpToolDescriptionTests
{
    [Test]
    public async Task AllToolMethods_ReturnNonEmpty()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ngraphiphy_mcp2_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "x.py"), "class X:\n    def y(self): pass\n");
        var tools = new GraphTools(await RepositoryAnalysis.RunAsync(dir));

        foreach (var result in new[]
        {
            tools.GetGodNodes(),
            tools.GetSurprisingConnections(),
            tools.GetSummaryStats(),
            tools.SearchNodes("X"),
            tools.GetReport(),
        })
            await Assert.That(result).IsNotNullOrEmpty();
    }
}
```

Run: `dotnet run --project tests/Ngraphiphy.Cli.Tests/`
Expected: All tests pass.

- [ ] **Step 2: Create docs/mcp-config.md**

```markdown
# Using Ngraphiphy as an MCP Server with Claude Desktop

## Build

```bash
dotnet publish src/Ngraphiphy.Cli/ -c Release -o ./dist
```

## Claude Desktop Configuration

Add to `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "ngraphiphy": {
      "command": "/absolute/path/to/dist/ngraphiphy",
      "args": ["serve", "/absolute/path/to/your/repo"]
    }
  }
}
```

- Linux/macOS: `~/.config/claude/claude_desktop_config.json`
- Windows: `%APPDATA%\Claude\claude_desktop_config.json`

## Available Tools

| Tool | Description |
|------|-------------|
| `get_god_nodes` | Most connected entities (optional `topN`) |
| `get_surprising_connections` | Cross-file and ambiguous edges (optional `topN`) |
| `get_summary_stats` | Node count, edge count, community count, top files |
| `search_nodes` | Filter nodes by label substring |
| `get_report` | Full Markdown analysis report |

## Example Prompts

> "What are the most connected classes in this repository?"

> "Are there any surprising dependencies between modules?"

> "Show me everything that depends on the Router class."
```

- [ ] **Step 3: Final end-to-end smoke test**

```bash
dotnet run --project src/Ngraphiphy.Cli/ -- analyze src/Ngraphiphy/
dotnet run --project src/Ngraphiphy.Cli/ -- report src/Ngraphiphy/ --out /tmp/ngraphiphy-report.md
head -3 /tmp/ngraphiphy-report.md
```
Expected: Table with node/edge counts; `/tmp/ngraphiphy-report.md` starts with `# Graph Report`.

- [ ] **Step 4: Final commit**

```bash
cd /home/timm/ngraphiphy
git add tests/Ngraphiphy.Cli.Tests/Mcp/McpToolDescriptionTests.cs docs/mcp-config.md
git commit -m "docs: add MCP config docs and final smoke test"
```

---

## Summary

| Task | Delivers |
|------|----------|
| 1 | Replace SK with MAF in Ngraphiphy.Llm; session-aware IGraphAgent + GraphSession; plain-method GraphPlugin |
| 2 | Provider configs: OpenAiConfig, AnthropicConfig, OllamaConfig, CopilotConfig, A2AConfig |
| 3 | MafGraphAgent + async GraphAgentFactory (OpenAI, Anthropic, Ollama, Copilot, A2A) |
| 4 | Ngraphiphy.Pipeline library: RepositoryAnalysis + GraphTools |
| 5 | AnalyzeCommand (spinner + summary table) |
| 6 | ReportCommand + QueryCommand (IAgentConfig-based provider selection) |
| 7 | GraphTools tests + McpGraphToolsWrapper + GraphMcpServer + ServeCommand |
| 8 | MCP docs + final integration smoke tests |

**MAF packages:**
- `Microsoft.Agents.AI` / `Microsoft.Agents.AI.OpenAI` → v1.5.0 (stable)
- `Microsoft.Agents.AI.Anthropic` / `.A2A` / `.GitHub.Copilot` → add with `dotnet add package <name> --prerelease`
- `OllamaSharp` → v5.4.8
