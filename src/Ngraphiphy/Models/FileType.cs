// src/Ngraphiphy/Models/FileType.cs
namespace Ngraphiphy.Models;

public enum FileType
{
    Code,
    Document,
    Paper,
    Image,
    Rationale,
    Concept,
    Video,
}

public static class FileTypeExtensions
{
    public static string ToSchemaString(this FileType ft) => ft switch
    {
        FileType.Code => "code",
        FileType.Document => "document",
        FileType.Paper => "paper",
        FileType.Image => "image",
        FileType.Rationale => "rationale",
        FileType.Concept => "concept",
        FileType.Video => "video",
        _ => "concept",
    };

    public static FileType FromString(string? s) => s?.ToLowerInvariant() switch
    {
        "code" => FileType.Code,
        "document" => FileType.Document,
        "paper" => FileType.Paper,
        "image" => FileType.Image,
        "rationale" => FileType.Rationale,
        "concept" => FileType.Concept,
        "video" => FileType.Video,
        _ => FileType.Concept,
    };
}
