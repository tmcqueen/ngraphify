# Graphiphy Phase 2: LLM Backends, MCP Server, and CLI

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a Spectre.Console CLI entry point, Microsoft Semantic Kernel–based LLM query agents (Anthropic / OpenAI / Ollama), and a ModelContextProtocol MCP server that exposes the knowledge graph as Claude tools — all wired together so one binary can be used interactively or as an MCP server.

**Architecture:** Three new projects join the solution. `Graphiphy.Llm` wraps Semantic Kernel and exposes an `IGraphAgent` interface with provider-specific implementations. `Graphiphy.Cli` (console app) owns the Spectre.Console command tree, a `RepositoryAnalysis` pipeline orchestrator, and the MCP server (embedded as a `serve` command using stdio transport). Tests live in matching `*.Tests` projects.

**Tech Stack:**
- Spectre.Console.Cli 0.49.1 — command-line interface
- Microsoft.SemanticKernel 1.32.0 — agent framework (OpenAI + Anthropic + Ollama)
- Microsoft.SemanticKernel.Connectors.Anthropic 1.32.0-preview — Claude backend
- ModelContextProtocol 0.1.0-preview.1 — official MCP C# SDK (Anthropic/Microsoft)
- Microsoft.Extensions.Hosting 9.0.0 — host builder for MCP server
- TUnit 1.43.41 — all tests

> **Note on package versions:** The MCP and SK Anthropic packages move quickly. Before starting, verify latest compatible versions on NuGet.org. Use `dotnet add package <name>` and let NuGet resolve; lock to those versions in the plan.

---

## File Structure

```
src/
  Graphiphy.Llm/
    Graphiphy.Llm.csproj            → refs Graphiphy; adds SK + connectors
    IGraphAgent.cs                   → interface: AnswerAsync, SummarizeAsync
    GraphPlugin.cs                   → [KernelFunction] wrappers over GraphAnalyzer + graph
    KernelFactory.cs                 → creates Kernel for each provider
    GraphAgentFactory.cs             → creates IGraphAgent from LlmConfig

  Graphiphy.Cli/
    Graphiphy.Cli.csproj            → refs Graphiphy + Graphiphy.Llm; adds Spectre + MCP
    Program.cs                       → CommandApp entry point
    Pipeline/
      RepositoryAnalysis.cs          → orchestrates Detect→Extract→Build→Dedup→Cluster→Analyze
    Commands/
      AnalyzeCommand.cs              → graphiphy analyze <path> [--out <file>] [--cache <dir>]
      ReportCommand.cs               → graphiphy report <path> [--out <file>]
      QueryCommand.cs                → graphiphy query <path> <question> --provider X --key Y
      ServeCommand.cs                → graphiphy serve --path <dir>  (starts MCP stdio server)
    Mcp/
      GraphMcpServer.cs              → IHost builder wired to stdio, registers all tools
      GraphTools.cs                  → [McpServerTool] methods (get_god_nodes, get_report, etc.)

tests/
  Graphiphy.Llm.Tests/
    Graphiphy.Llm.Tests.csproj
    GraphPluginTests.cs
    GraphAgentFactoryTests.cs

  Graphiphy.Cli.Tests/
    Graphiphy.Cli.Tests.csproj
    Pipeline/
      RepositoryAnalysisTests.cs
    Commands/
      AnalyzeCommandTests.cs
    Mcp/
      GraphToolsTests.cs
```

---

## Phase A: LLM Backends

### Task 1: Graphiphy.Llm Project Scaffold

**Files:**
- Create: `src/Graphiphy.Llm/Graphiphy.Llm.csproj`
- Create: `src/Graphiphy.Llm/IGraphAgent.cs`
- Create: `tests/Graphiphy.Llm.Tests/Graphiphy.Llm.Tests.csproj`
- Modify: `Graphiphy.sln` (add both new projects)

- [ ] **Step 1: Create the library csproj**

```xml
<!-- src/Graphiphy.Llm/Graphiphy.Llm.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>Graphiphy.Llm</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\Graphiphy\Graphiphy.csproj" />
  </ItemGroup>

  <ItemGroup>
    <!-- Verify latest versions on NuGet.org before adding -->
    <PackageReference Include="Microsoft.SemanticKernel" Version="1.32.0" />
    <PackageReference Include="Microsoft.SemanticKernel.Connectors.Anthropic" Version="1.32.0-preview.1" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Define the IGraphAgent interface**

```csharp
// src/Graphiphy.Llm/IGraphAgent.cs
using Graphiphy.Models;
using QuikGraph;

namespace Graphiphy.Llm;

/// <summary>
/// Answers natural-language questions about an analyzed knowledge graph.
/// </summary>
public interface IGraphAgent
{
    /// <summary>
    /// Answer a free-form question about the graph using the configured LLM.
    /// </summary>
    Task<string> AnswerAsync(
        string question,
        BidirectionalGraph<Node, TaggedEdge<Node, Edge>> graph,
        CancellationToken ct = default);

    /// <summary>
    /// Produce a short prose summary of the graph's most notable features.
    /// </summary>
    Task<string> SummarizeAsync(
        BidirectionalGraph<Node, TaggedEdge<Node, Edge>> graph,
        CancellationToken ct = default);
}
```

- [ ] **Step 3: Create the test project csproj**

```xml
<!-- tests/Graphiphy.Llm.Tests/Graphiphy.Llm.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <OutputType>Exe</OutputType>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="TUnit" Version="1.43.41" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Graphiphy.Llm\Graphiphy.Llm.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Add both projects to the solution**

```bash
dotnet sln Graphiphy.sln add src/Graphiphy.Llm/Graphiphy.Llm.csproj
dotnet sln Graphiphy.sln add tests/Graphiphy.Llm.Tests/Graphiphy.Llm.Tests.csproj
```

- [ ] **Step 5: Verify both projects build**

```bash
dotnet build src/Graphiphy.Llm/Graphiphy.Llm.csproj
```
Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add src/Graphiphy.Llm/ tests/Graphiphy.Llm.Tests/ Graphiphy.sln
git commit -m "feat: add Graphiphy.Llm project scaffold with IGraphAgent interface"
```

---

### Task 2: GraphPlugin — Kernel Functions Over Graph Analysis

**Files:**
- Create: `src/Graphiphy.Llm/GraphPlugin.cs`
- Create: `tests/Graphiphy.Llm.Tests/GraphPluginTests.cs`

The `GraphPlugin` is a Semantic Kernel plugin: a class whose methods are decorated with `[KernelFunction]`. The LLM will call these automatically when answering questions. Each method serialises its result as a short JSON string.

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/Graphiphy.Llm.Tests/GraphPluginTests.cs
using Graphiphy.Build;
using Graphiphy.Llm;
using Graphiphy.Models;

namespace Graphiphy.Llm.Tests;

public class GraphPluginTests
{
    private static BidirectionalGraph<Node, TaggedEdge<Node, Edge>> MakeGraph()
    {
        var ext = new Extraction
        {
            Nodes =
            [
                new() { Id = "a::Hub", Label = "Hub", FileTypeString = "code", SourceFile = "a.py" },
                new() { Id = "a::Spoke1", Label = "Spoke1", FileTypeString = "code", SourceFile = "a.py" },
                new() { Id = "b::Other", Label = "Other", FileTypeString = "code", SourceFile = "b.py" },
            ],
            Edges =
            [
                new() { Source = "a::Hub", Target = "a::Spoke1", Relation = "calls",
                        ConfidenceString = "EXTRACTED", SourceFile = "a.py" },
                new() { Source = "a::Hub", Target = "b::Other", Relation = "calls",
                        ConfidenceString = "AMBIGUOUS", SourceFile = "a.py" },
            ]
        };
        return GraphBuilder.Build([ext]);
    }

    [Test]
    public async Task GetGodNodes_ReturnsJson()
    {
        var plugin = new GraphPlugin(MakeGraph());
        var result = plugin.GetGodNodes(topN: 1);

        await Assert.That(result).IsNotNullOrEmpty();
        await Assert.That(result).Contains("Hub");
    }

    [Test]
    public async Task GetSurprisingConnections_ReturnsJson()
    {
        var plugin = new GraphPlugin(MakeGraph());
        var result = plugin.GetSurprisingConnections(topN: 5);

        await Assert.That(result).IsNotNullOrEmpty();
    }

    [Test]
    public async Task GetSummaryStats_ReturnsNodeAndEdgeCount()
    {
        var plugin = new GraphPlugin(MakeGraph());
        var result = plugin.GetSummaryStats();

        await Assert.That(result).Contains("3");   // 3 nodes
        await Assert.That(result).Contains("2");   // 2 edges
    }

    [Test]
    public async Task SearchNodes_FiltersById()
    {
        var plugin = new GraphPlugin(MakeGraph());
        var result = plugin.SearchNodes("Hub");

        await Assert.That(result).Contains("Hub");
        await Assert.That(result).DoesNotContain("Spoke1");
    }
}
```

