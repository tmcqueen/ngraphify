using Ngraphiphy.Dedup;
using Ngraphiphy.Models;

namespace Ngraphiphy.Tests.Dedup;

public class EntityDeduplicatorPerfTests
{
    [Test]
    public async Task Deduplicate_LargeInput_CompletesInReasonableTime()
    {
        var nodes = Enumerable.Range(0, 5000)
            .Select(i => new Node
            {
                Id = $"n{i}",
                Label = Guid.NewGuid().ToString("N"),
                FileTypeString = "code",
                SourceFile = $"file{i}.cs",
            })
            .ToList();
        var edges = new List<Edge>();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var (resultNodes, _) = EntityDeduplicator.Deduplicate(nodes, edges);

        sw.Stop();
        await Assert.That(resultNodes.Count).IsEqualTo(5000);
        await Assert.That(sw.Elapsed).IsLessThan(TimeSpan.FromSeconds(5));
    }
}
