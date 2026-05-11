# Provider Resolver Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace per-provider typed options classes and duplicate inline switch statements with two resolver classes (`AgentProviderResolver`, `EmbeddingProviderResolver`) that read named provider definitions from `IConfiguration["Providers:*"]`, plus a generic OpenAI-compatible embedding provider.

**Architecture:** `appsettings.json` gains a top-level `Providers` dictionary keyed by arbitrary names; `Llm.Provider` and `Embedding.Provider` point to those names. `AgentProviderResolver` in `Ngraphiphy.Llm` reads `IConfiguration` directly, dispatches on `ApiType`, and exposes `CreateAgentAsync`. `EmbeddingProviderResolver` in `Ngraphiphy.Storage` does the same for embeddings. `QueryCommand` and `PushCommand` inject the resolvers and shed all inline switch logic, `--key`, `--model`, `--agent-url`, and `--cf-*` flags.

**Tech Stack:** .NET 10, `Microsoft.Extensions.Configuration.Abstractions`, TUnit 1.43.41, OpenAI SDK (embedding), existing MAF + Anthropic/OpenAI/Ollama/A2A packages.

---

## File map

**Created:**
- `src/Ngraphiphy.Llm/AgentProviderResolver.cs`
- `src/Ngraphiphy.Storage/Embedding/OpenAiEmbeddingProvider.cs`
- `src/Ngraphiphy.Storage/Embedding/EmbeddingProviderResolver.cs`
- `tests/Ngraphiphy.Llm.Tests/AgentProviderResolverTests.cs`
- `tests/Ngraphiphy.Storage.Tests/EmbeddingProviderResolverTests.cs`

**Modified:**
- `src/Ngraphiphy.Llm/Ngraphiphy.Llm.csproj` — add `Microsoft.Extensions.Configuration.Abstractions`
- `src/Ngraphiphy.Llm/OpenAiConfig.cs` — add `Endpoint?`, `MaxTokens?`
- `src/Ngraphiphy.Llm/AnthropicConfig.cs` — add `Endpoint?`
- `src/Ngraphiphy.Llm/GraphAgentFactory.cs` — use `Endpoint` in OpenAI/Anthropic client construction
- `src/Ngraphiphy.Cli/Configuration/CliHostExtensions.cs` — register resolvers, remove old options bindings
- `src/Ngraphiphy.Cli/Commands/QueryCommand.cs` — inject resolver, remove switch + redundant CLI flags
- `src/Ngraphiphy.Cli/Commands/PushCommand.cs` — inject resolvers, remove switches + CF-specific flags, add `--embed-provider`
- `src/Ngraphiphy.Cli/appsettings.json` — restructure to `Providers` dict + pointer fields

**Deleted:**
- `src/Ngraphiphy.Cli/Configuration/Options/LlmOptions.cs`
- `src/Ngraphiphy.Cli/Configuration/Options/EmbeddingOptions.cs`
- `src/Ngraphiphy.Storage/Embedding/CloudflareEmbeddingConfig.cs`
- `src/Ngraphiphy.Storage/Embedding/CloudflareEmbeddingProvider.cs`
- `src/Ngraphiphy.Storage/Embedding/IEmbeddingProviderConfig.cs`
- `src/Ngraphiphy.Storage/Embedding/EmbeddingProviderFactory.cs`

**Unchanged:** `GraphDatabaseOptions.cs` (DB config is not being touched).

---

## Task 1: Extend `OpenAiConfig` and `AnthropicConfig` with `Endpoint`; update `GraphAgentFactory`

**Files:**
- Modify: `src/Ngraphiphy.Llm/OpenAiConfig.cs`
- Modify: `src/Ngraphiphy.Llm/AnthropicConfig.cs`
- Modify: `src/Ngraphiphy.Llm/GraphAgentFactory.cs`
- Test: `tests/Ngraphiphy.Llm.Tests/AgentConfigTests.cs` (extend)

- [ ] **Step 1: Write failing tests**

Read `tests/Ngraphiphy.Llm.Tests/AgentConfigTests.cs` to see what's already there, then append:

```csharp
[Test]
public async Task OpenAiConfig_WithEndpoint_StoresEndpoint()
{
    var config = new OpenAiConfig("sk-test", "gpt-4o", Endpoint: "https://my.proxy/v1");
    await Assert.That(config.Endpoint).IsEqualTo("https://my.proxy/v1");
}

[Test]
public async Task AnthropicConfig_WithEndpoint_StoresEndpoint()
{
    var config = new AnthropicConfig("sk-ant-test", "claude-sonnet-4-6", Endpoint: "https://my.proxy");
    await Assert.That(config.Endpoint).IsEqualTo("https://my.proxy");
}
```

- [ ] **Step 2: Run, verify compile failure**

```bash
dotnet build tests/Ngraphiphy.Llm.Tests/
```
Expected: error — `OpenAiConfig` and `AnthropicConfig` have no `Endpoint` parameter.

- [ ] **Step 3: Update `OpenAiConfig.cs`**

Replace the file with:

```csharp
namespace Ngraphiphy.Llm;

/// <param name="ApiKey">OpenAI API key (sk-...) or compatible provider key.</param>
/// <param name="Model">Model name, e.g. "gpt-4o".</param>
/// <param name="Endpoint">Custom base URL for OpenAI-compatible endpoints (e.g. Cloudflare AI, Azure). Null uses the default OpenAI endpoint.</param>
/// <param name="MaxTokens">Maximum tokens in the response. Null uses the provider default.</param>
public sealed record OpenAiConfig(
    string ApiKey,
    string Model = "gpt-4o",
    string? Endpoint = null,
    int? MaxTokens = null) : IAgentConfig;
```

- [ ] **Step 4: Update `AnthropicConfig.cs`**

Replace the file with:

