using Ngraphiphy.Extraction;
using Ngraphiphy.Extraction.Extractors;

namespace Ngraphiphy.Tests.Extraction;

public class PythonExtractorTests : ExtractorTestBase
{
    protected override ILanguageExtractor CreateExtractor() => new PythonExtractor();

    [Test]
    public async Task Extract_FindsClasses()
    {
        var result = ExtractFixture("sample.py");
        var labels = NodeLabels(result);

        await Assert.That(labels).Contains("Transformer");
        await Assert.That(labels).Contains("Pipeline");
    }

    [Test]
    public async Task Extract_FindsFunctions()
    {
        var result = ExtractFixture("sample.py");
        var labels = NodeLabels(result);

        await Assert.That(labels).Contains("forward");
        await Assert.That(labels).Contains("_attention");
        await Assert.That(labels).Contains("helper");
        await Assert.That(labels).Contains("run");
    }

    [Test]
    public async Task Extract_FindsContainsEdges()
    {
        var result = ExtractFixture("sample.py");
        var relations = EdgeRelations(result, "Transformer", "forward");

        await Assert.That(relations).Contains("contains");
    }

    [Test]
    public async Task Extract_FindsCallEdges()
    {
        var result = ExtractFixture("sample.py");

        var calls = result.Edges.Where(e => e.Relation == "calls").ToList();
        await Assert.That(calls).IsNotEmpty();
    }

    [Test]
    public async Task Extract_FindsImports()
    {
        var result = ExtractFixture("sample.py");
        var imports = result.Edges.Where(e => e.Relation == "imports").ToList();

        await Assert.That(imports).IsNotEmpty();
    }

    [Test]
    public async Task Extract_NodesHaveSourceLocations()
    {
        var result = ExtractFixture("sample.py");

        await Assert.That(result.Nodes).Count().IsGreaterThan(0);
        foreach (var node in result.Nodes)
        {
            await Assert.That(node.SourceLocation).IsNotNull();
            await Assert.That(node.SourceLocation!).StartsWith("L");
        }
    }

    [Test]
    public async Task Extract_AllEdgesHaveValidConfidence()
    {
        var result = ExtractFixture("sample.py");

        foreach (var edge in result.Edges)
        {
            await Assert.That(new[] { "EXTRACTED", "INFERRED", "AMBIGUOUS" }).Contains(edge.ConfidenceString);
        }
    }

    [Test]
    public async Task Extract_NoDuplicateNodeIds()
    {
        var result = ExtractFixture("sample.py");
        var ids = result.Nodes.Select(n => n.Id).ToList();
        var distinct = ids.Distinct().ToList();

        await Assert.That(ids.Count).IsEqualTo(distinct.Count);
    }

    [Test]
    public async Task SupportedExtensions_IncludesPy()
    {
        var extractor = new PythonExtractor();
        await Assert.That(extractor.SupportedExtensions).Contains(".py");
    }
}
