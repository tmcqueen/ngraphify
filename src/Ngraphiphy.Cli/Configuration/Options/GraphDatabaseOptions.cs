namespace Ngraphiphy.Cli.Configuration.Options;

public sealed class GraphDatabaseOptions
{
    public string Backend { get; set; } = "neo4j";
    public Neo4jDatabaseOptions Neo4j { get; set; } = new();
    public MemgraphDatabaseOptions Memgraph { get; set; } = new();
}

public sealed class Neo4jDatabaseOptions
{
    public string Uri { get; set; } = "bolt://localhost:7687";
    public string Username { get; set; } = "neo4j";
    public string Password { get; set; } = "";
}

public sealed class MemgraphDatabaseOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 7687;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}