```csharp
namespace Ngraphiphy.Llm;

/// <param name="ApiKey">Anthropic API key (sk-ant-...).</param>
/// <param name="Model">Model name, e.g. "claude-sonnet-4-6".</param>
/// <param name="MaxTokens">Maximum tokens in the response.</param>
/// <param name="Endpoint">Custom base URL for Anthropic-compatible endpoints. Null uses the default Anthropic endpoint.</param>
public sealed record AnthropicConfig(
    string ApiKey,
    string Model = "claude-sonnet-4-6",
    int MaxTokens = 4096,
    string? Endpoint = null) : IAgentConfig;
```

- [ ] **Step 5: Update `GraphAgentFactory.CreateOpenAi`**

Read `src/Ngraphiphy.Llm/GraphAgentFactory.cs`. Replace the `CreateOpenAi` private method:

```csharp
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
```

Also update `CreateAnthropic` to use `Endpoint` if set. Read the `ClientOptions` type in `Anthropic.Core` — it has a `BaseUrl` property:

```csharp
private static ChatClientAgent CreateAnthropic(AnthropicConfig config, IList<AITool> tools)
{
    var opts = new ClientOptions { ApiKey = config.ApiKey };
    if (config.Endpoint is not null)
        opts.BaseUrl = config.Endpoint;
    var client = new AnthropicClient(opts);
    return client.AsAIAgent(
        model: config.Model,
        instructions: Instructions,
        name: "GraphAnalyst",
        tools: tools,
        defaultMaxTokens: config.MaxTokens);
}
```

- [ ] **Step 6: Run tests**

```bash
dotnet run --project tests/Ngraphiphy.Llm.Tests/
```
Expected: new tests PASS, all existing tests still pass.

- [ ] **Step 7: Commit**

```bash
git add src/Ngraphiphy.Llm/OpenAiConfig.cs src/Ngraphiphy.Llm/AnthropicConfig.cs src/Ngraphiphy.Llm/GraphAgentFactory.cs tests/Ngraphiphy.Llm.Tests/AgentConfigTests.cs
git commit -m "feat(llm): add Endpoint and MaxTokens to OpenAiConfig; add Endpoint to AnthropicConfig"
```

---

## Task 2: Add `AgentProviderResolver` to `Ngraphiphy.Llm`

**Files:**
- Modify: `src/Ngraphiphy.Llm/Ngraphiphy.Llm.csproj`
- Create: `src/Ngraphiphy.Llm/AgentProviderResolver.cs`
- Create: `tests/Ngraphiphy.Llm.Tests/AgentProviderResolverTests.cs`

- [ ] **Step 1: Add configuration dependency to Llm project**

In `src/Ngraphiphy.Llm/Ngraphiphy.Llm.csproj`, add inside the existing `<ItemGroup>` with packages:

```xml
<PackageReference Include="Microsoft.Extensions.Configuration.Abstractions" Version="10.0.0" />
```

- [ ] **Step 2: Write failing tests**

Create `tests/Ngraphiphy.Llm.Tests/AgentProviderResolverTests.cs`:

```csharp
using Microsoft.Extensions.Configuration;
using Ngraphiphy.Llm;

namespace Ngraphiphy.Llm.Tests;

public class AgentProviderResolverTests
{
    private static IConfiguration BuildConfig(Dictionary<string, string?> values)
        => new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Test]
    public async Task Resolve_AnthropicProvider_ReturnsAnthropicConfig()
    {
        var config = BuildConfig(new()
        {
            ["Llm:Provider"] = "MyAnthropic",
            ["Providers:MyAnthropic:ApiType"] = "anthropic",
            ["Providers:MyAnthropic:ApiKey"] = "sk-ant-test",
            ["Providers:MyAnthropic:Model"] = "claude-sonnet-4-6",
            ["Providers:MyAnthropic:MaxTokens"] = "2048",
        });
        var resolver = new AgentProviderResolver(config);

        var result = resolver.Resolve();

        var anthropic = result as AnthropicConfig;
        await Assert.That(anthropic).IsNotNull();
        await Assert.That(anthropic!.ApiKey).IsEqualTo("sk-ant-test");
        await Assert.That(anthropic.Model).IsEqualTo("claude-sonnet-4-6");
        await Assert.That(anthropic.MaxTokens).IsEqualTo(2048);
    }

    [Test]
    public async Task Resolve_OpenAiProviderWithEndpoint_ReturnsOpenAiConfigWithEndpoint()
    {
        var config = BuildConfig(new()
        {
            ["Providers:CF:ApiType"] = "openai",
            ["Providers:CF:ApiKey"] = "token-abc",
            ["Providers:CF:Model"] = "@cf/baai/bge-base-en-v1.5",
            ["Providers:CF:Endpoint"] = "https://api.cloudflare.com/client/v4/accounts/123/ai/v1/",
        });
        var resolver = new AgentProviderResolver(config);

        var result = resolver.Resolve("CF");

        var openai = result as OpenAiConfig;
        await Assert.That(openai).IsNotNull();
        await Assert.That(openai!.Endpoint).IsEqualTo("https://api.cloudflare.com/client/v4/accounts/123/ai/v1/");
        await Assert.That(openai.ApiKey).IsEqualTo("token-abc");
    }

    [Test]
    public async Task Resolve_OllamaProvider_ReturnsOllamaConfig()
    {
        var config = BuildConfig(new()
        {
            ["Providers:Local:ApiType"] = "ollama",
            ["Providers:Local:Model"] = "llama3.2",
            ["Providers:Local:Endpoint"] = "http://localhost:11434",
        });
        var resolver = new AgentProviderResolver(config);

        var result = resolver.Resolve("Local");

        var ollama = result as OllamaConfig;
        await Assert.That(ollama).IsNotNull();
        await Assert.That(ollama!.Model).IsEqualTo("llama3.2");
    }

    [Test]
    public async Task Resolve_A2AProvider_UsesEndpointAsAgentUrl()
    {
        var config = BuildConfig(new()
        {
            ["Providers:RemoteAgent:ApiType"] = "a2a",
            ["Providers:RemoteAgent:Endpoint"] = "https://agent.example.com",
            ["Providers:RemoteAgent:ApiKey"] = "bearer-token",
        });
        var resolver = new AgentProviderResolver(config);

        var result = resolver.Resolve("RemoteAgent");

        var a2a = result as A2AConfig;
        await Assert.That(a2a).IsNotNull();
        await Assert.That(a2a!.AgentUrl).IsEqualTo("https://agent.example.com");
        await Assert.That(a2a.ApiKey).IsEqualTo("bearer-token");
    }

    [Test]
    public async Task Resolve_CopilotProvider_ReturnsCopilotConfig()
    {
        var config = BuildConfig(new()
        {
            ["Providers:GHCopilot:ApiType"] = "copilot",
        });
        var resolver = new AgentProviderResolver(config);

        var result = resolver.Resolve("GHCopilot");

        await Assert.That(result).IsTypeOf<CopilotConfig>();
    }

    [Test]
    public async Task Resolve_UnknownProvider_ThrowsInvalidOperationException()
    {
        var config = BuildConfig(new());
        var resolver = new AgentProviderResolver(config);

        var act = () => resolver.Resolve("DoesNotExist");

        await Assert.That(act).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Resolve_NoLlmProvider_ThrowsInvalidOperationException()
    {
        var config = BuildConfig(new());
        var resolver = new AgentProviderResolver(config);

        // No providerOverride, no Llm:Provider in config
        var act = () => resolver.Resolve();

        await Assert.That(act).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Resolve_UsesProviderOverride_IgnoresLlmProvider()
    {
        var config = BuildConfig(new()
        {
            ["Llm:Provider"] = "Anthropic",
            ["Providers:Anthropic:ApiType"] = "anthropic",
            ["Providers:Anthropic:ApiKey"] = "sk-ant-default",
            ["Providers:OpenAI:ApiType"] = "openai",
            ["Providers:OpenAI:ApiKey"] = "sk-openai-override",
            ["Providers:OpenAI:Model"] = "gpt-4o",
        });
        var resolver = new AgentProviderResolver(config);

        var result = resolver.Resolve("OpenAI");

        await Assert.That(result).IsTypeOf<OpenAiConfig>();
    }
}
```

