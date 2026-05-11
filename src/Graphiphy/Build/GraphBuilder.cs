using Graphiphy.Models;
using QuikGraph;
using ExtractionModel = Graphiphy.Models.Extraction;

namespace Graphiphy.Build;

public static class GraphBuilder
{
    public static BidirectionalGraph<Node, TaggedEdge<Node, Edge>> Build(IEnumerable<ExtractionModel> extractions)
    {
        var graph = new BidirectionalGraph<Node, TaggedEdge<Node, Edge>>();
        var nodeIndex = new Dictionary<string, Node>();

        foreach (var extraction in extractions)
        {
            foreach (var node in extraction.Nodes)
            {
                // Normalize path separators
                node.SourceFile = node.SourceFile?.Replace('\\', '/') ?? "";

                // Default null file_type to concept
                if (string.IsNullOrEmpty(node.FileTypeString))
                    node.FileTypeString = "concept";

                if (!nodeIndex.ContainsKey(node.Id))
                {
                    nodeIndex[node.Id] = node;
                    graph.AddVertex(node);
                }
            }

            foreach (var edge in extraction.Edges)
            {
                edge.SourceFile = edge.SourceFile?.Replace('\\', '/') ?? "";

                // Drop dangling edges silently (external imports are expected)
                if (!nodeIndex.ContainsKey(edge.Source) || !nodeIndex.ContainsKey(edge.Target))
                    continue;

                var source = nodeIndex[edge.Source];
                var target = nodeIndex[edge.Target];
                var taggedEdge = new TaggedEdge<Node, Edge>(source, target, edge);
                graph.AddEdge(taggedEdge);
            }
        }

        return graph;
    }

    public static GraphData ToGraphData(BidirectionalGraph<Node, TaggedEdge<Node, Edge>> graph)
    {
        return new GraphData
        {
            Nodes = graph.Vertices.ToList(),
            Edges = graph.Edges.Select(e => e.Tag).ToList()
        };
    }

    public static BidirectionalGraph<Node, TaggedEdge<Node, Edge>> FromGraphData(GraphData data)
    {
        var extractions = new[] { new ExtractionModel { Nodes = data.Nodes, Edges = data.Edges } };
        return Build(extractions);
    }
}
