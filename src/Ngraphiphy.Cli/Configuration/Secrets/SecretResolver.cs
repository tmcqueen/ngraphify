using Microsoft.Extensions.Configuration;

namespace Ngraphiphy.Cli.Configuration.Secrets;

public static class SecretResolver
{
    /// <summary>
    /// Walks all leaf values in <paramref name="snapshot"/>, resolves any pass:// and
    /// env:// references, and adds an in-memory overlay on <paramref name="configBuilder"/>
    /// so subsequent IOptions binding sees plain strings.
    /// </summary>
    public static void ResolveAndOverlay(
        IConfigurationBuilder configBuilder,
        IConfiguration snapshot,
        IReadOnlyDictionary<string, ISecretProvider> providers)
    {
        var overlay = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in snapshot.AsEnumerable(makePathsRelative: false))
        {
            if (value is null) continue;

            var reference = SecretReference.Parse(value);
            if (!reference.IsReference) continue;

            if (!providers.TryGetValue(reference.Scheme, out var provider))
                throw new InvalidOperationException(
                    $"No secret provider registered for scheme '{reference.Scheme}' " +
                    $"(key: {key}).");

            var resolved = provider.Resolve(reference.Path);
            if (resolved is not null)
                overlay[key] = resolved;
        }

        if (overlay.Count > 0)
            configBuilder.AddInMemoryCollection(overlay!);
    }
}
