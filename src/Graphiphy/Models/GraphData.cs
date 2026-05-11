// src/Graphiphy/Models/GraphData.cs
using System.Text.Json.Serialization;

namespace Graphiphy.Models;

public sealed class GraphData
{
    [JsonPropertyName("nodes")]
    public List<Node> Nodes { get; set; } = [];

    [JsonPropertyName("edges")]
    public List<Edge> Edges { get; set; } = [];

    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }
}
