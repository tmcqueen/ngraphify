using Ngraphiphy.Extraction;
using Ngraphiphy.Extraction.Extractors;

namespace Ngraphiphy.Tests.Extraction;

public class CExtractorTests : ExtractorTestBase
{
    protected override ILanguageExtractor CreateExtractor() => new CExtractor();

    [Test]
    public async Task Extract_FindsStructs()
    {
        var result = ExtractFixture("sample.c");
        var labels = NodeLabels(result);
        await Assert.That(labels).Contains("Point");
        await Assert.That(labels).Contains("Node");
    }

    [Test]
    public async Task Extract_FindsFunctions()
    {
        var result = ExtractFixture("sample.c");
        var labels = NodeLabels(result);
        await Assert.That(labels).Contains("print_point");
        await Assert.That(labels).Contains("create_node");
        await Assert.That(labels).Contains("push");
        await Assert.That(labels).Contains("main");
    }

    [Test]
    public async Task Extract_FindsIncludes()
    {
        var result = ExtractFixture("sample.c");
        var imports = result.Edges.Where(e => e.Relation == "imports").ToList();
        await Assert.That(imports).IsNotEmpty();
    }

    [Test]
    public async Task Extract_FindsCallEdges()
    {
        var result = ExtractFixture("sample.c");
        var calls = result.Edges.Where(e => e.Relation == "calls").ToList();
        await Assert.That(calls).IsNotEmpty();
    }

    [Test]
    public async Task SupportedExtensions_IncludesCAndH()
    {
        var extractor = new CExtractor();
        await Assert.That(extractor.SupportedExtensions).Contains(".c");
        await Assert.That(extractor.SupportedExtensions).Contains(".h");
    }
}
