using System.ComponentModel;
using Graphiphy.Cli.Mcp;
using Graphiphy.Pipeline;
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
    protected override async Task<int> ExecuteAsync(
        CommandContext context, ServeSettings settings, CancellationToken cancellationToken)
    {
        // stdout is the MCP channel — all diagnostics go to stderr
        Console.Error.WriteLine($"[graphiphy] Analyzing {settings.Path}...");
        RepositoryAnalysis analysis;
        try
        {
            analysis = await RepositoryAnalysis.RunAsync(
                settings.Path, cacheDir: settings.CacheDir,
                onProgress: msg => Console.Error.WriteLine($"[graphiphy] {msg}"),
                ct: cancellationToken);
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("[graphiphy] Cancelled.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[graphiphy] Analysis failed: {ex.Message}");
            return 1;
        }

        Console.Error.WriteLine(
            $"[graphiphy] Ready — {analysis.Graph.VertexCount} nodes, {analysis.Graph.EdgeCount} edges.");
        Console.Error.WriteLine("[graphiphy] Starting MCP server on stdio...");
        await GraphMcpServer.RunAsync(analysis, cancellationToken);
        return 0;
    }
}
