using Ngraphiphy.Extraction;
using Ngraphiphy.Extraction.Extractors;

namespace Ngraphiphy.Tests.Extraction;

public class RustExtractorTests : ExtractorTestBase
{
    protected override ILanguageExtractor CreateExtractor() => new RustExtractor();

    [Test]
    public async Task Extract_FindsStructs()
    {
        var result = ExtractFixture("sample.rs");
        var labels = NodeLabels(result);
        await Assert.That(labels).Contains("Config");
        await Assert.That(labels).Contains("Router");
    }

    [Test]
    public async Task Extract_FindsTraits()
    {
        var result = ExtractFixture("sample.rs");
        var labels = NodeLabels(result);
        await Assert.That(labels).Contains("Handler");
    }

    [Test]
    public async Task Extract_FindsFunctions()
    {
        var result = ExtractFixture("sample.rs");
        var labels = NodeLabels(result);
        await Assert.That(labels).Contains("main");
        await Assert.That(labels).Contains("new");
        await Assert.That(labels).Contains("add_route");
        await Assert.That(labels).Contains("dispatch");
    }

    [Test]
    public async Task Extract_FindsUseImports()
    {
        var result = ExtractFixture("sample.rs");
        var imports = result.Edges.Where(e => e.Relation == "imports").ToList();
        await Assert.That(imports).IsNotEmpty();
    }

    [Test]
    public async Task SupportedExtensions()
    {
        var extractor = new RustExtractor();
        await Assert.That(extractor.SupportedExtensions).Contains(".rs");
    }
}
