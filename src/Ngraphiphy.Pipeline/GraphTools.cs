using System.Text.Json;
using Ngraphiphy.Analysis;
using Ngraphiphy.Models;
using QuikGraph;

namespace Ngraphiphy.Pipeline;

/// <summary>
/// Pure graph query logic with no MCP/HTTP dependency.
/// Used by MCP wrapper in Ngraphiphy.Cli and by tests in Ngraphiphy.Cli.Tests.
/// </summary>
public sealed class GraphTools
{
    private readonly RepositoryAnalysis _analysis;

    public GraphTools(RepositoryAnalysis analysis) => _analysis = analysis;

    public string GetGodNodes(int topN = 5)
    {
        var graph = _analysis.Graph;
        if (graph.VertexCount == 0) return "[]";
        return JsonSerializer.Serialize(
            GraphAnalyzer.GodNodes(graph, topN).Select(n => new
            {
                n.Id, n.Label, n.SourceFile,
                Connections = graph.InDegree(n) + graph.OutDegree(n),
            }));
    }

    public string GetSurprisingConnections(int topN = 10)
        => JsonSerializer.Serialize(
            GraphAnalyzer.SurprisingConnections(_analysis.Graph, topN).Select(c => new
            {
                Source = c.Source.Label,
                Target = c.Target.Label,
                c.Edge.Relation,
                c.Edge.ConfidenceString,
                c.Score,
            }));

    public string GetSummaryStats()
    {
        var graph = _analysis.Graph;
        var byFile = graph.Vertices
            .GroupBy(n => n.SourceFile)
            .Select(g => new { File = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count).Take(10);
        var communities = graph.Vertices
            .Where(n => n.Community.HasValue)
            .Select(n => n.Community!.Value).Distinct().Count();
        return JsonSerializer.Serialize(new
        {
            NodeCount = graph.VertexCount,
            EdgeCount = graph.EdgeCount,
            Communities = communities,
            TopFiles = byFile,
        });
    }

    public string SearchNodes(string query, int limit = 20)
        => JsonSerializer.Serialize(
            _analysis.Graph.Vertices
                .Where(n => n.Label.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Take(limit)
                .Select(n => new { n.Id, n.Label, n.SourceFile, n.FileTypeString }));

    public string GetReport() => _analysis.Report;
}
