using Neo4j.Driver;
using Ngraphiphy.Models;
using Ngraphiphy.Storage.Models;

namespace Ngraphiphy.Storage.Providers.Neo4j;

public sealed class Neo4jStore : BoltStoreBase
{
    private Neo4jStore(IDriver driver, int vectorDimensions = 768)
        : base(driver, vectorDimensions)
    {
    }

    public static async Task<Neo4jStore> CreateAsync(Neo4jConfig config, int vectorDimensions = 768, CancellationToken ct = default)
    {
        var driver = GraphDatabase.Driver(config.Uri, AuthTokens.Basic(config.Username, config.Password));
        var store = new Neo4jStore(driver, vectorDimensions);

        // Initialize vector index (idempotent)
        var session = driver.AsyncSession();
        try
        {
            await session.ExecuteWriteAsync(async tx =>
            {
                // Create constraints
                await tx.RunAsync("CREATE CONSTRAINT snapshot_id IF NOT EXISTS FOR (s:Snapshot) REQUIRE s.id IS UNIQUE");
                await tx.RunAsync("CREATE CONSTRAINT graphnode_id IF NOT EXISTS FOR (n:GraphNode) REQUIRE (n.id, n.snapshotId) IS UNIQUE");

                // Create vector index (Neo4j 5.x syntax)
                await tx.RunAsync(
                    $@"CREATE VECTOR INDEX nodeEmbeddings IF NOT EXISTS
                       FOR (n:GraphNode) ON n.embedding
                       OPTIONS {{indexConfig: {{'vector.dimensions': {vectorDimensions},
                                               'vector.similarity_function': 'cosine'}}}}");
                return 0;
            });
        }
        finally
        {
            await session.CloseAsync();
        }

        return store;
    }

    public override async Task EmbedNodesAsync(SnapshotId id, IEmbeddingProvider embedder, CancellationToken ct)
    {
        // Phase 1: Retrieve all nodes in snapshot
        IReadOnlyList<IRecord> nodes;
        var readSession = _driver.AsyncSession();
        try
        {
            nodes = await readSession.ExecuteReadAsync(
                async tx =>
                {
                    var cursor = await tx.RunAsync(
                        "MATCH (n:GraphNode {snapshotId: $snapshotId}) RETURN n.id as id, n.label as label",
                        new { snapshotId = id.Id });
                    return await cursor.ToListAsync();
                });
        }
        finally
        {
            await readSession.CloseAsync();
        }

        // Phase 2: Embed each node in a separate write session
        var writeSession = _driver.AsyncSession();
        try
        {
            await writeSession.ExecuteWriteAsync(async tx =>
            {
                foreach (var record in nodes)
                {
                    ct.ThrowIfCancellationRequested();
                    var nodeId = (string)record["id"];
                    var label = (string)record["label"];
                    var embedding = await embedder.EmbedAsync(label, ct);

                    await tx.RunAsync(
                        @"MATCH (n:GraphNode {id: $id, snapshotId: $snapshotId})
                          SET n.embedding = $embedding",
                        new { id = nodeId, snapshotId = id.Id, embedding = embedding });
                }
                return 0;
            });
        }
        finally
        {
            await writeSession.CloseAsync();
        }
    }

    public override async Task<IReadOnlyList<Node>> SearchSimilarAsync(SnapshotId id, float[] queryVector, int topN, CancellationToken ct)
    {
        var session = _driver.AsyncSession();
        try
        {
            var results = new List<Node>();
            var records = await session.ExecuteReadAsync(
                async tx =>
                {
                    var cursor = await tx.RunAsync(
                        @"CALL db.index.vector.queryNodes('nodeEmbeddings', $topN, $queryVector) AS (node, score)
                          WHERE node.snapshotId = $snapshotId
                          RETURN node.id as id, node.label as label, node.fileType as fileType,
                                 node.sourceFile as sourceFile, node.sourceLocation as sourceLocation,
                                 node.community as community, node.normLabel as normLabel",
                        new { topN, queryVector, snapshotId = id.Id });
                    return await cursor.ToListAsync();
                });

            foreach (var record in records)
            {
                var node = new Node
                {
                    Id = (string)record["id"],
                    Label = (string)record["label"],
                    FileTypeString = (string)record["fileType"],
                    SourceFile = (string)record["sourceFile"],
                    SourceLocation = (string?)record["sourceLocation"],
                    Community = (int?)record["community"],
                    NormLabel = (string?)record["normLabel"]
                };
                results.Add(node);
            }

            return results;
        }
        finally
        {
            await session.CloseAsync();
        }
    }
}
