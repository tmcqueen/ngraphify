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
    [Description("Cache directory. Default: <path>/.graphiphy-cache")]
    public string? CacheDir { get; init; }
}

public sealed class ReportCommand : AsyncCommand<ReportSettings>
{
    protected override async Task<int> ExecuteAsync(
        CommandContext context, ReportSettings settings, CancellationToken cancellationToken)
    {
        RepositoryAnalysis? result = null;
        try
        {
            await AnsiConsole.Status().Spinner(Spinner.Known.Dots)
                .StartAsync("Analyzing...", async ctx =>
                {
                    result = await RepositoryAnalysis.RunAsync(
                        settings.Path, cacheDir: settings.CacheDir,
                        onProgress: msg => ctx.Status(msg), ct: cancellationToken);
                });
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLineInterpolated($"[red]Error: {ex.Message}[/]");
            return 1;
        }

        if (result is null) return 1;

        if (settings.OutputFile is not null)
        {
            await File.WriteAllTextAsync(settings.OutputFile, result.Report, cancellationToken);
            AnsiConsole.MarkupLineInterpolated($"[green]Report written to {settings.OutputFile}[/]");
        }
        else
        {
            Console.Write(result.Report);
        }

        return 0;
    }
}
