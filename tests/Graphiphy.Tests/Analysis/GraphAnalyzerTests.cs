using Graphiphy.Analysis;
using Graphiphy.Build;
using Graphiphy.Models;
using ExtractionModel = Graphiphy.Models.Extraction;

namespace Graphiphy.Tests.Analysis;

public class GraphAnalyzerTests
{
    private static ExtractionModel MakeStarGraph(string hub, string[] spokes)
    {
        var nodes = new List<Node> { new() { Id = hub, Label = hub.Split("::").Last(), FileTypeString = "code", SourceFile = "a.py" } };
        var edges = new List<Edge>();
        foreach (var s in spokes)
        {
            nodes.Add(new Node { Id = s, Label = s.Split("::").Last(), FileTypeString = "code", SourceFile = "a.py" });
            edges.Add(new Edge { Source = hub, Target = s, Relation = "calls", ConfidenceString = "EXTRACTED", SourceFile = "a.py" });
        }
        return new ExtractionModel { Nodes = nodes, Edges = edges };
    }

    [Test]
    public async Task GodNodes_FindsMostConnected()
    {
        var ext = MakeStarGraph("a::Hub", ["a::S1", "a::S2", "a::S3", "a::S4", "a::S5"]);
        var graph = GraphBuilder.Build([ext]);

        var gods = GraphAnalyzer.GodNodes(graph, topN: 1);

        await Assert.That(gods.Count).IsEqualTo(1);
        await Assert.That(gods[0].Label).IsEqualTo("Hub");
    }

    [Test]
    public async Task SurprisingConnections_CrossFileEdges()
    {
        var ext = new ExtractionModel
        {
            Nodes =
            [
                new() { Id = "a::Foo", Label = "Foo", FileTypeString = "code", SourceFile = "a.py" },
                new() { Id = "b::Bar", Label = "Bar", FileTypeString = "code", SourceFile = "b.py" },
            ],
            Edges =
            [
                new() { Source = "a::Foo", Target = "b::Bar", Relation = "calls", ConfidenceString = "AMBIGUOUS", SourceFile = "a.py" }
            ]
        };
        var graph = GraphBuilder.Build([ext]);

        var surprises = GraphAnalyzer.SurprisingConnections(graph, topN: 5);

        await Assert.That(surprises).IsNotEmpty();
    }

    [Test]
    public async Task GodNodes_ReturnsEmptyForEmptyGraph()
    {
        var ext = new ExtractionModel { Nodes = [], Edges = [] };
        var graph = GraphBuilder.Build([ext]);

        var gods = GraphAnalyzer.GodNodes(graph, topN: 5);

        await Assert.That(gods).IsEmpty();
    }
}
