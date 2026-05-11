namespace Graphiphy.Storage.Models;

public sealed record CommunitySummary(
    int CommunityId,
    string Summary,
    int NodeCount);
