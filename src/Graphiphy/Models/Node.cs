// src/Graphiphy/Models/Node.cs
using System.Text.Json.Serialization;

namespace Graphiphy.Models;

public sealed class Node
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("label")]
    public required string Label { get; set; }

    [JsonPropertyName("file_type")]
    public required string FileTypeString { get; set; }

    [JsonPropertyName("source_file")]
    public required string SourceFile { get; set; }

    [JsonPropertyName("source_location")]
    public string? SourceLocation { get; set; }

    [JsonPropertyName("community")]
    public int? Community { get; set; }

    [JsonPropertyName("norm_label")]
    public string? NormLabel { get; set; }

    [JsonIgnore]
    public FileType FileType
    {
        get => FileTypeExtensions.FromString(FileTypeString);
        set => FileTypeString = value.ToSchemaString();
    }
}
