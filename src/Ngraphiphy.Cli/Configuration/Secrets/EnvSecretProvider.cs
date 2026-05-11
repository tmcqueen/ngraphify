namespace Ngraphiphy.Cli.Configuration.Secrets;

public sealed class EnvSecretProvider : ISecretProvider
{
    public string Resolve(string path)
    {
        var value = Environment.GetEnvironmentVariable(path);
        if (value is null)
            throw new InvalidOperationException(
                $"Secret reference env://{path}: environment variable '{path}' is not set.");
        return value;
    }
}
