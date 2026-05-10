using Ngraphiphy.Build;
using Ngraphiphy.Models;

namespace Ngraphiphy.Tests.Build;

public class GraphBuilderTests
{
    [Test]
    public async Task BuildFromExtraction_CreatesNodesAndEdges()
    {
        var extraction = new Models.Extraction
        {
            Nodes =
            [
                new Node { Id = "a::Foo", Label = "Foo", FileTypeString = "code", SourceFile = "a.py" },
                new Node { Id = "a::Bar", Label = "Bar", FileTypeString = "code", SourceFile = "a.py" },
            ],
            Edges =
            [
                new Edge { Source = "a::Foo", Target = "a::Bar", Relation = "calls", ConfidenceString = "EXTRACTED", SourceFile = "a.py" }
            ]
        };

        var graph = GraphBuilder.Build([extraction]);

        await Assert.That(graph.VertexCount).IsEqualTo(2);
        await Assert.That(graph.EdgeCount).IsEqualTo(1);
    }

    [Test]
    public async Task Build_MergesMultipleExtractions()
    {
        var ext1 = new Models.Extraction
        {
            Nodes = [new Node { Id = "a::Foo", Label = "Foo", FileTypeString = "code", SourceFile = "a.py" }],
            Edges = []
        };
        var ext2 = new Models.Extraction
        {
            Nodes = [new Node { Id = "b::Bar", Label = "Bar", FileTypeString = "code", SourceFile = "b.py" }],
            Edges = [new Edge { Source = "a::Foo", Target = "b::Bar", Relation = "imports", ConfidenceString = "EXTRACTED", SourceFile = "b.py" }]
        };

        var graph = GraphBuilder.Build([ext1, ext2]);

        await Assert.That(graph.VertexCount).IsEqualTo(2);
        await Assert.That(graph.EdgeCount).IsEqualTo(1);
    }

    [Test]
    public async Task Build_DeduplicatesNodes()
    {
        var ext1 = new Models.Extraction
        {
            Nodes = [new Node { Id = "a::Foo", Label = "Foo", FileTypeString = "code", SourceFile = "a.py" }],
            Edges = []
        };
        var ext2 = new Models.Extraction
        {
            Nodes = [new Node { Id = "a::Foo", Label = "Foo", FileTypeString = "code", SourceFile = "a.py" }],
            Edges = []
        };

        var graph = GraphBuilder.Build([ext1, ext2]);

        await Assert.That(graph.VertexCount).IsEqualTo(1);
    }

    [Test]
    public async Task Build_NormalizesBackslashPaths()
    {
        var extraction = new Models.Extraction
        {
            Nodes = [new Node { Id = "a::Foo", Label = "Foo", FileTypeString = "code", SourceFile = @"src\foo\a.py" }],
            Edges = []
        };

        var graph = GraphBuilder.Build([extraction]);
        var node = graph.Vertices.First();

        await Assert.That(node.SourceFile).IsEqualTo("src/foo/a.py");
    }

    [Test]
    public async Task Build_DefaultsNullFileTypeToConcept()
    {
        var extraction = new Models.Extraction
        {
            Nodes = [new Node { Id = "a::X", Label = "X", FileTypeString = null!, SourceFile = "a.py" }],
            Edges = []
        };

        var graph = GraphBuilder.Build([extraction]);
        var node = graph.Vertices.First();

        await Assert.That(node.FileTypeString).IsEqualTo("concept");
    }

    [Test]
    public async Task Build_DropsDanglingEdgesSilently()
    {
        var extraction = new Models.Extraction
        {
            Nodes = [new Node { Id = "a::Foo", Label = "Foo", FileTypeString = "code", SourceFile = "a.py" }],
            Edges = [new Edge { Source = "a::Foo", Target = "external::Bar", Relation = "imports", ConfidenceString = "INFERRED", SourceFile = "a.py" }]
        };

        var graph = GraphBuilder.Build([extraction]);

        // Dangling edge (target not in nodes) is dropped
        await Assert.That(graph.EdgeCount).IsEqualTo(0);
    }

    [Test]
    public async Task ToGraphData_RoundTrips()
    {
        var extraction = new Models.Extraction
        {
            Nodes =
            [
                new Node { Id = "a::Foo", Label = "Foo", FileTypeString = "code", SourceFile = "a.py" },
                new Node { Id = "a::Bar", Label = "Bar", FileTypeString = "code", SourceFile = "a.py" },
            ],
            Edges =
            [
                new Edge { Source = "a::Foo", Target = "a::Bar", Relation = "calls", ConfidenceString = "EXTRACTED", SourceFile = "a.py" }
            ]
        };

        var graph = GraphBuilder.Build([extraction]);
        var data = GraphBuilder.ToGraphData(graph);

        await Assert.That(data.Nodes.Count).IsEqualTo(2);
        await Assert.That(data.Edges.Count).IsEqualTo(1);
        await Assert.That(data.Edges[0].Source).IsEqualTo("a::Foo");
        await Assert.That(data.Edges[0].Target).IsEqualTo("a::Bar");
    }
}
