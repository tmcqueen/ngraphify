using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ExtractionModel = Ngraphiphy.Models.Extraction;

namespace Ngraphiphy.Cache;

public sealed class ExtractionCache
{
    private readonly string _cacheDir;

    public ExtractionCache(string cacheDir)
    {
        _cacheDir = cacheDir;
    }

    public static string FileHash(string filePath, string rootDir)
    {
        var relativePath = Path.GetRelativePath(rootDir, filePath).Replace('\\', '/');
        var content = File.ReadAllText(filePath);

        // Strip markdown frontmatter before hashing
        if (relativePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            content = StripFrontmatter(content);

        var input = relativePath + "\n" + content;
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }

    public static string FileHash(string filePath, string rootDir, string fileContent)
    {
        var relativePath = Path.GetRelativePath(rootDir, filePath).Replace('\\', '/');
        var content = fileContent;

        // Strip markdown frontmatter before hashing
        if (relativePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            content = StripFrontmatter(content);

        var input = relativePath + "\n" + content;
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }

    public void Save(string hash, ExtractionModel extraction)
    {
        Directory.CreateDirectory(_cacheDir);
        var path = CachePath(hash);

        // Atomic write via temp file + rename
        var tmp = path + ".tmp";
        var json = JsonSerializer.Serialize(extraction, new JsonSerializerOptions { WriteIndented = false });
        File.WriteAllText(tmp, json);
        File.Move(tmp, path, overwrite: true);
    }

    public ExtractionModel? Load(string hash)
    {
        var path = CachePath(hash);
        if (!File.Exists(path))
            return null;

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<ExtractionModel>(json);
    }

    public void Clear()
    {
        if (Directory.Exists(_cacheDir))
            Directory.Delete(_cacheDir, recursive: true);
    }

    private string CachePath(string hash) => Path.Combine(_cacheDir, hash + ".json");

    private static string StripFrontmatter(string content)
    {
        if (!content.StartsWith("---"))
            return content;

        var endIndex = content.IndexOf("\n---", 3, StringComparison.Ordinal);
        if (endIndex < 0)
            return content;

        // Return everything after the closing ---
        var bodyStart = endIndex + 4; // skip "\n---"
        if (bodyStart < content.Length && content[bodyStart] == '\n')
            bodyStart++;

        return bodyStart < content.Length ? content[bodyStart..] : "";
    }
}