- [ ] **Step 3: Run, verify compile failure**

```bash
dotnet build tests/Ngraphiphy.Llm.Tests/
```
Expected: `AgentProviderResolver` not found.

- [ ] **Step 4: Create `AgentProviderResolver.cs`**

```csharp
using Microsoft.Extensions.Configuration;
using QuikGraph;
using Ngraphiphy.Models;

namespace Ngraphiphy.Llm;

/// <summary>
/// Reads a named provider entry from IConfiguration["Providers:{name}"] and constructs
/// the appropriate IAgentConfig. The provider name defaults to IConfiguration["Llm:Provider"].
/// </summary>
public sealed class AgentProviderResolver
{
    private readonly IConfiguration _configuration;

    public AgentProviderResolver(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Resolves the named provider to an <see cref="IAgentConfig"/>.
    /// </summary>
    /// <param name="providerOverride">
    /// Explicit provider name (key in Providers section). When null, reads Llm:Provider from config.
    /// </param>
    public IAgentConfig Resolve(string? providerOverride = null)
    {
        var name = providerOverride
            ?? _configuration["Llm:Provider"]
            ?? throw new InvalidOperationException(
                "No LLM provider specified. Set Llm:Provider in appsettings.json or pass --provider.");

        var section = _configuration.GetSection($"Providers:{name}");
        if (!section.Exists())
            throw new InvalidOperationException(
                $"Provider '{name}' not found in the Providers configuration section.");

        var apiType = (section["ApiType"] ?? name).ToLowerInvariant();

        return apiType switch
        {
            "anthropic" => new AnthropicConfig(
                ApiKey: section["ApiKey"]
                    ?? throw new InvalidOperationException($"Provider '{name}': ApiKey is required."),
                Model: section["Model"] ?? "claude-sonnet-4-6",
                MaxTokens: section.GetValue<int?>("MaxTokens") ?? 4096,
                Endpoint: section["Endpoint"]),

            "openai" => new OpenAiConfig(
                ApiKey: section["ApiKey"]
                    ?? throw new InvalidOperationException($"Provider '{name}': ApiKey is required."),
                Model: section["Model"] ?? "gpt-4o",
                Endpoint: section["Endpoint"],
                MaxTokens: section.GetValue<int?>("MaxTokens")),

            "ollama" => new OllamaConfig(
                Model: section["Model"] ?? "llama3.2",
                Endpoint: section["Endpoint"] ?? "http://localhost:11434"),

            "copilot" => new CopilotConfig(),

            "a2a" => new A2AConfig(
                AgentUrl: section["Endpoint"]
                    ?? throw new InvalidOperationException($"Provider '{name}': Endpoint is required for A2A."),
                ApiKey: section["ApiKey"]),

            _ => throw new InvalidOperationException(
                $"Unknown ApiType '{apiType}' for provider '{name}'. Valid values: anthropic, openai, ollama, copilot, a2a.")
        };
    }

    /// <summary>
    /// Resolves the provider and creates a ready-to-use <see cref="IGraphAgent"/>.
    /// </summary>
    public async Task<IGraphAgent> CreateAgentAsync(
        BidirectionalGraph<Node, TaggedEdge<Node, Edge>> graph,
        string? providerOverride = null,
        CancellationToken ct = default)
    {
        var config = Resolve(providerOverride);
        return await GraphAgentFactory.CreateAsync(config, graph, ct);
    }
}
```

- [ ] **Step 5: Run tests**

```bash
dotnet run --project tests/Ngraphiphy.Llm.Tests/ -- --filter "AgentProviderResolverTests"
```
Expected: all 8 tests PASS.

- [ ] **Step 6: Run full Llm test suite**

```bash
dotnet run --project tests/Ngraphiphy.Llm.Tests/
```
Expected: all tests pass.

- [ ] **Step 7: Commit**

