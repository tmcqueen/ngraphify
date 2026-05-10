using Ngraphiphy.Build;
using Ngraphiphy.Llm;
using Ngraphiphy.Models;
using QuikGraph;
using ExtractionModel = Ngraphiphy.Models.Extraction;

namespace Ngraphiphy.Llm.Tests;

public class GraphPluginTests
{
    private static BidirectionalGraph<Node, TaggedEdge<Node, Edge>> MakeGraph()
    {
        var ext = new ExtractionModel
        {
            Nodes =
            [
                new() { Id = "a::Hub", Label = "Hub", FileTypeString = "code", SourceFile = "a.py" },
                new() { Id = "a::Spoke1", Label = "Spoke1", FileTypeString = "code", SourceFile = "a.py" },
                new() { Id = "b::Other", Label = "Other", FileTypeString = "code", SourceFile = "b.py" },
            ],
            Edges =
            [
                new() { Source = "a::Hub", Target = "a::Spoke1", Relation = "calls",
                        ConfidenceString = "EXTRACTED", SourceFile = "a.py" },
                new() { Source = "a::Hub", Target = "b::Other", Relation = "calls",
                        ConfidenceString = "AMBIGUOUS", SourceFile = "a.py" },
            ]
        };
        return GraphBuilder.Build([ext]);
    }

    [Test]
    public async Task GetGodNodes_ReturnsJson()
    {
        var plugin = new GraphPlugin(MakeGraph());
        var result = plugin.GetGodNodes(topN: 1);

        await Assert.That(result).IsNotNullOrEmpty();
        await Assert.That(result).Contains("Hub");
    }

    [Test]
    public async Task GetSurprisingConnections_ReturnsJson()
    {
        var plugin = new GraphPlugin(MakeGraph());
        var result = plugin.GetSurprisingConnections(topN: 5);

        await Assert.That(result).IsNotNullOrEmpty();
    }

    [Test]
    public async Task GetSummaryStats_ReturnsNodeAndEdgeCount()
    {
        var plugin = new GraphPlugin(MakeGraph());
        var result = plugin.GetSummaryStats();

        await Assert.That(result).Contains("3");   // 3 nodes
        await Assert.That(result).Contains("2");   // 2 edges
    }

    [Test]
    public async Task SearchNodes_FiltersById()
    {
        var plugin = new GraphPlugin(MakeGraph());
        var result = plugin.SearchNodes("Hub");

        await Assert.That(result).Contains("Hub");
        await Assert.That(result).DoesNotContain("Spoke1");
    }
}
