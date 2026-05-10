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
