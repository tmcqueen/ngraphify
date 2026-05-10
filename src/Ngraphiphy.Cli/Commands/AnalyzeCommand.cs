using Spectre.Console.Cli;
namespace Ngraphiphy.Cli.Commands;
public sealed class AnalyzeSettings : CommandSettings { }
public sealed class AnalyzeCommand : AsyncCommand<AnalyzeSettings>
{
    protected override Task<int> ExecuteAsync(CommandContext context, AnalyzeSettings settings, CancellationToken cancellationToken)
        => Task.FromResult(0);
}