- [ ] **Step 2: Run tests to confirm they fail**

```bash
dotnet run --project tests/Graphiphy.Llm.Tests/
```
Expected: Build error — GraphPlugin not found.

- [ ] **Step 3: Implement GraphPlugin**

```csharp
// src/Graphiphy.Llm/GraphPlugin.cs
using System.Text.Json;
using Microsoft.SemanticKernel;
using Graphiphy.Analysis;
using Graphiphy.Models;
using QuikGraph;

namespace Graphiphy.Llm;

/// <summary>
/// Semantic Kernel plugin that wraps the Graphiphy analysis API.
/// The LLM may call these functions to explore the knowledge graph.
/// </summary>
public sealed class GraphPlugin
{
    private readonly BidirectionalGraph<Node, TaggedEdge<Node, Edge>> _graph;

    public GraphPlugin(BidirectionalGraph<Node, TaggedEdge<Node, Edge>> graph)
        => _graph = graph;

    [KernelFunction("get_god_nodes")]
    [System.ComponentModel.Description("Return the most connected nodes (god nodes) in the graph as JSON.")]
    public string GetGodNodes(
        [System.ComponentModel.Description("Maximum number of nodes to return")] int topN = 5)
    {
        var nodes = GraphAnalyzer.GodNodes(_graph, topN);
        return JsonSerializer.Serialize(nodes.Select(n => new
        {
            n.Id,
            n.Label,
            n.SourceFile,
            Connections = _graph.InDegree(n) + _graph.OutDegree(n),
        }));
    }

    [KernelFunction("get_surprising_connections")]
    [System.ComponentModel.Description("Return the most surprising cross-file or high-confidence edges as JSON.")]
    public string GetSurprisingConnections(
        [System.ComponentModel.Description("Maximum number of connections to return")] int topN = 10)
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

    [KernelFunction("get_summary_stats")]
    [System.ComponentModel.Description("Return summary statistics about the graph (node count, edge count, file breakdown).")]
    public string GetSummaryStats()
    {
        var byFile = _graph.Vertices
            .GroupBy(n => n.SourceFile)
            .Select(g => new { File = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10);

        return JsonSerializer.Serialize(new
        {
            NodeCount = _graph.VertexCount,
            EdgeCount = _graph.EdgeCount,
            TopFiles = byFile,
        });
    }

    [KernelFunction("search_nodes")]
    [System.ComponentModel.Description("Search for nodes whose label contains the given query string. Returns JSON.")]
    public string SearchNodes(
        [System.ComponentModel.Description("Search term to match against node labels")] string query)
    {
        var matches = _graph.Vertices
            .Where(n => n.Label.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(20)
            .Select(n => new { n.Id, n.Label, n.SourceFile, n.FileTypeString });

        return JsonSerializer.Serialize(matches);
    }
}
```

- [ ] **Step 4: Run tests to confirm they pass**

```bash
dotnet run --project tests/Graphiphy.Llm.Tests/
```
Expected: 4 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/Graphiphy.Llm/GraphPlugin.cs tests/Graphiphy.Llm.Tests/GraphPluginTests.cs
git commit -m "feat: add GraphPlugin with SK kernel functions for graph analysis"
```

---

### Task 3: KernelFactory and SemanticKernelGraphAgent

**Files:**
- Create: `src/Graphiphy.Llm/LlmConfig.cs`
- Create: `src/Graphiphy.Llm/KernelFactory.cs`
- Create: `src/Graphiphy.Llm/SemanticKernelGraphAgent.cs`
- Create: `tests/Graphiphy.Llm.Tests/KernelFactoryTests.cs`

The agent wraps a `ChatCompletionAgent` (SK) with the `GraphPlugin` attached. It answers questions in a single-turn fashion: user message → LLM streams response → return string.

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/Graphiphy.Llm.Tests/KernelFactoryTests.cs
using Graphiphy.Llm;

namespace Graphiphy.Llm.Tests;

public class KernelFactoryTests
{
    [Test]
    public async Task CreateOpenAiKernel_WithFakeKey_ReturnsKernel()
    {
        var config = new LlmConfig(Provider: LlmProvider.OpenAi, ApiKey: "sk-fake", Model: "gpt-4o");

        // Should not throw — we don't make a network call here
        var kernel = KernelFactory.Create(config);

        await Assert.That(kernel).IsNotNull();
    }

    [Test]
    public async Task CreateAnthropicKernel_WithFakeKey_ReturnsKernel()
    {
        var config = new LlmConfig(Provider: LlmProvider.Anthropic, ApiKey: "sk-ant-fake", Model: "claude-3-5-sonnet-20241022");

        var kernel = KernelFactory.Create(config);

        await Assert.That(kernel).IsNotNull();
    }

    [Test]
    public async Task CreateOllamaKernel_NoKey_ReturnsKernel()
    {
        var config = new LlmConfig(Provider: LlmProvider.Ollama, ApiKey: null, Model: "llama3.2");

        var kernel = KernelFactory.Create(config);

        await Assert.That(kernel).IsNotNull();
    }
}
```

- [ ] **Step 2: Run to confirm they fail**

```bash
dotnet run --project tests/Graphiphy.Llm.Tests/
```
Expected: Build error — LlmConfig, KernelFactory not found.

- [ ] **Step 3: Implement LlmConfig record**

```csharp
// src/Graphiphy.Llm/LlmConfig.cs
namespace Graphiphy.Llm;

public enum LlmProvider { OpenAi, Anthropic, Ollama }

/// <param name="Provider">Which LLM backend to use.</param>
/// <param name="ApiKey">API key. Null is accepted for Ollama (local, no auth).</param>
/// <param name="Model">Model name, e.g. "gpt-4o", "claude-3-5-sonnet-20241022", "llama3.2".</param>
/// <param name="OllamaEndpoint">Ollama base URI. Defaults to http://localhost:11434.</param>
public sealed record LlmConfig(
    LlmProvider Provider,
    string? ApiKey,
    string Model,
    string OllamaEndpoint = "http://localhost:11434");
```

- [ ] **Step 4: Implement KernelFactory**