```bash
git add src/Ngraphiphy.Llm/ tests/Ngraphiphy.Llm.Tests/AgentProviderResolverTests.cs
git commit -m "feat(llm): add AgentProviderResolver — named provider config via IConfiguration"
```

---

## Task 3: Add `OpenAiEmbeddingProvider` and `EmbeddingProviderResolver` to `Ngraphiphy.Storage`

**Files:**
- Create: `src/Ngraphiphy.Storage/Embedding/OpenAiEmbeddingProvider.cs`
- Create: `src/Ngraphiphy.Storage/Embedding/EmbeddingProviderResolver.cs`
- Create: `tests/Ngraphiphy.Storage.Tests/EmbeddingProviderResolverTests.cs`

The `OpenAiEmbeddingProvider` is a generic replacement for `CloudflareEmbeddingProvider`. It takes `(Endpoint, ApiKey, Model, DimensionSize)` instead of `(AccountId, ApiToken, Model)`. The caller (resolver) is responsible for supplying the correct `DimensionSize`.

- [ ] **Step 1: Write failing tests**

Create `tests/Ngraphiphy.Storage.Tests/EmbeddingProviderResolverTests.cs`:

```csharp
using Microsoft.Extensions.Configuration;
using Ngraphiphy.Storage.Embedding;

namespace Ngraphiphy.Storage.Tests;

public class EmbeddingProviderResolverTests
{
    private static IConfiguration BuildConfig(Dictionary<string, string?> values)
        => new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Test]
    public async Task Resolve_OpenAiCompatibleProvider_ReturnsProviderWithCorrectDimensions()
    {
        var config = BuildConfig(new()
        {
            ["Embedding:Provider"] = "CloudflareAI",
            ["Providers:CloudflareAI:ApiType"] = "openai",
            ["Providers:CloudflareAI:Endpoint"] = "https://api.cloudflare.com/client/v4/accounts/abc/ai/v1/",
            ["Providers:CloudflareAI:ApiKey"] = "test-token",
            ["Providers:CloudflareAI:Model"] = "@cf/baai/bge-base-en-v1.5",
            ["Providers:CloudflareAI:Dimensions"] = "768",
        });
        var resolver = new EmbeddingProviderResolver(config);

        var provider = resolver.Resolve();

        await Assert.That(provider.DimensionSize).IsEqualTo(768);
    }

    [Test]
    public async Task Resolve_ProviderWithNoDimensions_DefaultsTo768()
    {
        var config = BuildConfig(new()
        {
            ["Providers:MyEmbed:ApiType"] = "openai",
            ["Providers:MyEmbed:Endpoint"] = "https://embed.example.com/v1",
            ["Providers:MyEmbed:ApiKey"] = "key",
            ["Providers:MyEmbed:Model"] = "text-embedding-3-small",
        });
        var resolver = new EmbeddingProviderResolver(config);

        var provider = resolver.Resolve("MyEmbed");

        await Assert.That(provider.DimensionSize).IsEqualTo(768);
    }

    [Test]
    public async Task Resolve_MissingEndpoint_Throws()
    {
        var config = BuildConfig(new()
        {
            ["Providers:Bad:ApiType"] = "openai",
            ["Providers:Bad:ApiKey"] = "key",
            ["Providers:Bad:Model"] = "model",
            // No Endpoint
        });
        var resolver = new EmbeddingProviderResolver(config);

        var act = () => resolver.Resolve("Bad");

        await Assert.That(act).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Resolve_MissingProvider_Throws()
    {
        var config = BuildConfig(new());
        var resolver = new EmbeddingProviderResolver(config);

        var act = () => resolver.Resolve("NotHere");

        await Assert.That(act).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Resolve_NoEmbeddingProvider_Throws()
    {
        var config = BuildConfig(new());
        var resolver = new EmbeddingProviderResolver(config);

        var act = () => resolver.Resolve();

        await Assert.That(act).Throws<InvalidOperationException>();
    }
}
```

- [ ] **Step 2: Run, verify compile failure**

```bash
dotnet build tests/Ngraphiphy.Storage.Tests/
```
Expected: `EmbeddingProviderResolver` not found.

- [ ] **Step 3: Create `OpenAiEmbeddingProvider.cs`**

```csharp
using OpenAI;
using OpenAI.Embeddings;
using System.ClientModel;

namespace Ngraphiphy.Storage.Embedding;

/// <summary>
/// Embedding provider backed by any OpenAI-compatible embedding endpoint.
/// Works with OpenAI, Cloudflare Workers AI, Azure OpenAI, etc.
/// </summary>
public sealed class OpenAiEmbeddingProvider : IEmbeddingProvider
{
    private readonly OpenAIClient _client;
    private readonly string _model;

    public int DimensionSize { get; }

    public OpenAiEmbeddingProvider(string endpoint, string apiKey, string model, int dimensionSize = 768)
    {
        _model = model;
        DimensionSize = dimensionSize;

        var credential = new ApiKeyCredential(apiKey);
        var options = new OpenAIClientOptions { Endpoint = new Uri(endpoint) };
        _client = new OpenAIClient(credential, options);
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct)
    {
        var client = _client.GetEmbeddingClient(_model);
        var response = await client.GenerateEmbeddingAsync(text, cancellationToken: ct);
        return response.Value.ToFloats().ToArray();
    }

    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct)
    {
        var tasks = texts.Select(t => EmbedAsync(t, ct)).ToList();
        return await Task.WhenAll(tasks);
    }
}
```

- [ ] **Step 4: Create `EmbeddingProviderResolver.cs`**

