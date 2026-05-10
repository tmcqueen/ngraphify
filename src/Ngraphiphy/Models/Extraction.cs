// src/Ngraphiphy/Models/Extraction.cs
using System.Text.Json.Serialization;

namespace Ngraphiphy.Models;

public sealed class Extraction
{
    [JsonPropertyName("nodes")]
    public List<Node> Nodes { get; set; } = [];

    [JsonPropertyName("edges")]
    public List<Edge> Edges { get; set; } = [];

    [JsonPropertyName("source_file")]
    public string? SourceFile { get; set; }
}