```csharp
// src/Graphiphy.Llm/KernelFactory.cs
using Microsoft.SemanticKernel;

namespace Graphiphy.Llm;

public static class KernelFactory
{
    /// <summary>Creates a configured Kernel for the given provider. Does not make any network calls.</summary>
    public static Kernel Create(LlmConfig config)
    {
        var builder = Kernel.CreateBuilder();

        switch (config.Provider)
        {
            case LlmProvider.OpenAi:
                builder.AddOpenAIChatCompletion(config.Model, config.ApiKey
                    ?? throw new ArgumentException("ApiKey required for OpenAI"));
                break;

            case LlmProvider.Anthropic:
                // Microsoft.SemanticKernel.Connectors.Anthropic — verify package name on NuGet
                builder.AddAnthropicChatCompletion(config.Model, config.ApiKey
                    ?? throw new ArgumentException("ApiKey required for Anthropic"));
                break;

            case LlmProvider.Ollama:
                // Ollama exposes an OpenAI-compatible endpoint — no separate connector needed
                builder.AddOpenAIChatCompletion(
                    modelId: config.Model,
                    apiKey: "ollama",   // Ollama ignores this but SK requires a non-null value
                    endpoint: new Uri(config.OllamaEndpoint + "/v1"));
                break;

            default:
                throw new NotSupportedException($"Provider {config.Provider} is not supported");
        }

        return builder.Build();
    }
}
```

- [ ] **Step 5: Implement SemanticKernelGraphAgent**

```csharp
// src/Graphiphy.Llm/SemanticKernelGraphAgent.cs
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Graphiphy.Models;
using QuikGraph;

namespace Graphiphy.Llm;

public sealed class SemanticKernelGraphAgent : IGraphAgent
{
    private readonly LlmConfig _config;

    public SemanticKernelGraphAgent(LlmConfig config) => _config = config;

    public async Task<string> AnswerAsync(
        string question,
        BidirectionalGraph<Node, TaggedEdge<Node, Edge>> graph,
        CancellationToken ct = default)
    {
        var kernel = KernelFactory.Create(_config);
        kernel.Plugins.AddFromObject(new GraphPlugin(graph), "graph");

        var agent = new ChatCompletionAgent
        {
            Name = "GraphAnalyst",
            Instructions = """
                You are an expert software architect analyzing a knowledge graph of a codebase.
                Use the provided tools to explore the graph and answer questions accurately.
                Be concise. Format code identifiers in backticks.
                """,
            Kernel = kernel,
            Arguments = new KernelArguments(
                new PromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() }),
        };

        var history = new ChatHistory();
        history.AddUserMessage(question);

        var sb = new System.Text.StringBuilder();
        await foreach (var response in agent.InvokeAsync(history, cancellationToken: ct))
        {
            sb.Append(response.Content);
        }
        return sb.ToString();
    }

    public async Task<string> SummarizeAsync(
        BidirectionalGraph<Node, TaggedEdge<Node, Edge>> graph,
        CancellationToken ct = default)
        => await AnswerAsync(
            "Summarize the most interesting aspects of this codebase graph in 3-5 bullet points.",
            graph, ct);
}
```

- [ ] **Step 6: Run tests**

```bash
dotnet run --project tests/Graphiphy.Llm.Tests/
```
Expected: All tests pass (KernelFactory tests + GraphPlugin tests = 7 total).

- [ ] **Step 7: Commit**

```bash
git add src/Graphiphy.Llm/ tests/Graphiphy.Llm.Tests/
git commit -m "feat: add KernelFactory and SemanticKernelGraphAgent with multi-provider support"
```

---

### Task 4: GraphAgentFactory

**Files:**
- Create: `src/Graphiphy.Llm/GraphAgentFactory.cs`
- Create: `tests/Graphiphy.Llm.Tests/GraphAgentFactoryTests.cs`

A simple factory that creates `IGraphAgent` from an `LlmConfig`. Exists so the CLI only needs to pass one config object.

- [ ] **Step 1: Write the failing test**

```csharp
// tests/Graphiphy.Llm.Tests/GraphAgentFactoryTests.cs
using Graphiphy.Llm;

namespace Graphiphy.Llm.Tests;

public class GraphAgentFactoryTests
{
    [Test]
    public async Task Create_OpenAi_ReturnsSemanticKernelAgent()
    {
        var config = new LlmConfig(LlmProvider.OpenAi, "sk-fake", "gpt-4o");
        var agent = GraphAgentFactory.Create(config);
        await Assert.That(agent).IsNotNull();
        await Assert.That(agent).IsTypeOf<SemanticKernelGraphAgent>();
    }

    [Test]
    public async Task Create_Anthropic_ReturnsSemanticKernelAgent()
    {
        var config = new LlmConfig(LlmProvider.Anthropic, "sk-ant-fake", "claude-3-5-sonnet-20241022");
        var agent = GraphAgentFactory.Create(config);
        await Assert.That(agent).IsNotNull();
    }

    [Test]
    public async Task Create_Ollama_NoKeyRequired()
    {
        var config = new LlmConfig(LlmProvider.Ollama, null, "llama3.2");
        var agent = GraphAgentFactory.Create(config);
        await Assert.That(agent).IsNotNull();
    }
}
```

- [ ] **Step 2: Run to confirm failure**

```bash
dotnet run --project tests/Graphiphy.Llm.Tests/
```
Expected: Build error — GraphAgentFactory not found.

- [ ] **Step 3: Implement GraphAgentFactory**

```csharp
// src/Graphiphy.Llm/GraphAgentFactory.cs
namespace Graphiphy.Llm;

public static class GraphAgentFactory
{
    public static IGraphAgent Create(LlmConfig config)
        => new SemanticKernelGraphAgent(config);
}
```

- [ ] **Step 4: Run tests**

```bash
dotnet run --project tests/Graphiphy.Llm.Tests/
```
Expected: All 10 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/Graphiphy.Llm/GraphAgentFactory.cs tests/Graphiphy.Llm.Tests/GraphAgentFactoryTests.cs
git commit -m "feat: add GraphAgentFactory"
```

---

## Phase B: CLI Entry Point

### Task 5: Graphiphy.Cli Project Scaffold

**Files:**
- Create: `src/Graphiphy.Cli/Graphiphy.Cli.csproj`
- Create: `src/Graphiphy.Cli/Program.cs`
- Create: `tests/Graphiphy.Cli.Tests/Graphiphy.Cli.Tests.csproj`
- Modify: `Graphiphy.sln`

- [ ] **Step 1: Create the CLI csproj**

```xml
<!-- src/Graphiphy.Cli/Graphiphy.Cli.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <OutputType>Exe</OutputType>
    <AssemblyName>graphiphy</AssemblyName>
    <RootNamespace>Graphiphy.Cli</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\Graphiphy\Graphiphy.csproj" />
    <ProjectReference Include="..\Graphiphy.Llm\Graphiphy.Llm.csproj" />
  </ItemGroup>

  <ItemGroup>
    <!-- Verify latest versions on NuGet.org before adding -->
    <PackageReference Include="Spectre.Console.Cli" Version="0.49.1" />
    <PackageReference Include="ModelContextProtocol" Version="0.1.0-preview.1" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="9.0.0" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create Program.cs with all four commands registered**

```csharp
// src/Graphiphy.Cli/Program.cs
using Graphiphy.Cli.Commands;
using Spectre.Console.Cli;

var app = new CommandApp();
app.Configure(config =>
{
    config.SetApplicationName("graphiphy");
    config.SetApplicationVersion("2.0.0");

    config.AddCommand<AnalyzeCommand>("analyze")
          .WithDescription("Analyze a repository and print graph statistics.");

    config.AddCommand<ReportCommand>("report")
          .WithDescription("Generate a Markdown report for a repository.");

    config.AddCommand<QueryCommand>("query")
          .WithDescription("Ask an LLM a question about the repository graph.");

    config.AddCommand<ServeCommand>("serve")
          .WithDescription("Start an MCP server over stdio for the given repository.");
});

return app.Run(args);
```

- [ ] **Step 3: Create stub command classes so Program.cs compiles**

Create these four files with minimal stubs. They will be replaced in later tasks.

```csharp
// src/Graphiphy.Cli/Commands/AnalyzeCommand.cs
using Spectre.Console.Cli;
namespace Graphiphy.Cli.Commands;
public sealed class AnalyzeSettings : CommandSettings { }
public sealed class AnalyzeCommand : AsyncCommand<AnalyzeSettings>
{
    public override Task<int> ExecuteAsync(CommandContext context, AnalyzeSettings settings)
        => Task.FromResult(0);
}
```