```csharp
using Microsoft.Extensions.Configuration;

namespace Ngraphiphy.Storage.Embedding;

/// <summary>
/// Reads a named provider entry from IConfiguration["Providers:{name}"] and constructs
/// an IEmbeddingProvider. The provider name defaults to IConfiguration["Embedding:Provider"].
/// </summary>
public sealed class EmbeddingProviderResolver
{
    private readonly IConfiguration _configuration;

    public EmbeddingProviderResolver(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Resolves the named provider to an <see cref="IEmbeddingProvider"/>.
    /// </summary>
    /// <param name="providerOverride">
    /// Explicit provider name (key in Providers section). When null, reads Embedding:Provider from config.
    /// </param>
    public IEmbeddingProvider Resolve(string? providerOverride = null)
    {
        var name = providerOverride
            ?? _configuration["Embedding:Provider"]
            ?? throw new InvalidOperationException(
                "No embedding provider specified. Set Embedding:Provider in appsettings.json or pass --embed-provider.");

        var section = _configuration.GetSection($"Providers:{name}");
        if (!section.Exists())
            throw new InvalidOperationException(
                $"Provider '{name}' not found in the Providers configuration section.");

        var apiType = (section["ApiType"] ?? name).ToLowerInvariant();
        var dimensions = section.GetValue<int?>("Dimensions") ?? 768;

        return apiType switch
        {
            "openai" => new OpenAiEmbeddingProvider(
                endpoint: section["Endpoint"]
                    ?? throw new InvalidOperationException($"Provider '{name}': Endpoint is required for OpenAI-compatible embedding."),
                apiKey: section["ApiKey"]
                    ?? throw new InvalidOperationException($"Provider '{name}': ApiKey is required."),
                model: section["Model"] ?? "text-embedding-3-small",
                dimensionSize: dimensions),

            _ => throw new InvalidOperationException(
                $"Unknown ApiType '{apiType}' for embedding provider '{name}'. Currently supported: openai.")
        };
    }
}
```

- [ ] **Step 5: Check that `Microsoft.Extensions.Configuration.Abstractions` is available in Storage**

`Ngraphiphy.Storage` references `Ngraphiphy` which references `Microsoft.Extensions.Configuration.Abstractions` transitively via the pipeline. Verify it compiles:

```bash
dotnet build src/Ngraphiphy.Storage/
```

If not found, add to `src/Ngraphiphy.Storage/Ngraphiphy.Storage.csproj`:
```xml
<PackageReference Include="Microsoft.Extensions.Configuration.Abstractions" Version="10.0.0" />
```

- [ ] **Step 6: Run tests**

```bash
dotnet run --project tests/Ngraphiphy.Storage.Tests/ -- --filter "EmbeddingProviderResolverTests"
```
Expected: all 5 tests PASS.

- [ ] **Step 7: Commit**

```bash
git add src/Ngraphiphy.Storage/Embedding/OpenAiEmbeddingProvider.cs src/Ngraphiphy.Storage/Embedding/EmbeddingProviderResolver.cs tests/Ngraphiphy.Storage.Tests/EmbeddingProviderResolverTests.cs
git commit -m "feat(storage): add OpenAiEmbeddingProvider and EmbeddingProviderResolver"
```

---

## Task 4: Delete obsolete config classes; update `CliHostExtensions`

**Files:**
- Delete: `src/Ngraphiphy.Cli/Configuration/Options/LlmOptions.cs`
- Delete: `src/Ngraphiphy.Cli/Configuration/Options/EmbeddingOptions.cs`
- Delete: `src/Ngraphiphy.Storage/Embedding/CloudflareEmbeddingConfig.cs`
- Delete: `src/Ngraphiphy.Storage/Embedding/CloudflareEmbeddingProvider.cs`
- Delete: `src/Ngraphiphy.Storage/Embedding/IEmbeddingProviderConfig.cs`
- Delete: `src/Ngraphiphy.Storage/Embedding/EmbeddingProviderFactory.cs`
- Modify: `src/Ngraphiphy.Cli/Configuration/CliHostExtensions.cs`

Note: deleting these files will cause compile errors in `QueryCommand.cs` and `PushCommand.cs`. That is expected — those are fixed in Tasks 5 and 6. Do not attempt to build the CLI project until all three tasks (4, 5, 6) are done. You can build `Ngraphiphy.Llm` and `Ngraphiphy.Storage` at any point.

- [ ] **Step 1: Delete the six files**

```bash
rm src/Ngraphiphy.Cli/Configuration/Options/LlmOptions.cs
rm src/Ngraphiphy.Cli/Configuration/Options/EmbeddingOptions.cs
rm src/Ngraphiphy.Storage/Embedding/CloudflareEmbeddingConfig.cs
rm src/Ngraphiphy.Storage/Embedding/CloudflareEmbeddingProvider.cs
rm src/Ngraphiphy.Storage/Embedding/IEmbeddingProviderConfig.cs
rm src/Ngraphiphy.Storage/Embedding/EmbeddingProviderFactory.cs
```

- [ ] **Step 2: Build Storage to confirm no broken references**

```bash
dotnet build src/Ngraphiphy.Storage/
```
Expected: success (no remaining references to the deleted classes inside Storage).

- [ ] **Step 3: Update `CliHostExtensions.cs`**

Read `src/Ngraphiphy.Cli/Configuration/CliHostExtensions.cs`. Replace the entire file with:

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ngraphiphy.Cli.Configuration.Options;
using Ngraphiphy.Cli.Configuration.Secrets;
using Ngraphiphy.Llm;
using Ngraphiphy.Storage.Embedding;
using Spectre.Console;

namespace Ngraphiphy.Cli.Configuration;

public static class CliHostExtensions
{
    public static HostApplicationBuilder AddCliConfiguration(this HostApplicationBuilder builder)
    {
        // 1. JSON sources (both optional)
        var binaryDir = AppContext.BaseDirectory;
        var userConfigDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".ngraphiphy");

        builder.Configuration
            .AddJsonFile(Path.Combine(binaryDir, "appsettings.json"),
                optional: true, reloadOnChange: false)
            .AddJsonFile(Path.Combine(userConfigDir, "appsettings.json"),
                optional: true, reloadOnChange: false)
            .AddEnvironmentVariables(prefix: "NGRAPHIPHY_");

        // 2. Secret overlay
        var passProvider = new PassSecretProvider();
        var envProvider = new EnvSecretProvider();
        var providers = new Dictionary<string, ISecretProvider>(StringComparer.Ordinal)
        {
            ["pass"] = passProvider,
            ["env"] = envProvider,
        };

