using Graphiphy.Pipeline;

namespace Graphiphy.Cli.Tests.Mcp;

public class GraphToolsTests
{
    private static async Task<GraphTools> MakeToolsAsync()
    {
        var dir = Path.Combine(Path.GetTempPath(), "graphiphy_mcp_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "app.py"), """
            class Server:
                def start(self): pass
                def stop(self): pass

            class Client:
                def connect(self): pass

            def main():
                s = Server()
                s.start()
            """);
        return new GraphTools(await RepositoryAnalysis.RunAsync(dir));
    }

    [Test]
    public async Task GetGodNodes_ContainsServer()
    {
        var result = (await MakeToolsAsync()).GetGodNodes(topN: 2);
        await Assert.That(result).Contains("Server");
    }

    [Test]
    public async Task GetReport_ContainsHeader()
    {
        var result = (await MakeToolsAsync()).GetReport();
        await Assert.That(result).Contains("# Graph Report");
    }

    [Test]
    public async Task GetSummaryStats_ContainsNodeCount()
    {
        var result = (await MakeToolsAsync()).GetSummaryStats();
        await Assert.That(result).Contains("NodeCount");
    }

    [Test]
    public async Task SearchNodes_FindsServer_NotClient()
    {
        var tools = await MakeToolsAsync();
        var result = tools.SearchNodes("Server");
        await Assert.That(result).Contains("Server");
        await Assert.That(result).DoesNotContain("Client");
    }

    [Test]
    public async Task GetGodNodes_EmptyGraph_ReturnsEmptyArray()
    {
        var dir = Path.Combine(Path.GetTempPath(), "graphiphy_mcp_empty_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        var tools = new GraphTools(await RepositoryAnalysis.RunAsync(dir));
        await Assert.That(tools.GetGodNodes()).IsEqualTo("[]");
    }
}
