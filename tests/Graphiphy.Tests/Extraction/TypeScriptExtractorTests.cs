using Graphiphy.Extraction;
using Graphiphy.Extraction.Extractors;

namespace Graphiphy.Tests.Extraction;

public class TypeScriptExtractorTests : ExtractorTestBase
{
    protected override ILanguageExtractor CreateExtractor() => new TypeScriptExtractor();

    [Test]
    public async Task Extract_FindsClasses()
    {
        var result = ExtractFixture("sample.ts");
        var labels = NodeLabels(result);
        await Assert.That(labels).Contains("HttpServer");
        await Assert.That(labels).Contains("Response");
    }

    [Test]
    public async Task Extract_FindsInterfaces()
    {
        var result = ExtractFixture("sample.ts");
        var labels = NodeLabels(result);
        await Assert.That(labels).Contains("Config");
    }

    [Test]
    public async Task Extract_FindsFunctions()
    {
        var result = ExtractFixture("sample.ts");
        var labels = NodeLabels(result);
        await Assert.That(labels).Contains("createApp");
    }

    [Test]
    public async Task Extract_FindsMethods()
    {
        var result = ExtractFixture("sample.ts");
        var labels = NodeLabels(result);
        await Assert.That(labels).Contains("listen");
        await Assert.That(labels).Contains("handleRequest");
    }

    [Test]
    public async Task SupportedExtensions_IncludesTsAndTsx()
    {
        var extractor = new TypeScriptExtractor();
        await Assert.That(extractor.SupportedExtensions).Contains(".ts");
        await Assert.That(extractor.SupportedExtensions).Contains(".tsx");
    }
}
