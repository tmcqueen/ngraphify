using Ngraphiphy.Models;

namespace Ngraphiphy.Detection;

public sealed record DetectedFile(string RelativePath, string AbsolutePath, FileType FileType);

public static class FileDetector
{
    public static List<DetectedFile> Detect(string rootDir)
    {
        var ignore = IgnorePatterns.Load(rootDir);
        var results = new List<DetectedFile>();

        foreach (var file in Directory.EnumerateFiles(rootDir, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(rootDir, file).Replace('\\', '/');

            if (ignore.IsIgnored(relativePath))
                continue;

            var fileType = FileClassifier.ClassifyOrNull(relativePath);
            if (fileType is null)
                continue;

            results.Add(new DetectedFile(relativePath, file, fileType.Value));
        }

        return results;
    }
}
