namespace Graphiphy.Storage.Models;

public sealed record SnapshotInfo(
    SnapshotId Id,
    DateTime Timestamp,
    int NodeCount,
    int EdgeCount,
    int CommunityCount);
