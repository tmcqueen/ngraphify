namespace Ngraphiphy.Cli.Configuration.Secrets;

internal interface IProcessRunner
{
    (string Stdout, string Stderr, int ExitCode) Run(string executable, string arguments);
}
