using System.ComponentModel;
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
    public required string Backend { get; init; }

    [CommandOption("--uri <uri>")]
    [Description("Neo4j URI. Default: bolt://localhost:7687")]
    public string? Uri { get; init; }

    [CommandOption("--host <host>")]
    [Description("Memgraph host. Default: localhost")]
    public string Host { get; init; } = "localhost";

    [CommandOption("--port <port>")]
    [Description("Memgraph port. Default: 7687")]
    public int Port { get; init; } = 7687;

    [CommandOption("--username <username>")]
    [Description("Database username.")]
    public string? Username { get; init; }

    [CommandOption("--password <password>")]
    [Description("Database password.")]
    public string? Password { get; init; }

    [CommandOption("--embed")]
    [Description("Embed nodes after push.")]
    public bool Embed { get; init; }

    [CommandOption("--cf-account <id>")]
    [Description("Cloudflare account ID (for --embed).")]
    public string? CloudflareAccount { get; init; }

    [CommandOption("--cf-token <token>")]
    [Description("Cloudflare API token (or CF_API_TOKEN env).")]
    public string? CloudflareToken { get; init; }

    [CommandOption("--cf-model <model>")]
    [Description("Cloudflare embedding model. Default: @cf/baai/bge-base-en-v1.5")]
    public string CloudflareModel { get; init; } = "@cf/baai/bge-base-en-v1.5";

    [CommandOption("--summarize")]
    [Description("Generate community summaries.")]
    public bool Summarize { get; init; }

    [CommandOption("--provider <provider>")]
    [Description("LLM provider for summaries: anthropic (default), openai, ollama, copilot, a2a")]
    public string LlmProvider { get; init; } = "anthropic";

    [CommandOption("--key <key>")]
    [Description("LLM API key (or env var).")]
    public string? ApiKey { get; init; }

    [CommandOption("--model <model>")]
    [Description("LLM model name.")]
    public string? Model { get; init; }

    [CommandOption("--force")]
    [Description("Re-push even if snapshot exists.")]
    public bool Force { get; init; }

    [CommandOption("--cache <dir>")]
    [Description("Cache directory. Default: <path>/.ngraphiphy-cache")]
    public string? CacheDir { get; init; }
}

public sealed class PushCommand : AsyncCommand<PushSettings>
{
    protected override async Task<int> ExecuteAsync(
        CommandContext context, PushSettings settings, CancellationToken cancellationToken)
    {
        // 1. Resolve snapshot ID
        Console.Error.WriteLine("[ngraphiphy] Resolving snapshot ID...");
        var snapshotId = SnapshotId.Resolve(settings.Path);
        Console.Error.WriteLine($"[ngraphiphy] Snapshot: {snapshotId.Id}");

        // 2. Create graph store config based on backend
        IGraphStoreConfig storeConfig = settings.Backend.ToLowerInvariant() switch
        {
            "neo4j" => new Neo4jConfig(
                settings.Uri ?? "bolt://localhost:7687",
                settings.Username ?? "neo4j",
                settings.Password ?? ""),
            "memgraph" => new MemgraphConfig(
                settings.Host,
                settings.Port,
                settings.Username ?? "",
                settings.Password ?? ""),
            _ => throw new InvalidOperationException($"Unknown backend: {settings.Backend}")
        };

        // 3. Create graph store
        await using var store = await GraphStoreFactory.CreateAsync(storeConfig, ct: cancellationToken);

        // 4. Check if snapshot already exists
        var exists = await store.SnapshotExistsAsync(snapshotId, cancellationToken);
        if (exists && !settings.Force)
        {
            AnsiConsole.MarkupLine($"[yellow][ngraphiphy] Snapshot already exists, skipping.[/]");
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

        if (analysis is null)
            return 1;

        // 6. Save snapshot
        AnsiConsole.MarkupLine("[blue][ngraphiphy] Saving snapshot...[/]");
        await store.SaveSnapshotAsync(analysis, snapshotId, cancellationToken);
        AnsiConsole.MarkupLine($"[green][ngraphiphy] Snapshot saved: {analysis.Graph.VertexCount} nodes, {analysis.Graph.EdgeCount} edges[/]");

        // 7. Embed nodes (optional)
        if (settings.Embed)
        {
            var cfToken = settings.CloudflareToken ?? Environment.GetEnvironmentVariable("CF_API_TOKEN");
            if (string.IsNullOrEmpty(settings.CloudflareAccount) || string.IsNullOrEmpty(cfToken))
            {
                AnsiConsole.MarkupLine("[red]Error: --cf-account and --cf-token (or CF_API_TOKEN) required for --embed[/]");
                return 1;
            }

            var embedConfig = new CloudflareEmbeddingConfig(settings.CloudflareAccount, cfToken, settings.CloudflareModel);
            var embedder = new CloudflareEmbeddingProvider(embedConfig);

            AnsiConsole.MarkupLine("[blue][ngraphiphy] Embedding nodes...[/]");
            await AnsiConsole.Status().Spinner(Spinner.Known.Dots)
                .StartAsync("Embedding...", async ctx =>
                {
                    await store.EmbedNodesAsync(snapshotId, embedder, cancellationToken);
                });
            AnsiConsole.MarkupLine("[green][ngraphiphy] Nodes embedded[/]");
        }

        // 8. Generate community summaries (optional)
        if (settings.Summarize)
        {
            IAgentConfig llmConfig = settings.LlmProvider.ToLowerInvariant() switch
            {
                "openai" => new OpenAiConfig(
                    ApiKey: settings.ApiKey ?? Env("OPENAI_API_KEY") ?? Error("--key or OPENAI_API_KEY required"),
                    Model: settings.Model ?? "gpt-4o"),
                "ollama" => new OllamaConfig(Model: settings.Model ?? "llama3.2"),
                "copilot" => new CopilotConfig(),
                "a2a" => new A2AConfig(
                    AgentUrl: Env("A2A_AGENT_URL") ?? Error("A2A_AGENT_URL required"),
                    ApiKey: settings.ApiKey),
                _ => new AnthropicConfig(
                    ApiKey: settings.ApiKey ?? Env("ANTHROPIC_API_KEY") ?? Error("--key or ANTHROPIC_API_KEY required"),
                    Model: settings.Model ?? "claude-sonnet-4-6"),
            };

            AnsiConsole.MarkupLine("[blue][ngraphiphy] Generating community summaries...[/]");
            await using var agent = await GraphAgentFactory.CreateAsync(llmConfig, analysis.Graph, cancellationToken);
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
                    AnsiConsole.MarkupLine($"[dim]  Community {community.Key}: summarized[/]");
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[yellow]  Community {community.Key}: summary failed ({ex.Message})[/]");
                }
            }

            await store.SaveCommunitySummariesAsync(snapshotId, summaries, cancellationToken);
            AnsiConsole.MarkupLine($"[green][ngraphiphy] Saved {summaries.Count} community summaries[/]");
        }

        AnsiConsole.MarkupLine("[green][ngraphiphy] Push complete[/]");
        return 0;
    }

    private static string? Env(string name) => Environment.GetEnvironmentVariable(name);

    private static string Error(string message)
    {
        AnsiConsole.MarkupLine($"[red]{message}[/]");
        throw new InvalidOperationException(message);
    }
}
