// src/Ngraphiphy.Llm/GraphPlugin.cs
using System.ComponentModel;
using System.Text.Json;
using Ngraphiphy.Analysis;
using Ngraphiphy.Models;
using QuikGraph;

namespace Ngraphiphy.Llm;

/// <summary>
/// Graph query methods registered as AITool functions via AIFunctionFactory.Create().
/// [Description] attributes on methods and parameters become LLM tool documentation.
/// No Semantic Kernel dependency — this class has no special base class or attributes.
/// </summary>
public sealed class GraphPlugin
{
    private readonly BidirectionalGraph<Node, TaggedEdge<Node, Edge>> _graph;

    public GraphPlugin(BidirectionalGraph<Node, TaggedEdge<Node, Edge>> graph) => _graph = graph;

    [Description("Return the most connected nodes (god nodes) in the graph as JSON.")]
    public string GetGodNodes(
        [Description("Maximum number of nodes to return")] int topN = 5)
    {
        var nodes = GraphAnalyzer.GodNodes(_graph, topN);
        return JsonSerializer.Serialize(nodes.Select(n => new
        {
            n.Id, n.Label, n.SourceFile,
            Connections = _graph.InDegree(n) + _graph.OutDegree(n),
        }));
    }

    [Description("Return the most surprising cross-file or high-confidence edges as JSON.")]
    public string GetSurprisingConnections(
        [Description("Maximum connections to return")] int topN = 10)
    {
        var connections = GraphAnalyzer.SurprisingConnections(_graph, topN);
        return JsonSerializer.Serialize(connections.Select(c => new
        {
            Source = c.Source.Label,
            Target = c.Target.Label,
            c.Edge.Relation,
            c.Edge.ConfidenceString,
            c.Score,
        }));
    }

    [Description("Return summary statistics about the graph as JSON.")]
    public string GetSummaryStats()
    {
        var byFile = _graph.Vertices
            .GroupBy(n => n.SourceFile)
            .Select(g => new { File = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count).Take(10);
        return JsonSerializer.Serialize(new
        {
            NodeCount = _graph.VertexCount,
            EdgeCount = _graph.EdgeCount,
            TopFiles = byFile,
        });
    }

    [Description("Search for nodes whose label contains the given query string. Returns JSON.")]
    public string SearchNodes(
        [Description("Search term to match against node labels")] string query)
    {
        var matches = _graph.Vertices
            .Where(n => n.Label.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(20)
            .Select(n => new { n.Id, n.Label, n.SourceFile, n.FileTypeString });
        return JsonSerializer.Serialize(matches);
    }
}
