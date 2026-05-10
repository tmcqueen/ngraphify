using Ngraphiphy.Detection;
using Ngraphiphy.Models;

namespace Ngraphiphy.Tests.Detection;

public class FileClassifierTests
{
    [Test]
    [Arguments(".py", FileType.Code)]
    [Arguments(".js", FileType.Code)]
    [Arguments(".ts", FileType.Code)]
    [Arguments(".c", FileType.Code)]
    [Arguments(".cpp", FileType.Code)]
    [Arguments(".cs", FileType.Code)]
    [Arguments(".java", FileType.Code)]
    [Arguments(".go", FileType.Code)]
    [Arguments(".rs", FileType.Code)]
    [Arguments(".rb", FileType.Code)]
    [Arguments(".swift", FileType.Code)]
    [Arguments(".kt", FileType.Code)]
    public async Task CodeExtensions_ClassifiedAsCode(string ext, FileType expected)
    {
        var result = FileClassifier.Classify($"example{ext}");
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    [Arguments(".md", FileType.Document)]
    [Arguments(".txt", FileType.Document)]
    [Arguments(".rst", FileType.Document)]
    [Arguments(".adoc", FileType.Document)]
    public async Task DocExtensions_ClassifiedAsDocument(string ext, FileType expected)
    {
        var result = FileClassifier.Classify($"example{ext}");
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    [Arguments(".png", FileType.Image)]
    [Arguments(".jpg", FileType.Image)]
    [Arguments(".svg", FileType.Image)]
    public async Task ImageExtensions_ClassifiedAsImage(string ext, FileType expected)
    {
        var result = FileClassifier.Classify($"example{ext}");
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    [Arguments(".mp4", FileType.Video)]
    [Arguments(".webm", FileType.Video)]
    public async Task VideoExtensions_ClassifiedAsVideo(string ext, FileType expected)
    {
        var result = FileClassifier.Classify($"example{ext}");
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task PdfInXcassets_ClassifiedAsImage()
    {
        var result = FileClassifier.Classify("Assets.xcassets/icon.imageset/logo.pdf");
        await Assert.That(result).IsEqualTo(FileType.Image);
    }

    [Test]
    public async Task PdfNormal_ClassifiedAsPaper()
    {
        var result = FileClassifier.Classify("research/attention.pdf");
        await Assert.That(result).IsEqualTo(FileType.Paper);
    }

    [Test]
    public async Task UnknownExtension_ReturnsNull()
    {
        var result = FileClassifier.ClassifyOrNull("data.xyz123");
        await Assert.That(result).IsNull();
    }
}
