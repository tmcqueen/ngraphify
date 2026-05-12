using System.ComponentModel;
using Graphiphy.Analysis;
using Graphiphy.Cli.Configuration.Options;
using Graphiphy.Pipeline;
using Graphiphy.Validation;
using Microsoft.Extensions.Options;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Graphiphy.Cli.Commands;

public sealed class AnalyzeSettings : CommandSettings
{
    [CommandArgument(0, "<path>")]
    [Description("Path to the repository root directory.")]
    public required string Path { get; init; }

    [CommandOption("--out|-o <file>")]
    [Description("Write the Markdown report to this file.")]
    public string? OutputFile { get; init; }

    [CommandOption("--cache <dir>")]
    [Description("Cache directory. Default: <path>/.graphiphy-cache")]
    public string? CacheDir { get; init; }
}

public sealed class AnalyzeCommand : AsyncCommand<AnalyzeSettings>
{
    private readonly AnalysisOptions _analysisOpts;

    public AnalyzeCommand(IOptions<AnalysisOptions> analysisOptions)
    {
        // _analysisOpts = analysisOptions.Value;
        _analysisOpts = new AnalysisOptions
        {
            MalformedEdgeBehavior = MalformedEdgeBehavior.Warn
        };
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context, AnalyzeSettings settings, CancellationToken cancellationToken)
    {
        RepositoryAnalysis? result = null;
        await AnsiConsole.Status().Spinner(Spinner.Known.Dots)
            .StartAsync("Analyzing repository...", async ctx =>
            {
                result = await RepositoryAnalysis.RunAsync(
                    settings.Path, cacheDir: settings.CacheDir,
                    onProgress: msg => ctx.Status(msg),
                    malformedEdgeBehavior: _analysisOpts.MalformedEdgeBehavior,
                    ct: cancellationToken);
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
            AnsiConsole.MarkupLineInterpolated($"[green]Report written to {settings.OutputFile}[/]");
        }

        return 0;
    }
}
