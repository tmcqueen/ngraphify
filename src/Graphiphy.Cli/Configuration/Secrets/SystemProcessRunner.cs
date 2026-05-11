using System.Diagnostics;

namespace Graphiphy.Cli.Configuration.Secrets;

internal sealed class SystemProcessRunner : IProcessRunner
{
    public async Task<(string Stdout, string Stderr, int ExitCode)> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in arguments)
            psi.ArgumentList.Add(arg);

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start '{executable}'.");

        // Drain stdout and stderr concurrently to avoid pipe-buffer deadlock.
        var stdoutTask = proc.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = proc.StandardError.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        return (stdout, stderr, proc.ExitCode);
    }
}
