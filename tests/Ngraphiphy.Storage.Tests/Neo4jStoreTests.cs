using Ngraphiphy.Pipeline;
using Ngraphiphy.Storage.Models;
using Ngraphiphy.Storage.Providers.Neo4j;

namespace Ngraphiphy.Storage.Tests;

[Skip("Requires Neo4j instance on bolt://localhost:7687")]
public class Neo4jStoreTests
{
    [Test]
    public async Task SaveSnapshot_CreatesNodesAndEdges()
    {
        var config = new Neo4jConfig();
        var store = await Neo4jStore.CreateAsync(config);

        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"ngraphiphy_neo4j_{Guid.NewGuid().ToString("N")[..8]}");
            Directory.CreateDirectory(tempDir);
            try
            {
                // Create a minimal test file
                File.WriteAllText(Path.Combine(tempDir, "test.cs"), "public class TestClass { }");

                var analysis = await RepositoryAnalysis.RunAsync(tempDir);
                var snapshotId = SnapshotId.Resolve(tempDir);

                await store.SaveSnapshotAsync(analysis, snapshotId, CancellationToken.None);

                var exists = await store.SnapshotExistsAsync(snapshotId, CancellationToken.None);
                await Assert.That(exists).IsTrue();
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        }
        finally
        {
            await ((IAsyncDisposable)store).DisposeAsync();
        }
    }

    [Test]
    public async Task SnapshotExists_ReturnsFalseForNonExistentSnapshot()
    {
        var config = new Neo4jConfig();
        var store = await Neo4jStore.CreateAsync(config);

        try
        {
            var snapshotId = new SnapshotId("/nonexistent/path", "abc123def456");
            var exists = await store.SnapshotExistsAsync(snapshotId, CancellationToken.None);

            await Assert.That(exists).IsFalse();
        }
        finally
        {
            await ((IAsyncDisposable)store).DisposeAsync();
        }
    }
}
