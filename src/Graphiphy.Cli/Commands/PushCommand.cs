using System.ComponentModel;
using Microsoft.Extensions.Options;
using Graphiphy.Cli.Configuration.Options;
using Graphiphy.Llm;
using Graphiphy.Pipeline;
using Graphiphy.Storage;
using Graphiphy.Storage.Embedding;
using Graphiphy.Storage.Models;
using Graphiphy.Storage.Providers.Memgraph;
using Graphiphy.Storage.Providers.Neo4j;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Graphiphy.Cli.Commands;

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
    [Description("Memgraph port. Default: 7688")]
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
    [Description("Cache directory. Default: <path>/.graphiphy-cache")]
    public string? CacheDir { get; init; }
}

public sealed class PushCommand : AsyncCommand<PushSettings>
{
    private readonly GraphDatabaseOptions _dbOpts;
    private readonly AnalysisOptions _analysisOpts;
    private readonly AgentProviderResolver _agentResolver;
    private readonly EmbeddingProviderResolver _embedResolver;

    public PushCommand(
        IOptions<GraphDatabaseOptions> dbOptions,
        IOptions<AnalysisOptions> analysisOptions,
        AgentProviderResolver agentResolver,
        EmbeddingProviderResolver embedResolver)
    {
        _dbOpts = dbOptions.Value;
        _analysisOpts = analysisOptions.Value;
        _agentResolver = agentResolver;
        _embedResolver = embedResolver;
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context, PushSettings settings, CancellationToken cancellationToken)
    {
        // 1. Resolve snapshot ID
        AnsiConsole.MarkupLine("[dim][graphiphy] Resolving snapshot ID...[/]");
        var snapshotId = SnapshotId.Resolve(settings.Path);
        AnsiConsole.MarkupLineInterpolated($"[dim][graphiphy] Snapshot: {snapshotId.Id}[/]");

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
            AnsiConsole.MarkupLine("[yellow][graphiphy] Snapshot already exists, skipping.[/]");
            return 0;
        }

        // 5. Run analysis
        RepositoryAnalysis? analysis = null;
        try
        {
            await AnsiConsole.Status().Spinner(Spinner.Known.Dots)
                .StartAsync("Analyzing repository...", async ctx =>
                {
                    analysis = await RepositoryAnalysis.RunAsync(
                        settings.Path, cacheDir: settings.CacheDir,
                        onProgress: msg => ctx.Status(msg),
                        malformedEdgeBehavior: _analysisOpts.MalformedEdgeBehavior,
                        ct: cancellationToken);
                });
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Error: {ex.Message}[/]");
            return 1;
        }

        if (analysis is null) return 1;

        // 6. Save snapshot
        AnsiConsole.MarkupLine("[blue][graphiphy] Saving snapshot...[/]");
        await store.SaveSnapshotAsync(analysis, snapshotId, cancellationToken);
        AnsiConsole.MarkupLineInterpolated(
            $"[green][graphiphy] Snapshot saved: {analysis.Graph.VertexCount} nodes, {analysis.Graph.EdgeCount} edges[/]");

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

            AnsiConsole.MarkupLine("[blue][graphiphy] Embedding nodes...[/]");
            await AnsiConsole.Status().Spinner(Spinner.Known.Dots)
                .StartAsync("Embedding...", async ctx =>
                {
                    await store.EmbedNodesAsync(snapshotId, embedder, cancellationToken);
                });
            AnsiConsole.MarkupLine("[green][graphiphy] Nodes embedded[/]");
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

            AnsiConsole.MarkupLine("[blue][graphiphy] Generating community summaries...[/]");
            await using (agent)
            {
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
                    $"[green][graphiphy] Saved {summaries.Count} community summaries[/]");
            }
        }

        AnsiConsole.MarkupLine("[green][graphiphy] Push complete[/]");
        return 0;
    }
}
