// tests/Graphiphy.Llm.Tests/GraphPluginTests.cs
using Graphiphy.Build;
using Graphiphy.Llm;
using QuikGraph;
using ExtractionModel = Graphiphy.Models.Extraction;

namespace Graphiphy.Llm.Tests;

public class GraphPluginTests
{
    private static BidirectionalGraph<Graphiphy.Models.Node, TaggedEdge<Graphiphy.Models.Node, Graphiphy.Models.Edge>> MakeGraph()
    {
        var ext = new ExtractionModel
        {
            Nodes =
            [
                new() { Id = "a::Hub",   Label = "Hub",   FileTypeString = "code", SourceFile = "a.py" },
                new() { Id = "a::Spoke", Label = "Spoke", FileTypeString = "code", SourceFile = "a.py" },
                new() { Id = "b::Other", Label = "Other", FileTypeString = "code", SourceFile = "b.py" },
            ],
            Edges =
            [
                new() { Source = "a::Hub", Target = "a::Spoke", Relation = "calls",
                        ConfidenceString = "EXTRACTED", SourceFile = "a.py" },
                new() { Source = "a::Hub", Target = "b::Other", Relation = "calls",
                        ConfidenceString = "AMBIGUOUS", SourceFile = "a.py" },
            ]
        };
        return GraphBuilder.Build([ext]);
    }

    [Test]
    public async Task GetGodNodes_ContainsHub()
    {
        var plugin = new GraphPlugin(MakeGraph());
        var result = plugin.GetGodNodes(topN: 1);
        await Assert.That(result).IsNotNullOrEmpty();
        await Assert.That(result).Contains("Hub");
    }

    [Test]
    public async Task GetSurprisingConnections_ReturnsNonEmptyJson()
    {
        var plugin = new GraphPlugin(MakeGraph());
        var result = plugin.GetSurprisingConnections(topN: 5);
        await Assert.That(result).IsNotNullOrEmpty();
    }

    [Test]
    public async Task GetSummaryStats_ContainsNodeAndEdgeCounts()
    {
        var plugin = new GraphPlugin(MakeGraph());
        var result = plugin.GetSummaryStats();
        await Assert.That(result).Contains("3");  // NodeCount
        await Assert.That(result).Contains("2");  // EdgeCount
    }

    [Test]
    public async Task SearchNodes_ReturnsMatchAndExcludesNonMatch()
    {
        var plugin = new GraphPlugin(MakeGraph());
        var result = plugin.SearchNodes("Hub");
        await Assert.That(result).Contains("Hub");
        await Assert.That(result).DoesNotContain("Spoke");
    }
}