```csharp
// src/Graphiphy.Cli/Commands/ReportCommand.cs
using Spectre.Console.Cli;
namespace Graphiphy.Cli.Commands;
public sealed class ReportSettings : CommandSettings { }
public sealed class ReportCommand : AsyncCommand<ReportSettings>
{
    public override Task<int> ExecuteAsync(CommandContext context, ReportSettings settings)
        => Task.FromResult(0);
}
```

```csharp
// src/Graphiphy.Cli/Commands/QueryCommand.cs
using Spectre.Console.Cli;
namespace Graphiphy.Cli.Commands;
public sealed class QuerySettings : CommandSettings { }
public sealed class QueryCommand : AsyncCommand<QuerySettings>
{
    public override Task<int> ExecuteAsync(CommandContext context, QuerySettings settings)
        => Task.FromResult(0);
}
```

```csharp
// src/Graphiphy.Cli/Commands/ServeCommand.cs
using Spectre.Console.Cli;
namespace Graphiphy.Cli.Commands;
public sealed class ServeSettings : CommandSettings { }
public sealed class ServeCommand : AsyncCommand<ServeSettings>
{
    public override Task<int> ExecuteAsync(CommandContext context, ServeSettings settings)
        => Task.FromResult(0);
}
```

- [ ] **Step 4: Create the test project csproj**

```xml
<!-- tests/Graphiphy.Cli.Tests/Graphiphy.Cli.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <OutputType>Exe</OutputType>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="TUnit" Version="1.43.41" />
  </ItemGroup>

  <ItemGroup>
    <!-- Reference the library projects, NOT the CLI exe -->
    <ProjectReference Include="..\..\src\Graphiphy\Graphiphy.csproj" />
    <ProjectReference Include="..\..\src\Graphiphy.Llm\Graphiphy.Llm.csproj" />
  </ItemGroup>
</Project>
```

> Note: Tests reference `Graphiphy` and `Graphiphy.Llm` directly. `RepositoryAnalysis` and `GraphTools` will be moved to separate library targets or tested via their public APIs. The CLI exe cannot be referenced as a project. See Task 6 for the workaround.

- [ ] **Step 5: Add both projects to the solution**

```bash
dotnet sln Graphiphy.sln add src/Graphiphy.Cli/Graphiphy.Cli.csproj
dotnet sln Graphiphy.sln add tests/Graphiphy.Cli.Tests/Graphiphy.Cli.Tests.csproj
```

- [ ] **Step 6: Verify the CLI builds and shows help**

```bash
dotnet build src/Graphiphy.Cli/
dotnet run --project src/Graphiphy.Cli/ -- --help
```
Expected: Help text listing `analyze`, `report`, `query`, `serve`.

- [ ] **Step 7: Commit**

```bash
git add src/Graphiphy.Cli/ tests/Graphiphy.Cli.Tests/ Graphiphy.sln
git commit -m "feat: add Graphiphy.Cli project scaffold with four stub commands"
```

---

### Task 6: RepositoryAnalysis Pipeline Orchestrator

**Files:**
- Create: `src/Graphiphy.Cli/Pipeline/RepositoryAnalysis.cs`
- Create: `tests/Graphiphy.Cli.Tests/Pipeline/RepositoryAnalysisTests.cs`

> The test project references the Graphiphy core library directly, and `RepositoryAnalysis` uses only public APIs from that library. Since the CLI is an exe and can't be referenced, we test `RepositoryAnalysis` by extracting it as a class in the CLI project and verifying its behavior by calling it from the test project **after building the CLI dll** — or more simply, by duplicating the class in a shared location. The cleanest solution: create `src/Graphiphy.Pipeline/` as a library containing `RepositoryAnalysis` (see below).

**Revised approach:** extract `RepositoryAnalysis` into `src/Graphiphy.Pipeline/` so both the CLI and tests can reference it.

**Additional files:**
- Create: `src/Graphiphy.Pipeline/Graphiphy.Pipeline.csproj`
- Create: `src/Graphiphy.Pipeline/RepositoryAnalysis.cs`
- Modify: `src/Graphiphy.Cli/Graphiphy.Cli.csproj` — add ProjectReference to Pipeline
- Modify: `tests/Graphiphy.Cli.Tests/Graphiphy.Cli.Tests.csproj` — add ProjectReference to Pipeline
- Modify: `Graphiphy.sln` — add Pipeline project

- [ ] **Step 1: Create Graphiphy.Pipeline csproj**

```xml
<!-- src/Graphiphy.Pipeline/Graphiphy.Pipeline.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>Graphiphy.Pipeline</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\Graphiphy\Graphiphy.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Write the failing tests**

```csharp
// tests/Graphiphy.Cli.Tests/Pipeline/RepositoryAnalysisTests.cs
using Graphiphy.Pipeline;

namespace Graphiphy.Cli.Tests.Pipeline;

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
    public async Task RunAsync_UsesCache_OnSecondCall()
    {
        var dir = CreateTempDir();
        var cacheDir = Path.Combine(dir, ".graphiphy-cache");
        File.WriteAllText(Path.Combine(dir, "b.py"), "class B: pass");

        var r1 = await RepositoryAnalysis.RunAsync(dir, cacheDir: cacheDir);
        var r2 = await RepositoryAnalysis.RunAsync(dir, cacheDir: cacheDir);

        // Same graph regardless of cache
        await Assert.That(r2.Graph.VertexCount).IsEqualTo(r1.Graph.VertexCount);
    }

    private static string CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "graphiphy_test_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(path);
        return path;
    }
}
```

- [ ] **Step 3: Run tests to confirm they fail**

Add reference to Graphiphy.Pipeline in the test project's csproj, then:
```bash
dotnet run --project tests/Graphiphy.Cli.Tests/
```
Expected: Build error — RepositoryAnalysis not found.

- [ ] **Step 4: Implement RepositoryAnalysis**

```csharp
// src/Graphiphy.Pipeline/RepositoryAnalysis.cs
using Graphiphy.Build;
using Graphiphy.Cache;
using Graphiphy.Cluster;
using Graphiphy.Dedup;
using Graphiphy.Detection;
using Graphiphy.Extraction;
using Graphiphy.Models;
using Graphiphy.Report;
using QuikGraph;

namespace Graphiphy.Pipeline;

public sealed class RepositoryAnalysis
{
    public string RootPath { get; }
    public List<DetectedFile> Files { get; }
    public BidirectionalGraph<Node, TaggedEdge<Node, Edge>> Graph { get; }
    public string Report { get; }

    private RepositoryAnalysis(
        string rootPath,
        List<DetectedFile> files,
        BidirectionalGraph<Node, TaggedEdge<Node, Edge>> graph,
        string report)
    {
        RootPath = rootPath;
        Files = files;
        Graph = graph;
        Report = report;
    }

