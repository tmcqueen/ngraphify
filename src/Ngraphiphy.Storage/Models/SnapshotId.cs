using LibGit2Sharp;

namespace Ngraphiphy.Storage.Models;

public sealed record SnapshotId(string RootPath, string CommitHash)
{
    public string Id => $"{RootPath}::{CommitHash}";

    public static SnapshotId Resolve(string rootPath)
    {
        try
        {
            using var repo = new Repository(rootPath);
            var commitHash = repo.Head.Tip.Sha;
            return new(rootPath, commitHash);
        }
        catch (Exception)
        {
            // Not a git repo or error resolving — use content hash instead
            var contentHash = ComputeContentHash(rootPath);
            return new(rootPath, contentHash);
        }
    }

    private static string ComputeContentHash(string rootPath)
    {
        // Compute SHA256 of all .cs/.py/.js/.ts files in rootPath (sorted by name)
        using var hasher = System.Security.Cryptography.SHA256.Create();
        var files = Directory.GetFiles(rootPath, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".cs") || f.EndsWith(".py") || f.EndsWith(".js") || f.EndsWith(".ts"))
            .OrderBy(f => f)
            .ToList();

        foreach (var file in files)
        {
            var fileInfo = new FileInfo(file);
            var pathBytes = System.Text.Encoding.UTF8.GetBytes(file);
            hasher.TransformBlock(pathBytes, 0, pathBytes.Length, null, 0);
            hasher.TransformBlock(File.ReadAllBytes(file), 0, (int)fileInfo.Length, null, 0);
        }
        hasher.TransformFinalBlock([], 0, 0);

        return Convert.ToHexString(hasher.Hash!).ToLower()[..16]; // 16 chars
    }
}
