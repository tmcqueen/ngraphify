namespace Ngraphiphy.Cli.Configuration.Secrets;

internal interface IProcessRunner
{
    Task<(string Stdout, string Stderr, int ExitCode)> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken ct = default);
}
