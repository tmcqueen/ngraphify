// src/Graphiphy/Models/Edge.cs
using System.Text.Json.Serialization;

namespace Graphiphy.Models;

public sealed class Edge
{
    [JsonPropertyName("source")]
    public required string Source { get; set; }

    [JsonPropertyName("target")]
    public required string Target { get; set; }

    [JsonPropertyName("relation")]
    public required string Relation { get; set; }

    [JsonPropertyName("confidence")]
    public required string ConfidenceString { get; set; }

    [JsonPropertyName("source_file")]
    public required string SourceFile { get; set; }

    [JsonPropertyName("source_location")]
    public string? SourceLocation { get; set; }

    [JsonPropertyName("weight")]
    public double Weight { get; set; } = 1.0;

    [JsonPropertyName("context")]
    public string? Context { get; set; }

    [JsonPropertyName("confidence_score")]
    public double? ConfidenceScore { get; set; }

    [JsonIgnore]
    public Confidence Confidence
    {
        get => ConfidenceExtensions.FromString(ConfidenceString);
        set => ConfidenceString = value.ToSchemaString();
    }
}
