using Ngraphiphy.Build;
using Ngraphiphy.Cache;
using Ngraphiphy.Cluster;
using Ngraphiphy.Dedup;
using Ngraphiphy.Detection;
using Ngraphiphy.Extraction;
using Ngraphiphy.Models;
using Ngraphiphy.Report;
using Ngraphiphy.Validation;
using QuikGraph;
using ExtractionModel = Ngraphiphy.Models.Extraction;

namespace Ngraphiphy.Pipeline;

public sealed class RepositoryAnalysis
{
    public string RootPath { get; }
    public List<DetectedFile> Files { get; }
    public BidirectionalGraph<Node, TaggedEdge<Node, Edge>> Graph { get; }
    public string Report { get; }

    private RepositoryAnalysis(string rootPath, List<DetectedFile> files,
        BidirectionalGraph<Node, TaggedEdge<Node, Edge>> graph, string report)
    {
        RootPath = rootPath; Files = files; Graph = graph; Report = report;
    }

    public static async Task<RepositoryAnalysis> RunAsync(
        string rootPath,
        string? cacheDir = null,
        Action<string>? onProgress = null,
        CancellationToken ct = default)
    {
        cacheDir ??= Path.Combine(rootPath, ".ngraphiphy-cache");
        var cache = new ExtractionCache(cacheDir);
        var registry = LanguageRegistry.CreateDefault();

        onProgress?.Invoke("Detecting files...");
        var files = FileDetector.Detect(rootPath);

        onProgress?.Invoke($"Extracting {files.Count} files...");
        var extractions = new List<ExtractionModel>();
        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            var extractor = registry.GetExtractor(file.AbsolutePath);
            if (extractor is null) continue;

            var source = await File.ReadAllTextAsync(file.AbsolutePath, ct);
            var hash = ExtractionCache.FileHash(file.AbsolutePath, rootPath, source);
            var cached = cache.Load(hash);
            if (cached is not null) { extractions.Add(cached); continue; }
            var extraction = extractor.Extract(file.AbsolutePath, source);
            var validation = ExtractionValidator.Validate(extraction);
            foreach (var warning in validation.Warnings)
                onProgress?.Invoke($"Warning [{file.AbsolutePath}]: {warning}");
            if (validation.Errors.Count > 0)
                throw new InvalidOperationException(
                    $"Invalid extraction for {file.AbsolutePath} ({validation.Errors.Count} errors):\n"
                    + string.Join("\n", validation.Errors));
            cache.Save(hash, extraction);
            extractions.Add(extraction);
        }

        onProgress?.Invoke("Building graph...");
        var rawGraph = GraphBuilder.Build(extractions);

        onProgress?.Invoke("Deduplicating entities...");
        var (dedupNodes, dedupEdges) = EntityDeduplicator.Deduplicate(
            rawGraph.Vertices.ToList(),
            rawGraph.Edges.Select(e => e.Tag).ToList());
        var graph = GraphBuilder.FromGraphData(new GraphData { Nodes = dedupNodes, Edges = dedupEdges });

        try
        {
            onProgress?.Invoke("Clustering communities...");
            var nodeList = graph.Vertices.ToList();
            if (nodeList.Count > 0)
            {
                var nodeIndex = nodeList.Select((n, i) => (n, i)).ToDictionary(x => x.n, x => x.i);
                var edgeTuples = graph.Edges.Select(e => (nodeIndex[e.Source], nodeIndex[e.Target]));
                var communities = LeidenClustering.FindCommunities(
                    nodeList.Count, edgeTuples, PartitionType.Modularity, seed: 42);
                for (int i = 0; i < nodeList.Count; i++)
                    nodeList[i].Community = communities.Membership[i];
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or InvalidOperationException)
        {
            onProgress?.Invoke($"Warning: clustering skipped — {ex.GetType().Name}: {ex.Message}");
        }

        onProgress?.Invoke("Generating report...");
        var report = ReportGenerator.Generate(graph);
        return new RepositoryAnalysis(rootPath, files, graph, report);
    }
}
