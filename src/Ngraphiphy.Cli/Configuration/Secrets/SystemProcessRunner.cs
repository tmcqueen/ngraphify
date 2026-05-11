using System.Diagnostics;

namespace Ngraphiphy.Cli.Configuration.Secrets;

internal sealed class SystemProcessRunner : IProcessRunner
{
    public (string Stdout, string Stderr, int ExitCode) Run(string executable, string arguments)
    {
        var psi = new ProcessStartInfo(executable, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start '{executable}'.");

        var stdout = proc.StandardOutput.ReadToEnd();
        var stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit();

        return (stdout, stderr, proc.ExitCode);
    }
}
