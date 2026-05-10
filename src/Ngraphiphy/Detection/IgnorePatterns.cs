using System.Text.RegularExpressions;

namespace Ngraphiphy.Detection;

public sealed class IgnorePatterns
{
    private readonly List<Regex> _patterns = [];

    private static readonly string[] DefaultIgnoreDirs =
    [
        ".git", ".hg", ".svn", "__pycache__", "node_modules", ".tox",
        ".mypy_cache", ".pytest_cache", "dist", "build", ".eggs",
        "graphify-out", ".graphify-out", "venv", ".venv", "env",
        ".idea", ".vs", ".vscode", "bin", "obj",
    ];

    private static readonly string[] SensitivePatterns =
    [
        ".env", ".env.*", "*.pem", "id_rsa*", "id_ed25519*",
        "credentials.json", "service-account*.json", "*.key",
    ];

    public static IgnorePatterns Load(string rootDir)
    {
        var patterns = new IgnorePatterns();

        // Add sensitive file patterns
        foreach (var p in SensitivePatterns)
            patterns.Add(p);

        // Load .graphifyignore if present
        var ignoreFile = Path.Combine(rootDir, ".graphifyignore");
        if (File.Exists(ignoreFile))
        {
            foreach (var line in File.ReadAllLines(ignoreFile))
            {
                var trimmed = line.Trim();
                if (trimmed.Length > 0 && !trimmed.StartsWith('#'))
                    patterns.Add(trimmed);
            }
        }

        return patterns;
    }

    public void Add(string globPattern)
    {
        _patterns.Add(GlobToRegex(globPattern));
    }

    public bool IsIgnored(string relativePath)
    {
        // Check default ignored directories
        var parts = relativePath.Split('/', '\\');
        foreach (var part in parts)
        {
            if (part.StartsWith('.') && part.Length > 1)
                return true;
            if (DefaultIgnoreDirs.Contains(part))
                return true;
        }

        // Check custom patterns
        var fileName = Path.GetFileName(relativePath);
        foreach (var pattern in _patterns)
        {
            if (pattern.IsMatch(fileName) || pattern.IsMatch(relativePath))
                return true;
        }

        return false;
    }

    private static Regex GlobToRegex(string glob)
    {
        var escaped = Regex.Escape(glob)
            .Replace(@"\*\*", ".*")
            .Replace(@"\*", "[^/]*")
            .Replace(@"\?", ".");
        return new Regex($"^{escaped}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }
}
