using Graphiphy.Extraction;
using Graphiphy.Extraction.Extractors;

namespace Graphiphy.Tests.Extraction;

public class GoExtractorTests : ExtractorTestBase
{
    protected override ILanguageExtractor CreateExtractor() => new GoExtractor();

    [Test]
    public async Task Extract_FindsStructs()
    {
        var result = ExtractFixture("sample.go");
        var labels = NodeLabels(result);
        await Assert.That(labels).Contains("Server");
        await Assert.That(labels).Contains("Router");
    }

    [Test]
    public async Task Extract_FindsFunctions()
    {
        var result = ExtractFixture("sample.go");
        var labels = NodeLabels(result);
        await Assert.That(labels).Contains("NewServer");
        await Assert.That(labels).Contains("NewRouter");
        await Assert.That(labels).Contains("main");
    }

    [Test]
    public async Task Extract_FindsMethods()
    {
        var result = ExtractFixture("sample.go");
        var labels = NodeLabels(result);
        await Assert.That(labels).Contains("Start");
        await Assert.That(labels).Contains("Handle");
    }

    [Test]
    public async Task Extract_FindsImports()
    {
        var result = ExtractFixture("sample.go");
        var imports = result.Edges.Where(e => e.Relation == "imports").ToList();
        await Assert.That(imports).IsNotEmpty();
    }

    [Test]
    public async Task SupportedExtensions()
    {
        var extractor = new GoExtractor();
        await Assert.That(extractor.SupportedExtensions).Contains(".go");
    }
}
