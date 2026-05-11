namespace Graphiphy.Dedup;

internal sealed class UnionFind
{
    private readonly Dictionary<string, string> _parent = [];
    private readonly Dictionary<string, int> _rank = [];

    public string Find(string x)
    {
        if (!_parent.ContainsKey(x))
        {
            _parent[x] = x;
            _rank[x] = 0;
        }

        if (_parent[x] != x)
            _parent[x] = Find(_parent[x]); // path compression

        return _parent[x];
    }

    public void Union(string x, string y)
    {
        var rx = Find(x);
        var ry = Find(y);
        if (rx == ry) return;

        // Union by rank
        if (_rank[rx] < _rank[ry])
            _parent[rx] = ry;
        else if (_rank[rx] > _rank[ry])
            _parent[ry] = rx;
        else
        {
            _parent[ry] = rx;
            _rank[rx]++;
        }
    }

    public Dictionary<string, List<string>> Groups()
    {
        var groups = new Dictionary<string, List<string>>();
        foreach (var key in _parent.Keys)
        {
            var root = Find(key);
            if (!groups.TryGetValue(root, out var list))
            {
                list = [];
                groups[root] = list;
            }
            list.Add(key);
        }
        return groups;
    }
}
