using Microsoft.Extensions.Configuration;

namespace Ngraphiphy.Cli.Configuration.Secrets;

public static class SecretResolver
{
    /// <summary>
    /// Walks all leaf values in <paramref name="snapshot"/>, resolves any pass:// and
    /// env:// references, and adds an in-memory overlay on <paramref name="configBuilder"/>
    /// so subsequent IOptions binding sees plain strings.
    /// </summary>
    public static async Task ResolveAndOverlayAsync(
        IConfigurationBuilder configBuilder,
        IConfiguration snapshot,
        IReadOnlyDictionary<string, ISecretProvider> providers,
        Action<string>? warn = null,
        CancellationToken ct = default)
    {
        var overlay = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in snapshot.AsEnumerable(makePathsRelative: false))
        {
            if (value is null) continue;

            var reference = SecretReference.Parse(value);
            if (!reference.IsReference) continue;

            if (!providers.TryGetValue(reference.Scheme, out var provider))
            {
                warn?.Invoke($"Warning: No secret provider registered for scheme '{reference.Scheme}' (key: {key})");
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
                // Secret couldn't be resolved, but don't fail the entire CLI startup.
                // The command that actually needs this secret will fail with a clear error.
                warn?.Invoke($"Warning: Could not resolve secret reference '{key}': {ex.Message}");
                // Don't add to overlay — the config value (pass://...) remains
            }
        }

        if (overlay.Count > 0)
            configBuilder.AddInMemoryCollection(overlay!);
    }
}
