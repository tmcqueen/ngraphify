using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;
using Ngraphiphy.Pipeline;

namespace Ngraphiphy.Cli.Mcp;

public static class GraphMcpServer
{
    /// <summary>
    /// Run an MCP server over stdio for the given repository analysis.
    /// Blocks until the MCP client disconnects (stdin closes).
    /// </summary>
    public static async Task RunAsync(RepositoryAnalysis analysis, CancellationToken ct = default)
    {
        var tools = new GraphTools(analysis);
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton(tools);
        builder.Services.AddMcpServer()
            .WithStdioServerTransport()
            .WithTools<McpGraphToolsWrapper>();
        await builder.Build().RunAsync(ct);
    }
}