        var snapshot = ((IConfigurationBuilder)builder.Configuration).Build();
        SecretResolver.ResolveAndOverlayAsync(
            builder.Configuration, snapshot, providers,
            warn: msg => AnsiConsole.MarkupLineInterpolated($"[yellow]Warning: {msg}[/]"))
            .GetAwaiter().GetResult();

        // 3. Register secret providers (keyed + default)
        builder.Services.AddKeyedSingleton<ISecretProvider>("pass", passProvider);
        builder.Services.AddKeyedSingleton<ISecretProvider>("env", envProvider);
        builder.Services.AddSingleton<ISecretProvider>(passProvider);

        // 4. Register provider resolvers — both take IConfiguration directly
        builder.Services.AddSingleton<AgentProviderResolver>();
        builder.Services.AddSingleton<EmbeddingProviderResolver>();

        // 5. Bind graph database options (unchanged)
        builder.Services.Configure<GraphDatabaseOptions>(builder.Configuration.GetSection("GraphDatabase"));

        return builder;
    }
}
```

- [ ] **Step 4: Build Storage and Llm projects**

```bash
dotnet build src/Ngraphiphy.Storage/ && dotnet build src/Ngraphiphy.Llm/
```
Expected: both succeed.

- [ ] **Step 5: Do not commit yet — proceed to Task 5 and 6 to fix the CLI commands before committing.**

---

## Task 5: Refactor `QueryCommand`

**Files:**
- Modify: `src/Ngraphiphy.Cli/Commands/QueryCommand.cs`

- [ ] **Step 1: Replace `QueryCommand.cs`**

```csharp
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
    [Description("Named LLM provider from the Providers config section. Defaults to Llm:Provider.")]
    public string? Provider { get; init; }

    [CommandOption("--cache <dir>")]
    [Description("Cache directory. Default: <path>/.ngraphiphy-cache")]
    public string? CacheDir { get; init; }
}

public sealed class QueryCommand : AsyncCommand<QuerySettings>
{
    private readonly AgentProviderResolver _agentResolver;

    public QueryCommand(AgentProviderResolver agentResolver)
    {
        _agentResolver = agentResolver;
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context, QuerySettings settings, CancellationToken cancellationToken)
    {
        RepositoryAnalysis? repo = null;
        try
        {
            await AnsiConsole.Status().Spinner(Spinner.Known.Dots)
                .StartAsync("Analyzing repository...", async ctx =>
                {
                    repo = await RepositoryAnalysis.RunAsync(
                        settings.Path, cacheDir: settings.CacheDir,
                        onProgress: msg => ctx.Status(msg), ct: cancellationToken);
                });
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Error: {ex.Message}[/]");
            return 1;
        }

        if (repo is null) return 1;

        IGraphAgent agent;
        try
        {
            agent = await _agentResolver.CreateAgentAsync(repo.Graph, settings.Provider, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]{ex.Message}[/]");
            return 1;
        }

        await using (agent)
        await using var session = await agent.CreateSessionAsync(cancellationToken);

        AnsiConsole.MarkupLine("[bold]Querying LLM...[/]");
        string answer;
        try
        {
            answer = await agent.AnswerAsync(settings.Question, session, cancellationToken);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]LLM error: {ex.Message}[/]");
            return 1;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Panel(answer).Header("Answer").Expand());
        return 0;
    }
}
```

- [ ] **Step 2: Build CLI**

```bash
dotnet build src/Ngraphiphy.Cli/
```
Expected: QueryCommand compiles. PushCommand still broken (fixed in Task 6).

---

## Task 6: Refactor `PushCommand`; commit Tasks 4–6 together

**Files:**
- Modify: `src/Ngraphiphy.Cli/Commands/PushCommand.cs`

- [ ] **Step 1: Replace `PushCommand.cs`**

```csharp
using System.ComponentModel;
using Microsoft.Extensions.Options;
using Ngraphiphy.Cli.Configuration.Options;
using Ngraphiphy.Llm;
using Ngraphiphy.Pipeline;
using Ngraphiphy.Storage;
using Ngraphiphy.Storage.Embedding;
using Ngraphiphy.Storage.Models;
using Ngraphiphy.Storage.Providers.Memgraph;
using Ngraphiphy.Storage.Providers.Neo4j;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Ngraphiphy.Cli.Commands;

public sealed class PushSettings : CommandSettings
{
    [CommandArgument(0, "<path>")]
    [Description("Repository root directory.")]
    public required string Path { get; init; }

    [CommandOption("--backend <backend>")]
    [Description("Graph database: neo4j or memgraph")]
    public string? Backend { get; init; }

    [CommandOption("--uri <uri>")]
    [Description("Neo4j URI. Default: bolt://localhost:7687")]
    public string? Uri { get; init; }

    [CommandOption("--host <host>")]
    [Description("Memgraph host. Default: localhost")]
    public string? Host { get; init; }

    [CommandOption("--port <port>")]
    [Description("Memgraph port. Default: 7687")]
    public int? Port { get; init; }

    [CommandOption("--username <username>")]
    [Description("Database username.")]
    public string? Username { get; init; }

    [CommandOption("--password <password>")]
    [Description("Database password.")]
    public string? Password { get; init; }

    [CommandOption("--embed")]
    [Description("Embed nodes after push using the configured embedding provider.")]
    public bool Embed { get; init; }

    [CommandOption("--embed-provider <name>")]
    [Description("Named embedding provider from the Providers config section. Defaults to Embedding:Provider.")]
    public string? EmbedProvider { get; init; }

    [CommandOption("--summarize")]
    [Description("Generate community summaries using the configured LLM provider.")]
    public bool Summarize { get; init; }

    [CommandOption("--provider <name>")]
    [Description("Named LLM provider for --summarize. Defaults to Llm:Provider.")]
    public string? LlmProvider { get; init; }

    [CommandOption("--force")]
    [Description("Re-push even if snapshot exists.")]
    public bool Force { get; init; }

