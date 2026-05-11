namespace Graphiphy.Cli.Configuration.Secrets;

public sealed class EnvSecretProvider : ISecretProvider
{
    public Task<string> ResolveAsync(string path, CancellationToken ct = default)
    {
        var value = Environment.GetEnvironmentVariable(path)
            ?? throw new InvalidOperationException(
                $"Secret reference env://{path}: environment variable '{path}' is not set.");
        return Task.FromResult(value);
    }
}
