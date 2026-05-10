using Spectre.Console.Cli;
namespace Ngraphiphy.Cli.Commands;
public sealed class QuerySettings : CommandSettings { }
public sealed class QueryCommand : AsyncCommand<QuerySettings>
{
    protected override Task<int> ExecuteAsync(CommandContext context, QuerySettings settings, CancellationToken cancellationToken)
        => Task.FromResult(0);
}