    [CommandOption("--cache <dir>")]
    [Description("Cache directory. Default: <path>/.ngraphiphy-cache")]
    public string? CacheDir { get; init; }
}

public sealed class PushCommand : AsyncCommand<PushSettings>
{
    private readonly GraphDatabaseOptions _dbOpts;
    private readonly AgentProviderResolver _agentResolver;
    private readonly EmbeddingProviderResolver _embedResolver;

    public PushCommand(
        IOptions<GraphDatabaseOptions> dbOptions,
        AgentProviderResolver agentResolver,
        EmbeddingProviderResolver embedResolver)
    {
        _dbOpts = dbOptions.Value;
        _agentResolver = agentResolver;
        _embedResolver = embedResolver;
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context, PushSettings settings, CancellationToken cancellationToken)
    {
        // 1. Resolve snapshot ID
        Console.Error.WriteLine("[ngraphiphy] Resolving snapshot ID...");
        var snapshotId = SnapshotId.Resolve(settings.Path);
        Console.Error.WriteLine($"[ngraphiphy] Snapshot: {snapshotId.Id}");

        // 2. Create graph store config
        var backend = (settings.Backend ?? _dbOpts.Backend).ToLowerInvariant();

        IGraphStoreConfig storeConfig = backend switch
        {
            "neo4j" => new Neo4jConfig(
                Uri: settings.Uri ?? _dbOpts.Neo4j.Uri,
                Username: settings.Username ?? _dbOpts.Neo4j.Username,
                Password: settings.Password ?? _dbOpts.Neo4j.Password),

            "memgraph" => new MemgraphConfig(
                Host: settings.Host ?? _dbOpts.Memgraph.Host,
                Port: settings.Port ?? _dbOpts.Memgraph.Port,
                Username: settings.Username ?? _dbOpts.Memgraph.Username,
                Password: settings.Password ?? _dbOpts.Memgraph.Password),

            _ => throw new InvalidOperationException($"Unknown backend: {backend}")
        };

        // 3. Create graph store
        await using var store = await GraphStoreFactory.CreateAsync(storeConfig, ct: cancellationToken);

        // 4. Check snapshot
        var exists = await store.SnapshotExistsAsync(snapshotId, cancellationToken);
        if (exists && !settings.Force)
        {
            AnsiConsole.MarkupLine("[yellow][ngraphiphy] Snapshot already exists, skipping.[/]");
            return 0;
        }

        // 5. Run analysis
        RepositoryAnalysis? analysis = null;
        await AnsiConsole.Status().Spinner(Spinner.Known.Dots)
            .StartAsync("Analyzing repository...", async ctx =>
            {
                analysis = await RepositoryAnalysis.RunAsync(
                    settings.Path, cacheDir: settings.CacheDir,
                    onProgress: msg => ctx.Status(msg), ct: cancellationToken);
            });

        if (analysis is null) return 1;

        // 6. Save snapshot
        AnsiConsole.MarkupLine("[blue][ngraphiphy] Saving snapshot...[/]");
        await store.SaveSnapshotAsync(analysis, snapshotId, cancellationToken);
        AnsiConsole.MarkupLineInterpolated(
            $"[green][ngraphiphy] Snapshot saved: {analysis.Graph.VertexCount} nodes, {analysis.Graph.EdgeCount} edges[/]");

        // 7. Embed nodes (optional)
        if (settings.Embed)
        {
            IEmbeddingProvider embedder;
            try
            {
                embedder = _embedResolver.Resolve(settings.EmbedProvider);
            }
            catch (InvalidOperationException ex)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]Embedding config error: {ex.Message}[/]");
                return 1;
            }

