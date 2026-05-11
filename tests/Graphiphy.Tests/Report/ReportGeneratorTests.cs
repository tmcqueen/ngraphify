using Graphiphy.Build;
using Graphiphy.Models;
using Graphiphy.Report;

namespace Graphiphy.Tests.Report;

public class ReportGeneratorTests
{
    [Test]
    public async Task Generate_ProducesMarkdown()
    {
        var ext = new Models.Extraction
        {
            Nodes =
            [
                new() { Id = "a::Foo", Label = "Foo", FileTypeString = "code", SourceFile = "a.py", Community = 0 },
                new() { Id = "a::Bar", Label = "Bar", FileTypeString = "code", SourceFile = "a.py", Community = 0 },
                new() { Id = "b::Baz", Label = "Baz", FileTypeString = "code", SourceFile = "b.py", Community = 1 },
            ],
            Edges =
            [
                new() { Source = "a::Foo", Target = "a::Bar", Relation = "calls", ConfidenceString = "EXTRACTED", SourceFile = "a.py" },
                new() { Source = "a::Foo", Target = "b::Baz", Relation = "imports", ConfidenceString = "INFERRED", SourceFile = "a.py" },
            ]
        };
        var graph = GraphBuilder.Build([ext]);

        var report = ReportGenerator.Generate(graph);

        await Assert.That(report).Contains("# Graph Report");
        await Assert.That(report).Contains("node");
        await Assert.That(report).Contains("edge");
    }

    [Test]
    public async Task Generate_IncludesSummaryStats()
    {
        var ext = new Models.Extraction
        {
            Nodes =
            [
                new() { Id = "a::X", Label = "X", FileTypeString = "code", SourceFile = "a.py" },
            ],
            Edges = []
        };
        var graph = GraphBuilder.Build([ext]);

        var report = ReportGenerator.Generate(graph);

        await Assert.That(report).Contains("1");  // 1 node
    }

    [Test]
    public async Task Generate_EmptyGraph_DoesNotThrow()
    {
        var ext = new Models.Extraction { Nodes = [], Edges = [] };
        var graph = GraphBuilder.Build([ext]);

        var report = ReportGenerator.Generate(graph);

        await Assert.That(report).Contains("# Graph Report");
    }
}
