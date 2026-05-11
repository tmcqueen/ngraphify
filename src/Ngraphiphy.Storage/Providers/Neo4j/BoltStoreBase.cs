using Neo4j.Driver;
using Ngraphiphy.Models;
using Ngraphiphy.Pipeline;
using Ngraphiphy.Storage.Models;

namespace Ngraphiphy.Storage.Providers.Neo4j;

public abstract class BoltStoreBase : IGraphStore
{
    protected readonly IDriver _driver;
    protected readonly int _vectorDimensions;

    protected BoltStoreBase(IDriver driver, int vectorDimensions = 768)
    {
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        _vectorDimensions = vectorDimensions;
    }

    public async Task<bool> SnapshotExistsAsync(SnapshotId id, CancellationToken ct)
    {
        var session = _driver.AsyncSession();
        try
        {
            var result = await session.ExecuteReadAsync(
                async tx =>
                {
                    var cursor = await tx.RunAsync(
                        "MATCH (s:Snapshot {id: $id}) RETURN count(s) as count",
                        new { id = id.Id });
                    var record = await cursor.SingleAsync();
                    return (long)record["count"] > 0;
                });
            return result;
        }
        finally
        {
            await session.CloseAsync();
        }
    }

    public async Task SaveSnapshotAsync(RepositoryAnalysis analysis, SnapshotId id, CancellationToken ct)
    {
        var session = _driver.AsyncSession();
        try
        {
            _ = ct; // CancellationToken not supported by Neo4j driver session API
            await session.ExecuteWriteAsync(
                async tx =>
                {
                    var timestamp = DateTime.UtcNow;

                    // Create Snapshot node
                    await tx.RunAsync(
                        @"CREATE (s:Snapshot {
                            id: $id,
                            rootPath: $rootPath,
                            commitHash: $commitHash,
                            timestamp: $timestamp,
                            nodeCount: $nodeCount,
                            edgeCount: $edgeCount,
                            communityCount: 0
                          })",
                        new
                        {
                            id = id.Id,
                            rootPath = id.RootPath,
                            commitHash = id.CommitHash,
                            timestamp = timestamp.Ticks,
                            nodeCount = analysis.Graph.VertexCount,
                            edgeCount = analysis.Graph.EdgeCount,
                        });

                    // Create GraphNode nodes
                    foreach (var node in analysis.Graph.Vertices)
                    {
                        await tx.RunAsync(
                            @"CREATE (n:GraphNode {
                                id: $id,
                                snapshotId: $snapshotId,
                                label: $label,
                                fileType: $fileType,
                                sourceFile: $sourceFile,
                                sourceLocation: $sourceLocation,
                                community: $community,
                                normLabel: $normLabel
                              })",
                            new
                            {
                                id = node.Id,
                                snapshotId = id.Id,
                                label = node.Label,
                                fileType = node.FileTypeString,
                                sourceFile = node.SourceFile,
                                sourceLocation = node.SourceLocation,
                                community = node.Community,
                                normLabel = node.NormLabel,
                            });
                    }

