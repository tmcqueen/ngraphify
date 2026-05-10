using Spectre.Console.Cli;
namespace Ngraphiphy.Cli.Commands;
public sealed class ReportSettings : CommandSettings { }
public sealed class ReportCommand : AsyncCommand<ReportSettings>
{
    protected override Task<int> ExecuteAsync(CommandContext context, ReportSettings settings, CancellationToken cancellationToken)
        => Task.FromResult(0);
}
