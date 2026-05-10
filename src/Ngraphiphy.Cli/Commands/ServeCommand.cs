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
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("[ngraphiphy] Cancelled.");
            return 0;
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
