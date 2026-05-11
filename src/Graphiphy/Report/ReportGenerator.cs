using System.Text;
using Graphiphy.Analysis;
using Graphiphy.Models;
using QuikGraph;

namespace Graphiphy.Report;

public static class ReportGenerator
{
    public static string Generate(BidirectionalGraph<Node, TaggedEdge<Node, Edge>> graph)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Graph Report");
        sb.AppendLine();

        // Summary
        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine($"- {graph.VertexCount} node(s)");
        sb.AppendLine($"- {graph.EdgeCount} edge(s)");

        var communities = graph.Vertices
            .Where(n => n.Community.HasValue)
            .Select(n => n.Community!.Value)
            .Distinct()
            .Count();
        if (communities > 0)
            sb.AppendLine($"- **Communities:** {communities}");

        // Confidence breakdown
        var confidenceCounts = graph.Edges
            .GroupBy(e => e.Tag.ConfidenceString)
            .ToDictionary(g => g.Key, g => g.Count());
        if (confidenceCounts.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("### Confidence Breakdown");
            sb.AppendLine();
            foreach (var (conf, count) in confidenceCounts.OrderBy(kv => kv.Key))
            {
                var pct = graph.EdgeCount > 0 ? (100.0 * count / graph.EdgeCount) : 0;
                sb.AppendLine($"- {conf}: {count} ({pct:F1}%)");
            }
        }

        // God nodes
        var gods = GraphAnalyzer.GodNodes(graph, topN: 5);
        if (gods.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Most Connected Entities");
            sb.AppendLine();
            foreach (var node in gods)
            {
                var degree = graph.InDegree(node) + graph.OutDegree(node);
                sb.AppendLine($"- **{node.Label}** ({node.SourceFile}) — {degree} connections");
            }
        }

        // Surprising connections
        var surprises = GraphAnalyzer.SurprisingConnections(graph, topN: 5);
        if (surprises.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Surprising Connections");
            sb.AppendLine();
            foreach (var s in surprises)
            {
                sb.AppendLine($"- {s.Source.Label} → {s.Target.Label} ({s.Edge.Relation}, {s.Edge.ConfidenceString}) — score {s.Score:F1}");
            }
        }

        return sb.ToString();
    }
}
