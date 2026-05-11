namespace Ngraphiphy.Cli.Configuration.Secrets;

public interface ISecretProvider
{
    string Resolve(string path);
}
