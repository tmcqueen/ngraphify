using Ngraphiphy.Extraction;
using Ngraphiphy.Extraction.Extractors;

namespace Ngraphiphy.Tests.Extraction;

public class CppExtractorTests : ExtractorTestBase
{
    protected override ILanguageExtractor CreateExtractor() => new CppExtractor();

    [Test]
    public async Task Extract_FindsClasses()
    {
        var result = ExtractFixture("sample.cpp");
        var labels = NodeLabels(result);
        await Assert.That(labels).Contains("Graph");
        await Assert.That(labels).Contains("Stack");
    }

    [Test]
    public async Task Extract_FindsMethods()
    {
        var result = ExtractFixture("sample.cpp");
        var labels = NodeLabels(result);
        await Assert.That(labels).Contains("addEdge");
        await Assert.That(labels).Contains("bfs");
    }

    [Test]
    public async Task Extract_FindsFunctions()
    {
        var result = ExtractFixture("sample.cpp");
        var labels = NodeLabels(result);
        await Assert.That(labels).Contains("main");
    }

    [Test]
    public async Task Extract_FindsIncludes()
    {
        var result = ExtractFixture("sample.cpp");
        var imports = result.Edges.Where(e => e.Relation == "imports").ToList();
        await Assert.That(imports).IsNotEmpty();
    }

    [Test]
    public async Task SupportedExtensions_IncludesCpp()
    {
        var extractor = new CppExtractor();
        await Assert.That(extractor.SupportedExtensions).Contains(".cpp");
        await Assert.That(extractor.SupportedExtensions).Contains(".hpp");
        await Assert.That(extractor.SupportedExtensions).Contains(".cc");
    }
}
