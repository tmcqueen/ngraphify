using Ngraphiphy.Storage.Providers.Neo4j;
using Ngraphiphy.Storage.Providers.Memgraph;

namespace Ngraphiphy.Storage;

public static class GraphStoreFactory
{
    public static async Task<IGraphStore> CreateAsync(
        IGraphStoreConfig config,
        int vectorDimensions = 768,
        CancellationToken ct = default)
    {
        return config switch
        {
            Neo4jConfig neo4j => await Neo4jStore.CreateAsync(neo4j, vectorDimensions, ct),
            MemgraphConfig mem => await MemgraphStore.CreateAsync(mem, vectorDimensions, ct),
            _ => throw new ArgumentException($"Unknown graph store config: {config.GetType().Name}", nameof(config))
        };
    }
}
