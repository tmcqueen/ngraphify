namespace Ngraphiphy.Cli.Configuration.Secrets;

public sealed class EnvSecretProvider : ISecretProvider
{
    public string Resolve(string path)
    {
        var value = Environment.GetEnvironmentVariable(path);
        if (value is null)
        {
            // Return empty string for missing env vars to allow help/version commands to work
            return string.Empty;
        }
        return value;
    }
}
