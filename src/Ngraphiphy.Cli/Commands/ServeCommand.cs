using Spectre.Console.Cli;
namespace Ngraphiphy.Cli.Commands;
public sealed class ServeSettings : CommandSettings { }
public sealed class ServeCommand : AsyncCommand<ServeSettings>
{
    protected override Task<int> ExecuteAsync(CommandContext context, ServeSettings settings, CancellationToken cancellationToken)
        => Task.FromResult(0);
}
