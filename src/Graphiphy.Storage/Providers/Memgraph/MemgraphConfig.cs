namespace Graphiphy.Storage.Providers.Memgraph;

public sealed record MemgraphConfig(
    string Host = "localhost",
    int Port = 7687,
    string Username = "",
    string Password = "") : IGraphStoreConfig
{
    public string Uri => $"bolt://{Host}:{Port}";
}
