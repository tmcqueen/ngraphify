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
        {
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
        }
        return 0;
    }
}