                    // Create EDGE relationships
                    foreach (var edge in analysis.Graph.Edges)
                    {
                        await tx.RunAsync(
                            @"MATCH (source:GraphNode {id: $sourceId, snapshotId: $snapshotId}),
                                    (target:GraphNode {id: $targetId, snapshotId: $snapshotId})
                              CREATE (source)-[e:EDGE {
                                relation: $relation,
                                weight: $weight
                              }]->(target)",
                            new
                            {
                                sourceId = edge.Source.Id,
                                targetId = edge.Target.Id,
                                snapshotId = id.Id,
                                relation = edge.Tag.Relation,
                                weight = edge.Tag.Weight,
                            });
                    }
                    return 0;
                });
        }
        finally
        {
            await session.CloseAsync();
        }
    }

    public async Task<IReadOnlyList<SnapshotInfo>> ListSnapshotsAsync(string rootPath, CancellationToken ct)
    {
        var session = _driver.AsyncSession();
        try
        {
            _ = ct; // CancellationToken not supported by Neo4j driver session API
            var results = new List<SnapshotInfo>();
            var records = await session.ExecuteReadAsync(
                async tx =>
                {
                    var cursor = await tx.RunAsync(
                        @"MATCH (s:Snapshot {rootPath: $rootPath})
                          ORDER BY s.timestamp DESC
                          RETURN s.id as id, s.commitHash as commitHash, s.timestamp as timestamp,
                                 s.nodeCount as nodeCount, s.edgeCount as edgeCount, s.communityCount as communityCount",
                        new { rootPath });
                    return await cursor.ToListAsync();
                });

            foreach (var record in records)
            {
                var snapshotId = new SnapshotId(rootPath, (string)record["commitHash"]);
                results.Add(new SnapshotInfo(
                    snapshotId,
                    new DateTime((long)record["timestamp"], DateTimeKind.Utc),
                    (int)(long)record["nodeCount"],
                    (int)(long)record["edgeCount"],
                    (int)(long)record["communityCount"]
                ));
            }

            return results;
        }
        finally
        {
            await session.CloseAsync();
        }
    }

    public async Task<IReadOnlyList<Node>> GetNeighborsAsync(string nodeId, int depth, CancellationToken ct)
    {
        var session = _driver.AsyncSession();
        try
        {
            _ = ct; // CancellationToken not supported by Neo4j driver session API
            var results = new List<Node>();
            var records = await session.ExecuteReadAsync(
                async tx =>
                {
                    var cursor = await tx.RunAsync(
                        @"MATCH (n:GraphNode {id: $nodeId})-[*1..$depth]->(neighbor:GraphNode)
                          RETURN DISTINCT neighbor.id as id, neighbor.label as label,
                                 neighbor.fileType as fileType, neighbor.sourceFile as sourceFile,
                                 neighbor.sourceLocation as sourceLocation, neighbor.community as community,
                                 neighbor.normLabel as normLabel",
                        new { nodeId, depth });
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

    public async Task SaveCommunitySummariesAsync(SnapshotId id, IReadOnlyList<CommunitySummary> summaries, CancellationToken ct)
    {
        var session = _driver.AsyncSession();
        try
        {
            _ = ct; // CancellationToken not supported by Neo4j driver session API
            await session.ExecuteWriteAsync(
                async tx =>
                {
                    foreach (var summary in summaries)
                    {
                        await tx.RunAsync(
                            @"CREATE (c:Community {
                                snapshotId: $snapshotId,
                                communityId: $communityId,
                                summary: $summary,
                                nodeCount: $nodeCount
                              })",
                            new
                            {
                                snapshotId = id.Id,
                                communityId = summary.CommunityId,
                                summary = summary.Summary,
                                nodeCount = summary.NodeCount,
                            });
                    }

                    // Update snapshot community count
                    await tx.RunAsync(
                        @"MATCH (s:Snapshot {id: $id})
                          SET s.communityCount = $count",
                        new { id = id.Id, count = summaries.Count });
                    return 0;
                });
        }
        finally
        {
            await session.CloseAsync();
        }
    }

    public async Task<IReadOnlyList<CommunitySummary>> GetCommunitySummariesAsync(SnapshotId id, CancellationToken ct)
    {
        var session = _driver.AsyncSession();
        try
        {
            _ = ct; // CancellationToken not supported by Neo4j driver session API
            var results = new List<CommunitySummary>();
            var records = await session.ExecuteReadAsync(
                async tx =>
                {
                    var cursor = await tx.RunAsync(
                        @"MATCH (c:Community {snapshotId: $snapshotId})
                          RETURN c.communityId as communityId, c.summary as summary,
                                 c.nodeCount as nodeCount",
                        new { snapshotId = id.Id });
                    return await cursor.ToListAsync();
                });

            foreach (var record in records)
            {
                results.Add(new CommunitySummary(
                    (int)(long)record["communityId"],
                    (string)record["summary"],
                    (int)(long)record["nodeCount"]
                ));
            }

            return results;
        }
        finally
        {
            await session.CloseAsync();
        }
    }

    public abstract Task EmbedNodesAsync(SnapshotId id, IEmbeddingProvider embedder, CancellationToken ct);
    public abstract Task<IReadOnlyList<Node>> SearchSimilarAsync(SnapshotId id, float[] queryVector, int topN, CancellationToken ct);

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await _driver.DisposeAsync();
    }
}
