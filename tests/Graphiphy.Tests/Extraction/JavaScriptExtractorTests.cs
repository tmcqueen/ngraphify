using Graphiphy.Extraction;
using Graphiphy.Extraction.Extractors;

namespace Graphiphy.Tests.Extraction;

public class JavaScriptExtractorTests : ExtractorTestBase
{
    protected override ILanguageExtractor CreateExtractor() => new JavaScriptExtractor();

    [Test]
    public async Task Extract_FindsClasses()
    {
        var result = ExtractFixture("sample.js");
        var labels = NodeLabels(result);
        await Assert.That(labels).Contains("EventEmitter");
    }

    [Test]
    public async Task Extract_FindsFunctions()
    {
        var result = ExtractFixture("sample.js");
        var labels = NodeLabels(result);
        await Assert.That(labels).Contains("createServer");
        await Assert.That(labels).Contains("handleRequest");
    }

    [Test]
    public async Task Extract_FindsMethods()
    {
        var result = ExtractFixture("sample.js");
        var labels = NodeLabels(result);
        await Assert.That(labels).Contains("on");
        await Assert.That(labels).Contains("emit");
    }

    [Test]
    public async Task Extract_FindsImports()
    {
        var result = ExtractFixture("sample.js");
        var imports = result.Edges.Where(e => e.Relation == "imports").ToList();
        await Assert.That(imports).IsNotEmpty();
    }

    [Test]
    public async Task Extract_FindsCallEdges()
    {
        var result = ExtractFixture("sample.js");
        var calls = result.Edges.Where(e => e.Relation == "calls").ToList();
        await Assert.That(calls).IsNotEmpty();
    }

    [Test]
    public async Task SupportedExtensions_IncludesJsAndJsx()
    {
        var extractor = new JavaScriptExtractor();
        await Assert.That(extractor.SupportedExtensions).Contains(".js");
        await Assert.That(extractor.SupportedExtensions).Contains(".jsx");
    }
}
