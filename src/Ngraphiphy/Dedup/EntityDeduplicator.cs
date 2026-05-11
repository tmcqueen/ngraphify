using F23.StringSimilarity;
using MinHashSharp;
using Ngraphiphy.Models;

namespace Ngraphiphy.Dedup;

public static class EntityDeduplicator
{
    private const double EntropyThreshold = 2.5;
    private const double LshThreshold = 0.5;
    private const double MergeThreshold = 0.92;
    private const int ShingleK = 3;
    private const int NumPerm = 128;

    public static (List<Node> Nodes, List<Edge> Edges) Deduplicate(List<Node> nodes, List<Edge> edges)
    {
        var uf = new UnionFind();

        // Index nodes by normalized label
        var byNorm = new Dictionary<string, List<Node>>();
        foreach (var node in nodes)
        {
            var norm = Normalize(node.Label);
            if (!byNorm.TryGetValue(norm, out var list))
            {
                list = [];
                byNorm[norm] = list;
            }
            list.Add(node);
        }

        // Pass 1: Exact normalization merging
        foreach (var (_, group) in byNorm)
        {
            if (group.Count < 2) continue;
            var winner = PickWinner(group);
            foreach (var n in group)
                uf.Union(winner.Id, n.Id);
        }

        // Build O(1) lookup index
        var nodesById = nodes.ToDictionary(n => n.Id);

        // Pass 2: MinHash/LSH + Jaro-Winkler for high-entropy labels
        var highEntropy = nodes.Where(n => Entropy(n.Label) >= EntropyThreshold).ToList();
        if (highEntropy.Count > 1)
        {
            // MinHashLSH constructor: (double threshold, int numPerm)
            var lsh = new MinHashLSH(LshThreshold, NumPerm);
            var minhashes = new Dictionary<string, MinHash>();

            foreach (var node in highEntropy)
            {
                var shingles = Shingles(node.Label);
                // MinHash constructor: (int numPerm, int seed, Func<string, uint> hashFunc)
                var mh = new MinHash(NumPerm, 42, s =>
                {
                    uint hash = 2166136261u;
                    foreach (var c in s)
                    {
                        hash ^= c;
                        hash *= 16777619u;
                    }
                    return hash;
                });
                // MinHash.Update takes string[]
                if (shingles.Count > 0)
                    mh.Update(shingles.ToArray());
                minhashes[node.Id] = mh;
                lsh.Insert(node.Id, mh);
            }

            var jw = new JaroWinkler();
            var checked_ = new HashSet<(string, string)>();

            foreach (var node in highEntropy)
            {
                var candidates = lsh.Query(minhashes[node.Id]).Distinct();
                foreach (var candidateId in candidates)
                {
                    if (candidateId == node.Id) continue;
                    var pair = (string.Compare(node.Id, candidateId, StringComparison.Ordinal) < 0)
                        ? (node.Id, candidateId) : (candidateId, node.Id);
                    if (!checked_.Add(pair)) continue;

                    var other = nodesById[candidateId];
                    var similarity = jw.Similarity(Normalize(node.Label), Normalize(other.Label));
                    if (similarity >= MergeThreshold)
                        uf.Union(node.Id, candidateId);
                }
            }
        }

        // Build remap table
        var remap = new Dictionary<string, string>();
        var groups = uf.Groups();
        var keptNodes = new Dictionary<string, Node>();

        foreach (var (root, members) in groups)
        {
            var memberNodes = members.Select(id => nodesById[id]).ToList();
            var winner = PickWinner(memberNodes);
            foreach (var id in members)
                remap[id] = winner.Id;
            keptNodes[winner.Id] = winner;
        }

        // Add nodes that were never in the UF (shouldn't happen, but safe)
        foreach (var node in nodes)
        {
            if (!remap.ContainsKey(node.Id))
            {
                remap[node.Id] = node.Id;
                keptNodes[node.Id] = node;
            }
        }

        // Rewire edges
        var dedupEdges = new List<Edge>();
        foreach (var edge in edges)
        {
            var newSource = remap.GetValueOrDefault(edge.Source, edge.Source);
            var newTarget = remap.GetValueOrDefault(edge.Target, edge.Target);

            // Drop self-loops
            if (newSource == newTarget) continue;

            dedupEdges.Add(new Edge
            {
                Source = newSource,
                Target = newTarget,
                Relation = edge.Relation,
                ConfidenceString = edge.ConfidenceString,
                SourceFile = edge.SourceFile,
                SourceLocation = edge.SourceLocation,
                Weight = edge.Weight,
                Context = edge.Context,
                ConfidenceScore = edge.ConfidenceScore,
            });
        }

        return (keptNodes.Values.ToList(), dedupEdges);
    }

    private static string Normalize(string label)
    {
        var chars = label.ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : ' ')
            .ToArray();
        return new string(chars).Trim();
    }

    private static double Entropy(string s)
    {
        if (s.Length == 0) return 0;
        var freq = new Dictionary<char, int>();
        foreach (var c in s)
            freq[c] = freq.GetValueOrDefault(c) + 1;

        double entropy = 0;
        foreach (var count in freq.Values)
        {
            double p = (double)count / s.Length;
            entropy -= p * Math.Log2(p);
        }
        return entropy;
    }

    private static HashSet<string> Shingles(string label)
    {
        var norm = Normalize(label);
        var result = new HashSet<string>();
        for (int i = 0; i <= norm.Length - ShingleK; i++)
            result.Add(norm.Substring(i, ShingleK));
        return result;
    }

    private static Node PickWinner(IReadOnlyList<Node> candidates)
    {
        // Prefer: no chunk suffix, shorter ID, alphabetical
        return candidates
            .OrderBy(n => n.Id.Contains("__chunk") ? 1 : 0)
            .ThenBy(n => n.Id.Length)
            .ThenBy(n => n.Id, StringComparer.Ordinal)
            .First();
    }
}
