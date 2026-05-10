using Ngraphiphy.Models;
using QuikGraph;

namespace Ngraphiphy.Analysis;

public static class GraphAnalyzer
{
    public static List<Node> GodNodes(BidirectionalGraph<Node, TaggedEdge<Node, Edge>> graph, int topN = 5)
    {
        if (graph.VertexCount == 0) return [];

        return graph.Vertices
            .Where(n => n.FileTypeString == "code")
            .OrderByDescending(n => graph.InDegree(n) + graph.OutDegree(n))
            .Take(topN)
            .ToList();
    }

    public record SurprisingConnection(Edge Edge, Node Source, Node Target, double Score);

    public static List<SurprisingConnection> SurprisingConnections(
        BidirectionalGraph<Node, TaggedEdge<Node, Edge>> graph, int topN = 10)
    {
        var results = new List<SurprisingConnection>();

        foreach (var edge in graph.Edges)
        {
            var tag = edge.Tag;
            var source = edge.Source;
            var target = edge.Target;

            double score = SurpriseScore(tag, source, target);
            if (score > 0)
                results.Add(new SurprisingConnection(tag, source, target, score));
        }

        return results.OrderByDescending(s => s.Score).Take(topN).ToList();
    }

    private static double SurpriseScore(Edge edge, Node source, Node target)
    {
        double score = 0;

        // Cross-file bonus
        if (source.SourceFile != target.SourceFile)
            score += 1.0;

        // Confidence bonus (AMBIGUOUS > INFERRED > EXTRACTED)
        score += edge.Confidence switch
        {
            Confidence.Ambiguous => 2.0,
            Confidence.Inferred => 1.0,
            _ => 0.0,
        };

        // Cross file-type bonus
        if (source.FileTypeString != target.FileTypeString)
            score += 1.5;

        return score;
    }
}