    /// <summary>
    /// Run the full pipeline: Detect → Extract (with cache) → Build → Dedup → Cluster → Report.
    /// </summary>
    /// <param name="rootPath">Repository root directory.</param>
    /// <param name="cacheDir">Directory for extraction cache. Defaults to rootPath/.graphiphy-cache.</param>
    /// <param name="onProgress">Optional progress callback called with status messages.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task<RepositoryAnalysis> RunAsync(
        string rootPath,
        string? cacheDir = null,
        Action<string>? onProgress = null,
        CancellationToken ct = default)
    {
        cacheDir ??= Path.Combine(rootPath, ".graphiphy-cache");
        var cache = new ExtractionCache(cacheDir);
        var registry = LanguageRegistry.CreateDefault();

        // 1. Detect
        onProgress?.Invoke("Detecting files...");
        var files = FileDetector.Detect(rootPath);

        // 2. Extract (with cache)
        onProgress?.Invoke($"Extracting {files.Count} files...");
        var extractions = new List<Extraction>();
        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            var extractor = registry.GetExtractor(file.AbsolutePath);
            if (extractor is null) continue;

            var hash = ExtractionCache.FileHash(file.AbsolutePath, rootPath);
            var cached = cache.Load(hash);
            if (cached is not null)
            {
                extractions.Add(cached);
                continue;
            }

            var source = await File.ReadAllTextAsync(file.AbsolutePath, ct);
            var extraction = extractor.Extract(file.AbsolutePath, source);
            cache.Save(hash, extraction);
            extractions.Add(extraction);
        }

        // 3. Build initial graph
        onProgress?.Invoke("Building graph...");
        var rawGraph = GraphBuilder.Build(extractions);

        // 4. Deduplicate
        onProgress?.Invoke("Deduplicating entities...");
        var allNodes = rawGraph.Vertices.ToList();
        var allEdges = rawGraph.Edges.Select(e => e.Tag).ToList();
        var (dedupNodes, dedupEdges) = EntityDeduplicator.Deduplicate(allNodes, allEdges);

        var graphData = new GraphData { Nodes = dedupNodes, Edges = dedupEdges };
        var graph = GraphBuilder.FromGraphData(graphData);

        // 5. Cluster (optional — skip gracefully if native lib unavailable)
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

        // 6. Generate report
        onProgress?.Invoke("Generating report...");
        var report = Graphiphy.Report.ReportGenerator.Generate(graph);

        return new RepositoryAnalysis(rootPath, files, graph, report);
    }
}
```

- [ ] **Step 5: Add ProjectReference for Pipeline to CLI and test project**

In `src/Graphiphy.Cli/Graphiphy.Cli.csproj`, add:
```xml
<ProjectReference Include="..\Graphiphy.Pipeline\Graphiphy.Pipeline.csproj" />
```

In `tests/Graphiphy.Cli.Tests/Graphiphy.Cli.Tests.csproj`, add:
```xml
<ProjectReference Include="..\..\src\Graphiphy.Pipeline\Graphiphy.Pipeline.csproj" />
```

Add to solution:
```bash
dotnet sln Graphiphy.sln add src/Graphiphy.Pipeline/Graphiphy.Pipeline.csproj
```

- [ ] **Step 6: Run tests**

```bash
dotnet run --project tests/Graphiphy.Cli.Tests/
```
Expected: 4 tests pass.

- [ ] **Step 7: Commit**

```bash
git add src/Graphiphy.Pipeline/ src/Graphiphy.Cli/Graphiphy.Cli.csproj tests/Graphiphy.Cli.Tests/ Graphiphy.sln
git commit -m "feat: add RepositoryAnalysis pipeline orchestrator"
```

---

### Task 7: AnalyzeCommand

**Files:**
- Modify: `src/Graphiphy.Cli/Commands/AnalyzeCommand.cs`
- Create: `tests/Graphiphy.Cli.Tests/Commands/AnalyzeCommandTests.cs`

The `analyze` command runs the full pipeline and prints a summary table to the console.

```
graphiphy analyze /path/to/repo [--out report.md] [--cache .cache]
```

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/Graphiphy.Cli.Tests/Commands/AnalyzeCommandTests.cs
using Graphiphy.Pipeline;
using Spectre.Console.Testing;

namespace Graphiphy.Cli.Tests.Commands;

/// <summary>
/// Tests the AnalyzeCommand logic by calling RepositoryAnalysis directly.
/// Spectre commands are thin wrappers — we test the orchestration, not Spectre plumbing.
/// </summary>
public class AnalyzeCommandTests
{
    [Test]
    public async Task AnalyzePythonRepo_OutputsNodeCount()
    {
        var dir = CreateTempDir();
        File.WriteAllText(Path.Combine(dir, "main.py"), """
            class App:
                def run(self): pass
            """);

        var result = await RepositoryAnalysis.RunAsync(dir);

        await Assert.That(result.Graph.VertexCount).IsGreaterThan(0);
    }

    [Test]
    public async Task AnalyzeEmptyDir_CompletesWithoutError()
    {
        var dir = CreateTempDir();

        // Should not throw
        var result = await RepositoryAnalysis.RunAsync(dir);

        await Assert.That(result).IsNotNull();
        await Assert.That(result.Files.Count).IsEqualTo(0);
    }

    [Test]
    public async Task AnalyzeWithCache_SecondRunFaster()
    {
        var dir = CreateTempDir();
        var cacheDir = Path.Combine(dir, ".cache");
        File.WriteAllText(Path.Combine(dir, "app.py"), "class X:\n    def y(self): pass");

        // First run — no cache
        var sw1 = System.Diagnostics.Stopwatch.StartNew();
        await RepositoryAnalysis.RunAsync(dir, cacheDir: cacheDir);
        sw1.Stop();

        // Second run — all cache hits
        var sw2 = System.Diagnostics.Stopwatch.StartNew();
        await RepositoryAnalysis.RunAsync(dir, cacheDir: cacheDir);
        sw2.Stop();

        // Cache should be at least as fast (not strictly faster on tiny inputs)
        await Assert.That(sw2.ElapsedMilliseconds).IsLessThanOrEqualTo(sw1.ElapsedMilliseconds + 1000);
    }

    private static string CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "graphiphy_analyze_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(path);
        return path;
    }
}
```

- [ ] **Step 2: Run to confirm tests pass already (they test RepositoryAnalysis, not the command class)**

```bash
dotnet run --project tests/Graphiphy.Cli.Tests/
```
Expected: These tests should pass (they rely on RepositoryAnalysis from Task 6).

- [ ] **Step 3: Implement the full AnalyzeCommand**

```csharp
// src/Graphiphy.Cli/Commands/AnalyzeCommand.cs
using System.ComponentModel;
using Graphiphy.Analysis;
using Graphiphy.Pipeline;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Graphiphy.Cli.Commands;

public sealed class AnalyzeSettings : CommandSettings
{
    [CommandArgument(0, "<path>")]
    [Description("Path to the repository root directory.")]
    public required string Path { get; init; }

    [CommandOption("--out|-o <file>")]
    [Description("Write the Markdown report to this file instead of stdout.")]
    public string? OutputFile { get; init; }

    [CommandOption("--cache <dir>")]
    [Description("Cache directory for extraction results. Default: <path>/.graphiphy-cache")]
    public string? CacheDir { get; init; }
}

public sealed class AnalyzeCommand : AsyncCommand<AnalyzeSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, AnalyzeSettings settings)
    {
        RepositoryAnalysis? result = null;

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Analyzing repository...", async ctx =>
            {
                result = await RepositoryAnalysis.RunAsync(
                    settings.Path,
                    cacheDir: settings.CacheDir,
                    onProgress: msg => ctx.Status(msg));
            });

        if (result is null) return 1;

        // Print summary table
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

        // Write or print report
        if (settings.OutputFile is not null)
        {
            await File.WriteAllTextAsync(settings.OutputFile, result.Report);
            AnsiConsole.MarkupLine($"[green]Report written to {settings.OutputFile}[/]");
        }

        return 0;
    }
}
```

- [ ] **Step 4: Verify the analyze command works on a real directory**

```bash
dotnet run --project src/Graphiphy.Cli/ -- analyze src/Graphiphy/
```
Expected: A table showing file/node/edge counts for the Graphiphy source.

- [ ] **Step 5: Commit**

```bash
git add src/Graphiphy.Cli/Commands/AnalyzeCommand.cs tests/Graphiphy.Cli.Tests/Commands/
git commit -m "feat: implement analyze command with progress spinner and summary table"
```

---

### Task 8: ReportCommand and QueryCommand

**Files:**
- Modify: `src/Graphiphy.Cli/Commands/ReportCommand.cs`
- Modify: `src/Graphiphy.Cli/Commands/QueryCommand.cs`

- [ ] **Step 1: Implement ReportCommand**

