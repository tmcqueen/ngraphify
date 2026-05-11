using Microsoft.Extensions.Configuration;

namespace Graphiphy.Cli.Configuration.Secrets;

public static class SecretResolver
{
    /// <summary>
    /// Walks leaf values in <paramref name="snapshot"/>, resolves pass:// and env://
    /// references, and adds an in-memory overlay so IOptions binding sees plain strings.
    /// </summary>
    /// <param name="sectionPrefixes">
    /// When provided, only keys whose path starts with one of these prefixes are processed.
    /// Use this to avoid eagerly resolving secrets for config sections that are read lazily
    /// (e.g. Providers:* resolved on demand by AgentProviderResolver).
    /// </param>
    public static async Task ResolveAndOverlayAsync(
        IConfigurationBuilder configBuilder,
        IConfiguration snapshot,
        IReadOnlyDictionary<string, ISecretProvider> providers,
        Action<string>? warn = null,
        IEnumerable<string>? sectionPrefixes = null,
        CancellationToken ct = default)
    {
        var prefixes = sectionPrefixes?.ToList();
        var overlay = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in snapshot.AsEnumerable(makePathsRelative: false))
        {
            if (value is null) continue;

            // When sectionPrefixes is specified, skip keys outside those sections.
            // This prevents eager resolution of secrets for providers that aren't active.
            if (prefixes is not null)
            {
                var matchesPrefix = prefixes.Any(p =>
                    key.Equals(p, StringComparison.OrdinalIgnoreCase) ||
                    key.StartsWith(p + ":", StringComparison.OrdinalIgnoreCase));
                if (!matchesPrefix) continue;
            }

            var reference = SecretReference.Parse(value);
            if (!reference.IsReference) continue;

            if (!providers.TryGetValue(reference.Scheme, out var provider))
            {
                warn?.Invoke($"No secret provider registered for scheme '{reference.Scheme}' (key: {key})");
                continue;
            }

            try
            {
                var resolved = await provider.ResolveAsync(reference.Path, ct);
                overlay[key] = resolved;
            }
            catch (Exception ex) when (
                ex is InvalidOperationException
                || ex is System.ComponentModel.Win32Exception
                || ex is IOException)
            {
                // Secret couldn't be resolved at startup — the command that needs it will fail clearly.
                warn?.Invoke($"Could not resolve secret reference '{key}': {ex.Message}");
            }
        }

        if (overlay.Count > 0)
            configBuilder.AddInMemoryCollection(overlay!);
    }

    /// <summary>
    /// Resolves a single config value if it is a pass:// or env:// reference; returns it as-is otherwise.
    /// Suitable for use as a delegate passed to provider resolvers for lazy secret resolution.
    /// </summary>
    public static string? ResolveValue(
        string? value,
        IReadOnlyDictionary<string, ISecretProvider> providers)
    {
        if (value is null) return null;
        var reference = SecretReference.Parse(value);
        if (!reference.IsReference) return value;

        if (!providers.TryGetValue(reference.Scheme, out var provider))
            throw new InvalidOperationException(
                $"No secret provider registered for scheme '{reference.Scheme}'.");

        return provider.ResolveAsync(reference.Path).GetAwaiter().GetResult();
    }
}
