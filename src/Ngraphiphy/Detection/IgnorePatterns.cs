using System.Text.RegularExpressions;

namespace Ngraphiphy.Detection;

public sealed class IgnorePatterns
{
    private record IgnoreScope(string BaseDir, Ignore.Ignore Matcher);

    private readonly List<IgnoreScope> _scopes = [];
    private readonly List<Regex> _sensitive = [];

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

    /// <summary>
    /// Load ignore rules from all .gitignore files found under rootDir,
    /// plus built-in sensitive file patterns.
    /// Each .gitignore file's patterns are scoped to its own directory.
    /// </summary>
    public static IgnorePatterns Load(string rootDir)
    {
        var ip = new IgnorePatterns();

        // Sensitive patterns always apply, regardless of gitignore
        foreach (var p in SensitivePatterns)
            ip._sensitive.Add(GlobToRegex(p));

        // Load every .gitignore file in the tree
        foreach (var gitignorePath in Directory.EnumerateFiles(rootDir, ".gitignore", SearchOption.AllDirectories))
        {
            var gitignoreDir = Path.GetDirectoryName(gitignorePath)!;
            var baseDir = Path.GetRelativePath(rootDir, gitignoreDir).Replace('\\', '/');
            if (baseDir == ".") baseDir = "";

            var matcher = new Ignore.Ignore();
            foreach (var line in File.ReadAllLines(gitignorePath))
            {
                var trimmed = line.Trim();
                if (trimmed.Length > 0 && !trimmed.StartsWith('#'))
                    matcher.Add(trimmed);
            }
            ip._scopes.Add(new IgnoreScope(baseDir, matcher));
        }

        return ip;
    }

    public bool IsIgnored(string relativePath)
    {
        // Block default noise directories (these may not be in .gitignore)
        var parts = relativePath.Split('/');
        foreach (var part in parts)
            if (DefaultIgnoreDirs.Contains(part, StringComparer.OrdinalIgnoreCase))
                return true;

        // Block sensitive files (secrets that may not be in .gitignore)
        var fileName = Path.GetFileName(relativePath);
        foreach (var pattern in _sensitive)
            if (pattern.IsMatch(fileName) || pattern.IsMatch(relativePath))
                return true;

        // Check each .gitignore scope
        foreach (var scope in _scopes)
        {
            string pathToCheck;
            if (scope.BaseDir == "")
            {
                pathToCheck = relativePath;
            }
            else if (relativePath.StartsWith(scope.BaseDir + "/", StringComparison.Ordinal))
            {
                // Strip the scope prefix so patterns are relative to the .gitignore's directory
                pathToCheck = relativePath[(scope.BaseDir.Length + 1)..];
            }
            else
            {
                continue;
            }

            if (scope.Matcher.IsIgnored(pathToCheck))
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
