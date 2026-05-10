using Ngraphiphy.Extraction;
using Ngraphiphy.Extraction.Extractors;

namespace Ngraphiphy.Tests.Extraction;

public class CSharpExtractorTests : ExtractorTestBase
{
    protected override ILanguageExtractor CreateExtractor() => new CSharpExtractor();

    [Test]
    public async Task Extract_FindsClasses()
    {
        var result = ExtractFixture("sample.cs");
        var labels = NodeLabels(result);
        await Assert.That(labels).Contains("UserRepository");
    }

    [Test]
    public async Task Extract_FindsInterfaces()
    {
        var result = ExtractFixture("sample.cs");
        var labels = NodeLabels(result);
        await Assert.That(labels).Contains("IRepository");
    }

    [Test]
    public async Task Extract_FindsRecords()
    {
        var result = ExtractFixture("sample.cs");
        var labels = NodeLabels(result);
        await Assert.That(labels).Contains("User");
    }

    [Test]
    public async Task Extract_FindsMethods()
    {
        var result = ExtractFixture("sample.cs");
        var labels = NodeLabels(result);
        await Assert.That(labels).Contains("GetById");
        await Assert.That(labels).Contains("Save");
        await Assert.That(labels).Contains("Validate");
    }

    [Test]
    public async Task Extract_FindsUsings()
    {
        var result = ExtractFixture("sample.cs");
        var imports = result.Edges.Where(e => e.Relation == "imports").ToList();
        await Assert.That(imports).IsNotEmpty();
    }

    [Test]
    public async Task SupportedExtensions()
    {
        var extractor = new CSharpExtractor();
        await Assert.That(extractor.SupportedExtensions).Contains(".cs");
    }
}
