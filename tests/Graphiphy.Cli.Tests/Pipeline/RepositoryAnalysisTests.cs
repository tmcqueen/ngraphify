using Graphiphy.Pipeline;

namespace Graphiphy.Cli.Tests.Pipeline;

public class RepositoryAnalysisTests
{
    [Test]
    public async Task RunAsync_OnEmptyDir_ReturnsEmptyGraph()
    {
        var dir = CreateTempDir();
        var result = await RepositoryAnalysis.RunAsync(dir);
        await Assert.That(result.Graph.VertexCount).IsEqualTo(0);
        await Assert.That(result.Report).Contains("# Graph Report");
    }

    [Test]
    public async Task RunAsync_OnPythonFile_FindsNodes()
    {
        var dir = CreateTempDir();
        File.WriteAllText(Path.Combine(dir, "app.py"), """
            class Foo:
                def bar(self): pass

            def main():
                Foo().bar()
            """);
        var result = await RepositoryAnalysis.RunAsync(dir);
        var labels = result.Graph.Vertices.Select(n => n.Label).ToList();
        await Assert.That(labels).Contains("Foo");
        await Assert.That(labels).Contains("main");
    }

    [Test]
    public async Task RunAsync_PopulatesReport()
    {
        var dir = CreateTempDir();
        File.WriteAllText(Path.Combine(dir, "a.py"), "class X: pass");
        var result = await RepositoryAnalysis.RunAsync(dir);
        await Assert.That(result.Report).IsNotNullOrEmpty();
    }

    [Test]
    public async Task RunAsync_UsesCache_SecondCallProducesSameCount()
    {
        var dir = CreateTempDir();
        var cacheDir = Path.Combine(dir, ".graphiphy-cache");
        File.WriteAllText(Path.Combine(dir, "b.py"), "class B: pass");
        var r1 = await RepositoryAnalysis.RunAsync(dir, cacheDir: cacheDir);
        var r2 = await RepositoryAnalysis.RunAsync(dir, cacheDir: cacheDir);
        await Assert.That(r2.Graph.VertexCount).IsEqualTo(r1.Graph.VertexCount);
    }

    private static string CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "graphiphy_test_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(path);
        return path;
    }
}
