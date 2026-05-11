using Graphiphy.Models;
using Graphiphy.Pipeline;
using Graphiphy.Storage.Models;

namespace Graphiphy.Storage;

public interface IGraphStore : IAsyncDisposable
{
    Task<bool> SnapshotExistsAsync(SnapshotId id, CancellationToken ct);
    Task SaveSnapshotAsync(RepositoryAnalysis analysis, SnapshotId id, CancellationToken ct);
    Task<IReadOnlyList<SnapshotInfo>> ListSnapshotsAsync(string rootPath, CancellationToken ct);

    Task EmbedNodesAsync(SnapshotId id, IEmbeddingProvider embedder, CancellationToken ct);

    Task<IReadOnlyList<Node>> SearchSimilarAsync(SnapshotId id, float[] queryVector, int topN, CancellationToken ct);
    Task<IReadOnlyList<Node>> GetNeighborsAsync(string nodeId, int depth, CancellationToken ct);

    Task SaveCommunitySummariesAsync(SnapshotId id, IReadOnlyList<CommunitySummary> summaries, CancellationToken ct);
    Task<IReadOnlyList<CommunitySummary>> GetCommunitySummariesAsync(SnapshotId id, CancellationToken ct);
}
