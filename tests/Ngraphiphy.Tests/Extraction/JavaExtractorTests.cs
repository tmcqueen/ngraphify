using Ngraphiphy.Extraction;
using Ngraphiphy.Extraction.Extractors;

namespace Ngraphiphy.Tests.Extraction;

public class JavaExtractorTests : ExtractorTestBase
{
    protected override ILanguageExtractor CreateExtractor() => new JavaExtractor();

    [Test]
    public async Task Extract_FindsClasses()
    {
        var result = ExtractFixture("sample.java");
        var labels = NodeLabels(result);
        await Assert.That(labels).Contains("UserService");
    }

    [Test]
    public async Task Extract_FindsInterfaces()
    {
        var result = ExtractFixture("sample.java");
        var labels = NodeLabels(result);
        await Assert.That(labels).Contains("UserRepository");
    }

    [Test]
    public async Task Extract_FindsMethods()
    {
        var result = ExtractFixture("sample.java");
        var labels = NodeLabels(result);
        await Assert.That(labels).Contains("findUser");
        await Assert.That(labels).Contains("listAll");
        await Assert.That(labels).Contains("validate");
    }

    [Test]
    public async Task Extract_FindsImports()
    {
        var result = ExtractFixture("sample.java");
        var imports = result.Edges.Where(e => e.Relation == "imports").ToList();
        await Assert.That(imports).IsNotEmpty();
    }

    [Test]
    public async Task SupportedExtensions()
    {
        var extractor = new JavaExtractor();
        await Assert.That(extractor.SupportedExtensions).Contains(".java");
    }
}