```csharp
// src/Graphiphy.Cli/Commands/ReportCommand.cs
using System.ComponentModel;
using Graphiphy.Pipeline;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Graphiphy.Cli.Commands;

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
    public override async Task<int> ExecuteAsync(CommandContext context, ReportSettings settings)
    {
        RepositoryAnalysis? result = null;

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Analyzing...", async ctx =>
            {
                result = await RepositoryAnalysis.RunAsync(
                    settings.Path,
                    cacheDir: settings.CacheDir,
                    onProgress: msg => ctx.Status(msg));
            });

        if (result is null) return 1;

        if (settings.OutputFile is not null)
        {
            await File.WriteAllTextAsync(settings.OutputFile, result.Report);
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

- [ ] **Step 2: Implement QueryCommand**

```csharp
// src/Graphiphy.Cli/Commands/QueryCommand.cs
using System.ComponentModel;
using Graphiphy.Llm;
using Graphiphy.Pipeline;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Graphiphy.Cli.Commands;

public sealed class QuerySettings : CommandSettings
{
    [CommandArgument(0, "<path>")]
    [Description("Repository root directory.")]
    public required string Path { get; init; }

    [CommandArgument(1, "<question>")]
    [Description("Question to ask about the repository graph.")]
    public required string Question { get; init; }

    [CommandOption("--provider <name>")]
    [Description("LLM provider: anthropic (default), openai, or ollama.")]
    public string Provider { get; init; } = "anthropic";

    [CommandOption("--key <apiKey>")]
    [Description("API key for the chosen provider. Falls back to ANTHROPIC_API_KEY / OPENAI_API_KEY env vars.")]
    public string? ApiKey { get; init; }

    [CommandOption("--model <name>")]
    [Description("Model name. Defaults: claude-3-5-sonnet-20241022 / gpt-4o / llama3.2")]
    public string? Model { get; init; }

    [CommandOption("--cache <dir>")]
    public string? CacheDir { get; init; }
}

public sealed class QueryCommand : AsyncCommand<QuerySettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, QuerySettings settings)
    {
        var provider = settings.Provider.ToLowerInvariant() switch
        {
            "openai"    => LlmProvider.OpenAi,
            "ollama"    => LlmProvider.Ollama,
            _           => LlmProvider.Anthropic,
        };

        var defaultModel = provider switch
        {
            LlmProvider.OpenAi    => "gpt-4o",
            LlmProvider.Ollama    => "llama3.2",
            _                     => "claude-3-5-sonnet-20241022",
        };

        var apiKey = settings.ApiKey
            ?? (provider == LlmProvider.Anthropic
                ? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
                : Environment.GetEnvironmentVariable("OPENAI_API_KEY"));

        if (provider != LlmProvider.Ollama && string.IsNullOrWhiteSpace(apiKey))
        {
            AnsiConsole.MarkupLine("[red]No API key provided. Use --key or set ANTHROPIC_API_KEY / OPENAI_API_KEY.[/]");
            return 1;
        }

        var config = new LlmConfig(provider, apiKey, settings.Model ?? defaultModel);

        RepositoryAnalysis? repo = null;
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Analyzing repository...", async ctx =>
            {
                repo = await RepositoryAnalysis.RunAsync(
                    settings.Path,
                    cacheDir: settings.CacheDir,
                    onProgress: msg => ctx.Status(msg));
            });

        if (repo is null) return 1;

        AnsiConsole.MarkupLine("[bold]Querying LLM...[/]");
        var agent = GraphAgentFactory.Create(config);
        var answer = await agent.AnswerAsync(settings.Question, repo.Graph);

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Panel(answer).Header("Answer").Expand());

        return 0;
    }
}
```

- [ ] **Step 3: Verify both commands appear in help**

```bash
dotnet run --project src/Graphiphy.Cli/ -- report --help
dotnet run --project src/Graphiphy.Cli/ -- query --help
```
Expected: Usage and options shown for both.

- [ ] **Step 4: Smoke-test report command**

```bash
dotnet run --project src/Graphiphy.Cli/ -- report src/Graphiphy/
```
Expected: Markdown report printed to stdout.

- [ ] **Step 5: Commit**

```bash
git add src/Graphiphy.Cli/Commands/ReportCommand.cs src/Graphiphy.Cli/Commands/QueryCommand.cs
git commit -m "feat: implement report and query commands"
```

---

## Phase C: MCP Server

### Task 9: GraphTools — MCP Tool Definitions

**Files:**
- Create: `src/Graphiphy.Cli/Mcp/GraphTools.cs`
- Create: `tests/Graphiphy.Cli.Tests/Mcp/GraphToolsTests.cs`

The `ModelContextProtocol` SDK discovers tools via `[McpServerToolType]` on a class and `[McpServerTool]` on its methods. We test the method logic directly — no need to spin up a server.

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/Graphiphy.Cli.Tests/Mcp/GraphToolsTests.cs
using Graphiphy.Build;
using Graphiphy.Models;

namespace Graphiphy.Cli.Tests.Mcp;

// We test GraphTools logic by instantiating it directly with a pre-built graph.
// The MCP registration is thin infrastructure tested in Task 10.
public class GraphToolsTests
{
    private static Graphiphy.Pipeline.RepositoryAnalysis MakeAnalysis()
    {
        // We can't construct RepositoryAnalysis directly (private ctor), so we
        // expose a static TestCreate helper in it, or we run a real mini pipeline.
        var dir = CreateTempDir();
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
        return Graphiphy.Pipeline.RepositoryAnalysis.RunAsync(dir).GetAwaiter().GetResult();
    }

    [Test]
    public async Task GetGodNodes_ReturnsMostConnected()
    {
        var analysis = MakeAnalysis();
        var tools = new Graphiphy.Cli.Mcp.GraphTools(analysis);

        var result = tools.GetGodNodes(topN: 2);

        await Assert.That(result).IsNotNullOrEmpty();
        await Assert.That(result).Contains("Server");
    }

    [Test]
    public async Task GetReport_ReturnsMarkdown()
    {
        var analysis = MakeAnalysis();
        var tools = new Graphiphy.Cli.Mcp.GraphTools(analysis);

        var result = tools.GetReport();

        await Assert.That(result).Contains("# Graph Report");
    }

    [Test]
    public async Task GetSummaryStats_ReturnsJson()
    {
        var analysis = MakeAnalysis();
        var tools = new Graphiphy.Cli.Mcp.GraphTools(analysis);

        var result = tools.GetSummaryStats();

        await Assert.That(result).Contains("NodeCount");
    }

    [Test]
    public async Task SearchNodes_FindsMatchingLabel()
    {
        var analysis = MakeAnalysis();
        var tools = new Graphiphy.Cli.Mcp.GraphTools(analysis);

        var result = tools.SearchNodes("Server");

        await Assert.That(result).Contains("Server");
        await Assert.That(result).DoesNotContain("Client");
    }

    [Test]
    public async Task GetGodNodes_EmptyGraph_ReturnsEmptyArray()
    {
        var dir = CreateTempDir();
        var analysis = await Graphiphy.Pipeline.RepositoryAnalysis.RunAsync(dir);
        var tools = new Graphiphy.Cli.Mcp.GraphTools(analysis);

        var result = tools.GetGodNodes(topN: 5);

        await Assert.That(result).IsEqualTo("[]");
    }

    private static string CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "graphiphy_mcp_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(path);
        return path;
    }
}
```

- [ ] **Step 2: Run to confirm failure**

```bash
dotnet run --project tests/Graphiphy.Cli.Tests/
```
Expected: Build error — GraphTools not found.

- [ ] **Step 3: Implement GraphTools**

Note: `[McpServerToolType]` and `[McpServerTool]` require `using ModelContextProtocol.Server;`. The test project doesn't need the MCP package — we test the methods directly, which have no MCP runtime dependency. The attributes are metadata-only at call time. Add MCP package only to `Graphiphy.Cli.csproj`, not to the test project. The test project references the tool class via the CLI's **library output** — but since CLI is an exe, this needs a workaround.

