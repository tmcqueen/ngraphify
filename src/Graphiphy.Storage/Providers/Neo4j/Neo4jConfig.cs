namespace Graphiphy.Storage.Providers.Neo4j;

public sealed record Neo4jConfig(
    string Uri = "bolt://localhost:7687",
    string Username = "neo4j",
    string Password = "") : IGraphStoreConfig;
