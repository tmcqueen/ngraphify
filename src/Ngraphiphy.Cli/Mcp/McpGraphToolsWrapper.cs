using System.ComponentModel;
using ModelContextProtocol.Server;
using Ngraphiphy.Pipeline;

namespace Ngraphiphy.Cli.Mcp;

[McpServerToolType]
internal sealed class McpGraphToolsWrapper(GraphTools tools)
{
    [McpServerTool(Name = "get_god_nodes")]
    [Description("Return the most connected nodes (god nodes) in the repository graph as JSON.")]
    public string GetGodNodes([Description("Maximum nodes to return (default 5)")] int topN = 5)
        => tools.GetGodNodes(topN);

    [McpServerTool(Name = "get_surprising_connections")]
    [Description("Return the most surprising cross-file or ambiguous edges as JSON.")]
    public string GetSurprisingConnections([Description("Maximum connections (default 10)")] int topN = 10)
        => tools.GetSurprisingConnections(topN);

    [McpServerTool(Name = "get_summary_stats")]
    [Description("Return summary statistics: node count, edge count, communities, top files.")]
    public string GetSummaryStats() => tools.GetSummaryStats();

    [McpServerTool(Name = "search_nodes")]
    [Description("Search for nodes whose label contains the query string. Returns JSON.")]
    public string SearchNodes(
        [Description("Search term")] string query,
        [Description("Maximum results (default 20)")] int limit = 20)
        => tools.SearchNodes(query, limit);

    [McpServerTool(Name = "get_report")]
    [Description("Return the full Markdown analysis report for the repository.")]
    public string GetReport() => tools.GetReport();
}
