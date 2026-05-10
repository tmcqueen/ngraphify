using Ngraphiphy.Pipeline;

namespace Ngraphiphy.Cli.Tests.Mcp;

public class McpToolDescriptionTests
{
    [Test]
    public async Task AllToolMethods_ReturnNonEmpty()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ngraphiphy_mcp2_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "x.py"), "class X:\n    def y(self): pass\n");
        var tools = new GraphTools(await RepositoryAnalysis.RunAsync(dir));

        foreach (var result in new[]
        {
            tools.GetGodNodes(),
            tools.GetSurprisingConnections(),
            tools.GetSummaryStats(),
            tools.SearchNodes("X"),
            tools.GetReport(),
        })
            await Assert.That(result).IsNotNullOrEmpty();
    }
}