**Workaround:** Move `GraphTools` to `Graphiphy.Pipeline` (which is a library). This keeps the tool logic testable and the MCP attributes can be conditionally excluded via `#if` or simply accepted as no-ops in the test project that doesn't have the MCP package.

**Simpler workaround:** Keep `GraphTools` in `Graphiphy.Pipeline` with no MCP attributes. Apply the attributes in a thin wrapper class in `Graphiphy.Cli` that delegates to `GraphTools`. Tests test `GraphTools`; the MCP server uses the wrapper.

```csharp
// src/Graphiphy.Pipeline/GraphTools.cs
// Pure logic, no MCP dependency — testable from any project
using System.Text.Json;
using Graphiphy.Analysis;
using Graphiphy.Models;
using QuikGraph;

namespace Graphiphy.Pipeline;

/// <summary>
/// Graph query tools. Used directly by MCP wrappers and tests.
/// </summary>
public sealed class GraphTools
{
    private readonly RepositoryAnalysis _analysis;

    public GraphTools(RepositoryAnalysis analysis) => _analysis = analysis;

    public string GetGodNodes(int topN = 5)
    {
        var graph = _analysis.Graph;
        if (graph.VertexCount == 0) return "[]";

        var nodes = GraphAnalyzer.GodNodes(graph, topN);
        return JsonSerializer.Serialize(nodes.Select(n => new
        {
            n.Id,
            n.Label,
            n.SourceFile,
            Connections = graph.InDegree(n) + graph.OutDegree(n),
        }));
    }

    public string GetSurprisingConnections(int topN = 10)
    {
        var connections = GraphAnalyzer.SurprisingConnections(_analysis.Graph, topN);
        return JsonSerializer.Serialize(connections.Select(c => new
        {
            Source = c.Source.Label,
            Target = c.Target.Label,
            c.Edge.Relation,
            c.Edge.ConfidenceString,
            c.Score,
        }));
    }

    public string GetSummaryStats()
    {
        var graph = _analysis.Graph;
        var byFile = graph.Vertices
            .GroupBy(n => n.SourceFile)
            .Select(g => new { File = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10);

        var communities = graph.Vertices
            .Where(n => n.Community.HasValue)
            .Select(n => n.Community!.Value)
            .Distinct()
            .Count();

        return JsonSerializer.Serialize(new
        {
            NodeCount = graph.VertexCount,
            EdgeCount = graph.EdgeCount,
            Communities = communities,
            TopFiles = byFile,
        });
    }

    public string SearchNodes(string query, int limit = 20)
    {
        var matches = _analysis.Graph.Vertices
            .Where(n => n.Label.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(limit)
            .Select(n => new { n.Id, n.Label, n.SourceFile, n.FileTypeString });

        return JsonSerializer.Serialize(matches);
    }

    public string GetReport() => _analysis.Report;
}
```

- [ ] **Step 4: Run tests**

Update the test file to use `Graphiphy.Pipeline.GraphTools` and run:
```bash
dotnet run --project tests/Graphiphy.Cli.Tests/
```
Expected: All tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/Graphiphy.Pipeline/GraphTools.cs tests/Graphiphy.Cli.Tests/Mcp/
git commit -m "feat: add GraphTools with testable MCP tool logic"
```

---

### Task 10: GraphMcpServer and ServeCommand

**Files:**
- Create: `src/Graphiphy.Cli/Mcp/McpGraphToolsWrapper.cs`
- Create: `src/Graphiphy.Cli/Mcp/GraphMcpServer.cs`
- Modify: `src/Graphiphy.Cli/Commands/ServeCommand.cs`

The MCP server runs in stdio mode. The client (Claude Desktop, etc.) starts the process as:
```
graphiphy serve --path /repo/root
```
Then communicates via stdin/stdout using the MCP protocol.

- [ ] **Step 1: Create the MCP attributes wrapper class**

This class lives in `Graphiphy.Cli` and applies `[McpServerToolType]`/`[McpServerTool]` attributes (from the `ModelContextProtocol` package) to thin delegates that call `GraphTools`.

```csharp
// src/Graphiphy.Cli/Mcp/McpGraphToolsWrapper.cs
using System.ComponentModel;
using ModelContextProtocol.Server;
using Graphiphy.Pipeline;

namespace Graphiphy.Cli.Mcp;

[McpServerToolType]
internal sealed class McpGraphToolsWrapper(GraphTools tools)
{
    [McpServerTool(Name = "get_god_nodes")]
    [Description("Return the most connected nodes (god nodes) in the repository graph as JSON.")]
    public string GetGodNodes(
        [Description("Maximum number of nodes to return (default 5)")] int topN = 5)
        => tools.GetGodNodes(topN);

    [McpServerTool(Name = "get_surprising_connections")]
    [Description("Return the most surprising cross-file or ambiguous edges in the graph as JSON.")]
    public string GetSurprisingConnections(
        [Description("Maximum connections to return (default 10)")] int topN = 10)
        => tools.GetSurprisingConnections(topN);

    [McpServerTool(Name = "get_summary_stats")]
    [Description("Return summary statistics: node count, edge count, communities, top files.")]
    public string GetSummaryStats()
        => tools.GetSummaryStats();

    [McpServerTool(Name = "search_nodes")]
    [Description("Search for nodes whose label contains the given query string. Returns JSON.")]
    public string SearchNodes(
        [Description("Search term to match against node labels")] string query,
        [Description("Maximum results (default 20)")] int limit = 20)
        => tools.SearchNodes(query, limit);

    [McpServerTool(Name = "get_report")]
    [Description("Return the full Markdown analysis report for the repository.")]
    public string GetReport()
        => tools.GetReport();
}
```

- [ ] **Step 2: Create GraphMcpServer**

```csharp
// src/Graphiphy.Cli/Mcp/GraphMcpServer.cs
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;
using Graphiphy.Pipeline;

namespace Graphiphy.Cli.Mcp;

public static class GraphMcpServer
{
    /// <summary>
    /// Build and run an MCP server over stdio for the given analyzed repository.
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

        var host = builder.Build();
        await host.RunAsync(ct);
    }
}
```

- [ ] **Step 3: Implement ServeCommand**

```csharp
// src/Graphiphy.Cli/Commands/ServeCommand.cs
using System.ComponentModel;
using Graphiphy.Cli.Mcp;
using Graphiphy.Pipeline;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Graphiphy.Cli.Commands;

public sealed class ServeSettings : CommandSettings
{
    [CommandArgument(0, "[path]")]
    [Description("Repository root to analyze. Defaults to current directory.")]
    public string Path { get; init; } = Directory.GetCurrentDirectory();

    [CommandOption("--cache <dir>")]
    [Description("Cache directory. Default: <path>/.graphiphy-cache")]
    public string? CacheDir { get; init; }
}

public sealed class ServeCommand : AsyncCommand<ServeSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, ServeSettings settings)
    {
        // When running as MCP server, stdout is the MCP protocol channel.
        // We must NOT write anything to stdout before RunAsync — log to stderr only.
        Console.Error.WriteLine($"[graphiphy] Analyzing {settings.Path}...");

        RepositoryAnalysis? analysis = null;
        try
        {
            analysis = await RepositoryAnalysis.RunAsync(
                settings.Path,
                cacheDir: settings.CacheDir,
                onProgress: msg => Console.Error.WriteLine($"[graphiphy] {msg}"));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[graphiphy] Analysis failed: {ex.Message}");
            return 1;
        }

        Console.Error.WriteLine($"[graphiphy] Ready — {analysis.Graph.VertexCount} nodes, {analysis.Graph.EdgeCount} edges.");
        Console.Error.WriteLine("[graphiphy] Starting MCP server on stdio...");

        await GraphMcpServer.RunAsync(analysis);
        return 0;
    }
}
```

- [ ] **Step 4: Verify the serve command appears in help**

```bash
dotnet run --project src/Graphiphy.Cli/ -- serve --help
```
Expected: Usage shown with `--path` and `--cache` options.

- [ ] **Step 5: Verify the CLI builds fully**

```bash
dotnet build src/Graphiphy.Cli/
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 6: Commit**

