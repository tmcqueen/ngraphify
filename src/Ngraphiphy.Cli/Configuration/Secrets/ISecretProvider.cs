namespace Ngraphiphy.Cli.Configuration.Secrets;

public interface ISecretProvider
{
    /// <summary>
    /// Resolves a secret reference. Throws InvalidOperationException if the secret cannot be resolved.
    /// </summary>
    /// <param name="path">The secret path/identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The resolved secret value (never null)</returns>
    /// <exception cref="InvalidOperationException">Thrown when the secret cannot be resolved</exception>
    Task<string> ResolveAsync(string path, CancellationToken ct = default);
}
