using Ngraphiphy.Pipeline;

namespace Ngraphiphy.Cli.Tests.Commands;

public class AnalyzeCommandTests
{
    [Test]
    public async Task AnalyzePythonRepo_FindsNodes()
    {
        var dir = CreateTempDir();
        File.WriteAllText(Path.Combine(dir, "main.py"), "class App:\n    def run(self): pass\n");
        var result = await RepositoryAnalysis.RunAsync(dir);
        await Assert.That(result.Graph.VertexCount).IsGreaterThan(0);
    }

    [Test]
    public async Task AnalyzeEmptyDir_ReturnsZeroFiles()
    {
        var dir = CreateTempDir();
        var result = await RepositoryAnalysis.RunAsync(dir);
        await Assert.That(result.Files.Count).IsEqualTo(0);
    }

    [Test]
    public async Task AnalyzeWithCache_SecondRunSameCount()
    {
        var dir = CreateTempDir();
        var cacheDir = Path.Combine(dir, ".cache");
        File.WriteAllText(Path.Combine(dir, "app.py"), "class X:\n    def y(self): pass");
        var r1 = await RepositoryAnalysis.RunAsync(dir, cacheDir: cacheDir);
        var r2 = await RepositoryAnalysis.RunAsync(dir, cacheDir: cacheDir);
        await Assert.That(r2.Graph.VertexCount).IsEqualTo(r1.Graph.VertexCount);
    }

    private static string CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "ngraphiphy_analyze_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(path);
        return path;
    }
}