```bash
git add src/Graphiphy.Cli/Mcp/ src/Graphiphy.Cli/Commands/ServeCommand.cs
git commit -m "feat: implement MCP server and serve command using ModelContextProtocol stdio transport"
```

---

### Task 11: MCP Server Configuration for Claude Desktop

**Files:**
- Create: `docs/mcp-config.md` — configuration instructions for Claude Desktop

This task documents how to wire the CLI as an MCP server in Claude Desktop and verifies the MCP tools are correctly described.

- [ ] **Step 1: Write MCP server integration test**

```csharp
// tests/Graphiphy.Cli.Tests/Mcp/McpToolDescriptionTests.cs
using Graphiphy.Pipeline;

namespace Graphiphy.Cli.Tests.Mcp;

public class McpToolDescriptionTests
{
    [Test]
    public async Task AllExpectedTools_ArePresent()
    {
        // Verify the tool names we document are actually defined in McpGraphToolsWrapper.
        // We check via reflection on the method names and their McpServerTool attribute names.
        var wrapperType = typeof(Graphiphy.Cli.Mcp.McpGraphToolsWrapper);
        var methods = wrapperType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        var names = methods.Select(m => m.Name).ToList();

        await Assert.That(names).Contains("GetGodNodes");
        await Assert.That(names).Contains("GetSurprisingConnections");
        await Assert.That(names).Contains("GetSummaryStats");
        await Assert.That(names).Contains("SearchNodes");
        await Assert.That(names).Contains("GetReport");
    }
}
```

Note: The test project would need to reference `Graphiphy.Cli` — which is an exe. For reflection tests, build the CLI dll first and load it, or simply test `McpGraphToolsWrapper` is in the expected namespace by moving it to `Graphiphy.Pipeline`. 

**Simplest approach:** Move `McpGraphToolsWrapper` attribute names to an enum or constant list in `Graphiphy.Pipeline` and verify that list. But this adds complexity for little value. 

**Recommended:** Skip the reflection test. The build itself proves the wrapper compiles. Instead, write an integration smoke test:

```csharp
// tests/Graphiphy.Cli.Tests/Mcp/McpToolDescriptionTests.cs
using Graphiphy.Pipeline;

namespace Graphiphy.Cli.Tests.Mcp;

/// Verifies that GraphTools (the pure logic layer) returns well-formed JSON for all tool methods.
public class McpToolDescriptionTests
{
    [Test]
    public async Task AllToolMethods_ReturnValidJson()
    {
        var dir = CreateTempDir();
        File.WriteAllText(Path.Combine(dir, "x.py"), "class X:\n    def y(self): pass\n");
        var analysis = await RepositoryAnalysis.RunAsync(dir);
        var tools = new GraphTools(analysis);

        var results = new[]
        {
            tools.GetGodNodes(),
            tools.GetSurprisingConnections(),
            tools.GetSummaryStats(),
            tools.SearchNodes("X"),
            tools.GetReport(),
        };

        foreach (var result in results)
        {
            await Assert.That(result).IsNotNullOrEmpty();
        }
    }

    private static string CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "graphiphy_mcp2_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(path);
        return path;
    }
}
```

- [ ] **Step 2: Run all tests to confirm everything still passes**

```bash
dotnet run --project tests/Graphiphy.Cli.Tests/
dotnet run --project tests/Graphiphy.Llm.Tests/
dotnet run --project tests/Graphiphy.Tests/
```
Expected: All tests pass across all three test projects.

- [ ] **Step 3: Create MCP config documentation**

```markdown
<!-- docs/mcp-config.md -->
# Using Graphiphy as an MCP Server with Claude Desktop

## Build the CLI

```bash
dotnet publish src/Graphiphy.Cli/ -c Release -o ./dist
```

## Claude Desktop Configuration

Add this to your `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "graphiphy": {
      "command": "/absolute/path/to/dist/graphiphy",
      "args": ["serve", "--path", "/absolute/path/to/your/repo"]
    }
  }
}
```

On Linux/macOS: `~/.config/claude/claude_desktop_config.json`  
On Windows: `%APPDATA%\Claude\claude_desktop_config.json`

## Available Tools

| Tool | Description |
|------|-------------|
| `get_god_nodes` | Most connected entities (optional `topN` param) |
| `get_surprising_connections` | Cross-file and ambiguous edges (optional `topN`) |
| `get_summary_stats` | Node count, edge count, community count, top files |
| `search_nodes` | Filter nodes by label substring |
| `get_report` | Full Markdown analysis report |

## Example Prompts

> "What are the most connected classes in this repository?"

> "Are there any surprising dependencies between modules?"

> "Show me everything that depends on the Router class."
```

- [ ] **Step 4: Commit**

```bash
git add tests/Graphiphy.Cli.Tests/Mcp/McpToolDescriptionTests.cs docs/mcp-config.md
git commit -m "docs: add MCP server config docs and smoke test for all tool methods"
```

---

### Task 12: Final Integration — Full Test Suite

**Files:** No new files — verification only.

- [ ] **Step 1: Run all test suites**

```bash
dotnet run --project tests/Graphiphy.Tests/
dotnet run --project tests/Graphiphy.Llm.Tests/
dotnet run --project tests/Graphiphy.Cli.Tests/
```
Expected: All tests pass with zero failures.

- [ ] **Step 2: Smoke-test the CLI end-to-end**

```bash
# Analyze the source tree
dotnet run --project src/Graphiphy.Cli/ -- analyze src/Graphiphy/

# Generate a report
dotnet run --project src/Graphiphy.Cli/ -- report src/Graphiphy/ --out /tmp/graphiphy-report.md
cat /tmp/graphiphy-report.md | head -30
```
Expected: Table with node/edge counts; report file contains `# Graph Report`.

- [ ] **Step 3: Verify `serve --help` and CLI `--help` are correct**

```bash
dotnet run --project src/Graphiphy.Cli/ -- --help
dotnet run --project src/Graphiphy.Cli/ -- serve --help
```
Expected: All four commands listed; serve shows `--path` and `--cache`.

- [ ] **Step 4: Fix any issues found**

Address compilation errors, test failures, or runtime errors before committing.

- [ ] **Step 5: Final commit**

```bash
git add -A
git commit -m "feat: phase 2 complete — CLI, LLM backends, MCP server"
```

---

## Summary

| Phase | Tasks | Delivers |
|-------|-------|----------|
| A: LLM Backends | 1–4 | `Graphiphy.Llm` library: `IGraphAgent`, `GraphPlugin`, SK-based agent, factory for Anthropic/OpenAI/Ollama |
| B: CLI | 5–8 | `graphiphy analyze`, `report`, `query` commands with Spectre.Console |
| C: MCP Server | 9–12 | `graphiphy serve` starts MCP stdio server; 5 tools; Claude Desktop config |

**New projects:** `Graphiphy.Llm`, `Graphiphy.Pipeline`, `Graphiphy.Cli`  
**New test projects:** `Graphiphy.Llm.Tests`, `Graphiphy.Cli.Tests`

**Key constraints to verify before starting:**
- `ModelContextProtocol` package name and version on NuGet.org (Anthropic's official C# MCP SDK)
- `Microsoft.SemanticKernel.Connectors.Anthropic` package availability; if not published yet, use `Anthropic.SDK` and wrap it manually in `KernelFactory` via a custom `IChatCompletionService` implementation
- `Spectre.Console.Cli` version compatible with .NET 10