            AnsiConsole.MarkupLine("[blue][ngraphiphy] Embedding nodes...[/]");
            await AnsiConsole.Status().Spinner(Spinner.Known.Dots)
                .StartAsync("Embedding...", async ctx =>
                {
                    await store.EmbedNodesAsync(snapshotId, embedder, cancellationToken);
                });
            AnsiConsole.MarkupLine("[green][ngraphiphy] Nodes embedded[/]");
        }

        // 8. Community summaries (optional)
        if (settings.Summarize)
        {
            IGraphAgent agent;
            try
            {
                agent = await _agentResolver.CreateAgentAsync(
                    analysis.Graph, settings.LlmProvider, cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                AnsiConsole.MarkupLineInterpolated($"[red]LLM config error: {ex.Message}[/]");
                return 1;
            }

            AnsiConsole.MarkupLine("[blue][ngraphiphy] Generating community summaries...[/]");
            await using (agent)
            await using var session = await agent.CreateSessionAsync(cancellationToken);

            var summaries = new List<CommunitySummary>();
            var communities = analysis.Graph.Vertices
                .Where(n => n.Community.HasValue)
                .GroupBy(n => n.Community!.Value)
                .ToList();

            foreach (var community in communities)
            {
                var nodes = string.Join(", ", community.Select(n => n.Label).Take(5));
                var prompt = $"Summarize this software module in 1-2 sentences: {nodes}";
                try
                {
                    var summary = await agent.AnswerAsync(prompt, session, cancellationToken);
                    summaries.Add(new(community.Key, summary, community.Count()));
                    AnsiConsole.MarkupLineInterpolated($"[dim]  Community {community.Key}: summarized[/]");
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLineInterpolated(
                        $"[yellow]  Community {community.Key}: summary failed ({ex.Message})[/]");
                }
            }

            await store.SaveCommunitySummariesAsync(snapshotId, summaries, cancellationToken);
            AnsiConsole.MarkupLineInterpolated(
                $"[green][ngraphiphy] Saved {summaries.Count} community summaries[/]");
        }

        AnsiConsole.MarkupLine("[green][ngraphiphy] Push complete[/]");
        return 0;
    }
}
```

- [ ] **Step 2: Build entire solution**

```bash
dotnet build
```
Expected: 0 errors.

- [ ] **Step 3: Run all tests**

```bash
dotnet run --project tests/Ngraphiphy.Cli.Tests/
dotnet run --project tests/Ngraphiphy.Storage.Tests/
dotnet run --project tests/Ngraphiphy.Llm.Tests/
```
Expected: all pass (some storage tests skip due to requiring live Neo4j/Memgraph).

- [ ] **Step 4: Commit Tasks 4–6 together**

```bash
git add src/Ngraphiphy.Cli/ src/Ngraphiphy.Storage/Embedding/
git commit -m "refactor(cli): replace LlmOptions/EmbeddingOptions with AgentProviderResolver/EmbeddingProviderResolver"
```

---

## Task 7: Restructure `appsettings.json`; update `docs/`

**Files:**
- Modify: `src/Ngraphiphy.Cli/appsettings.json`
- Modify: `docs/Usage.md` (update config examples)

- [ ] **Step 1: Replace `appsettings.json`**

```json
{
  "Providers": {
    "Anthropic": {
      "ApiType": "anthropic",
      "ApiKey": "pass://anthropic/api-key",
      "Model": "claude-sonnet-4-6",
      "MaxTokens": 4096
    },
    "OpenAI": {
      "ApiType": "openai",
      "ApiKey": "pass://openai/api-key",
      "Model": "gpt-4o"
    },
    "Ollama": {
      "ApiType": "ollama",
      "Endpoint": "http://localhost:11434",
      "Model": "llama3.2"
    },
    "GitHubCopilot": {
      "ApiType": "copilot"
    },
    "A2A": {
      "ApiType": "a2a",
      "Endpoint": "env://A2A_AGENT_URL",
      "ApiKey": ""
    },
    "CloudflareAI": {
      "ApiType": "openai",
      "Endpoint": "https://api.cloudflare.com/client/v4/accounts/{account_id}/ai/v1/",
      "ApiKey": "pass://cloudflare/api-token",
      "Model": "@cf/baai/bge-base-en-v1.5",
      "Dimensions": 768
    }
  },
  "Llm": {
    "Provider": "Anthropic"
  },
  "Embedding": {
    "Provider": "CloudflareAI"
  },
  "GraphDatabase": {
    "Backend": "neo4j",
    "Neo4j": {
      "Uri": "bolt://localhost:7687",
      "Username": "neo4j",
      "Password": "pass://databases/neo4j/password"
    },
    "Memgraph": {
      "Host": "localhost",
      "Port": 7688
    }
  }
}
```

Note: Memgraph Port changed to 7688 to match the `.docker/memgraph.docker-compose.yml` mapping.

Note: `{account_id}` in the Cloudflare endpoint is a placeholder — users replace it with their actual account ID. It is NOT resolved by the secret provider.

- [ ] **Step 2: Update `docs/Usage.md`**

Find the section in `docs/Usage.md` that describes API key / LLM provider configuration. Replace with a section that explains the `Providers` dict pattern:
- Show the `appsettings.json` `Providers` block structure
- Show the `Llm.Provider` and `Embedding.Provider` pointer fields
- Show how to add a custom OpenAI-compatible endpoint (e.g. Groq, Azure)
- Show that `--provider <name>` in `query`/`push` now refers to a named `Providers` key
- Remove any references to `--key`, `--model`, `--agent-url`, `--cf-account`, `--cf-token`, `--cf-model`

- [ ] **Step 3: Build and smoke-test**

```bash
dotnet build
```
Expected: 0 errors.

- [ ] **Step 4: Run all tests**

```bash
dotnet run --project tests/Ngraphiphy.Cli.Tests/
dotnet run --project tests/Ngraphiphy.Storage.Tests/
dotnet run --project tests/Ngraphiphy.Llm.Tests/
dotnet run --project tests/Ngraphiphy.Tests/
```
Expected: all pass (Leiden native failures are pre-existing and unrelated).

- [ ] **Step 5: Commit**

```bash
git add src/Ngraphiphy.Cli/appsettings.json docs/Usage.md
git commit -m "feat: restructure appsettings.json to Providers dictionary with Llm/Embedding provider pointers"
```

---

## Self-review

**Spec coverage:**

| Requirement | Task |
|---|---|
| `appsettings.json` uses named `Providers` dict | T7 |
| `Llm.Provider` and `Embedding.Provider` pointers | T7 |
| `AgentProviderResolver` reads `IConfiguration` dynamically | T2 |
| `EmbeddingProviderResolver` reads `IConfiguration` dynamically | T3 |
| Cloudflare works as `ApiType: "openai"` with custom `Endpoint` | T3 + T7 |
| Duplicate switch logic removed from `QueryCommand` | T5 |
| Duplicate switch logic removed from `PushCommand` | T6 |
| `LlmOptions` / `EmbeddingOptions` / Cloudflare-specific classes deleted | T4 |
| `OpenAiConfig` and `AnthropicConfig` support custom `Endpoint` | T1 |
| `--cf-account` / `--cf-token` / `--cf-model` removed; `--embed-provider` added | T6 |
| `--key` / `--model` / `--agent-url` removed from `QueryCommand` | T5 |
| Tests for both resolvers | T2 + T3 |

**Placeholder scan:** None found.

**Type consistency check:**
- `AgentProviderResolver.Resolve(string?)` → `IAgentConfig` used in T2 and referenced in T5/T6 ✓
- `EmbeddingProviderResolver.Resolve(string?)` → `IEmbeddingProvider` used in T3 and referenced in T6 ✓
- `AgentProviderResolver.CreateAgentAsync(graph, string?, CancellationToken)` used in T2 and T5/T6 ✓
- `OpenAiEmbeddingProvider(string endpoint, string apiKey, string model, int dimensionSize)` matches constructor in T3 and resolver in T3 ✓
- `OpenAiConfig` 4-param constructor `(ApiKey, Model, Endpoint?, MaxTokens?)` matches T1 and T2 resolver ✓
- `AnthropicConfig` 4-param constructor `(ApiKey, Model, MaxTokens, Endpoint?)` matches T1 and T2 resolver ✓
