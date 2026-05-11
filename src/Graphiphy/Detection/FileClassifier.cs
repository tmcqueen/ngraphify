using Graphiphy.Models;

namespace Graphiphy.Detection;

public static class FileClassifier
{
    private static readonly HashSet<string> CodeExtensions =
    [
        ".py", ".js", ".ts", ".jsx", ".tsx", ".c", ".h", ".cpp", ".hpp", ".cc", ".cxx",
        ".cs", ".java", ".go", ".rs", ".rb", ".swift", ".kt", ".kts", ".scala", ".sc",
        ".php", ".lua", ".zig", ".ps1", ".psm1", ".ex", ".exs", ".m", ".mm", ".jl",
        ".v", ".sv", ".f90", ".f95", ".f03", ".f", ".for", ".pas", ".pp", ".lpr",
        ".dpr", ".dart", ".groovy", ".gvy", ".sql", ".sh", ".bash", ".zsh", ".fish",
        ".r", ".R", ".pl", ".pm", ".t", ".vb", ".fs", ".fsx", ".clj", ".cljs",
        ".erl", ".hrl", ".hs", ".lhs", ".ml", ".mli", ".nim", ".cr", ".d",
        ".ada", ".adb", ".ads", ".cob", ".cbl", ".lisp", ".cl", ".el",
        ".vue", ".svelte", ".astro",
    ];

    private static readonly HashSet<string> DocExtensions =
    [
        ".md", ".markdown", ".txt", ".rst", ".adoc", ".asciidoc", ".org",
        ".wiki", ".textile", ".rtf", ".docx", ".odt", ".tex", ".html", ".htm",
        ".yaml", ".yml", ".toml", ".json", ".xml", ".csv", ".tsv",
    ];

    private static readonly HashSet<string> PaperExtensions = [".pdf"];

    private static readonly HashSet<string> ImageExtensions =
    [
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tiff", ".tif",
        ".svg", ".webp", ".ico", ".heic", ".heif", ".avif",
    ];

    private static readonly HashSet<string> VideoExtensions =
    [
        ".mp4", ".webm", ".mkv", ".avi", ".mov", ".flv", ".wmv",
        ".m4v", ".ogv", ".3gp",
    ];

    public static FileType Classify(string filePath)
    {
        return ClassifyOrNull(filePath) ?? FileType.Concept;
    }

    public static FileType? ClassifyOrNull(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        if (CodeExtensions.Contains(ext))
            return FileType.Code;

        if (DocExtensions.Contains(ext))
            return FileType.Document;

        if (ImageExtensions.Contains(ext))
            return FileType.Image;

        if (VideoExtensions.Contains(ext))
            return FileType.Video;

        if (PaperExtensions.Contains(ext))
        {
            // PDFs inside .xcassets are icons, not papers
            if (filePath.Contains(".xcassets", StringComparison.OrdinalIgnoreCase))
                return FileType.Image;
            return FileType.Paper;
        }

        return null;
    }
}
