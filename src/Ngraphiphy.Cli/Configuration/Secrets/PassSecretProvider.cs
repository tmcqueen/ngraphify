namespace Ngraphiphy.Cli.Configuration.Secrets;

/// <summary>
/// Resolves secrets via `pass show path`. Results are cached by path for the process lifetime.
/// </summary>
public sealed class PassSecretProvider : ISecretProvider
{
    private readonly IProcessRunner _runner;
    private readonly Dictionary<string, string> _cache = new(StringComparer.Ordinal);

    public PassSecretProvider() : this(new SystemProcessRunner()) { }

    internal PassSecretProvider(IProcessRunner runner)
    {
        _runner = runner;
    }

    public string Resolve(string path)
    {
        if (_cache.TryGetValue(path, out var cached))
            return cached;

        var (stdout, stderr, exitCode) = _runner.Run("pass", $"show {path}");

        if (exitCode != 0)
            throw new InvalidOperationException(
                $"Secret reference pass://{path}: 'pass show {path}' exited {exitCode}. " +
                $"stderr: {stderr.Trim()}");

        // pass show outputs the secret on the first line; additional lines may contain metadata.
        var secret = stdout.Split('\n')[0].TrimEnd();
        _cache[path] = secret;
        return secret;
    }
}
