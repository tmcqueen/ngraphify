// src/Ngraphiphy.Cli/Commands/QueryCommand.cs
using System.ComponentModel;
using Microsoft.Extensions.Options;
using Ngraphiphy.Cli.Configuration.Options;
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
    [Description("Cache directory. Default: <path>/.ngraphiphy-cache")]
    public string? CacheDir { get; init; }
}

public sealed class QueryCommand : AsyncCommand<QuerySettings>
{
    private readonly LlmOptions _llmOpts;

    public QueryCommand(IOptions<LlmOptions> llmOptions)
    {
        _llmOpts = llmOptions.Value;
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context, QuerySettings settings, CancellationToken cancellationToken)
    {
        IAgentConfig config = settings.Provider.ToLowerInvariant() switch
        {
            "openai" => new OpenAiConfig(
                ApiKey: settings.ApiKey
                    ?? _llmOpts.OpenAi.ApiKey
                    ?? Error("--key, Llm:OpenAi:ApiKey in appsettings.json required"),
                Model: settings.Model ?? _llmOpts.OpenAi.Model),

            "ollama" => new OllamaConfig(
                Model: settings.Model ?? _llmOpts.Ollama.Model,
                Endpoint: _llmOpts.Ollama.Endpoint),

            "copilot" => new CopilotConfig(),

            "a2a" => new A2AConfig(
                AgentUrl: settings.AgentUrl
                    ?? _llmOpts.A2A.AgentUrl
                    ?? Error("--agent-url, Llm:A2A:AgentUrl in appsettings.json required"),
                ApiKey: settings.ApiKey ?? _llmOpts.A2A.ApiKey),

            _ => new AnthropicConfig(
                ApiKey: settings.ApiKey
                    ?? _llmOpts.Anthropic.ApiKey
                    ?? Error("--key, Llm:Anthropic:ApiKey in appsettings.json required"),
                Model: settings.Model ?? _llmOpts.Anthropic.Model,
                MaxTokens: _llmOpts.Anthropic.MaxTokens),
        };

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

        await using var agent = await GraphAgentFactory.CreateAsync(config, repo.Graph, cancellationToken);
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

    private static string Error(string message)
    {
        AnsiConsole.MarkupLineInterpolated($"[red]{message}[/]");
        throw new InvalidOperationException(message);
    }
}
