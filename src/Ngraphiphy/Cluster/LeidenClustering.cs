namespace Ngraphiphy.Cluster;

public enum PartitionType
{
    Modularity = 0,
    CPM = 1,
}

public sealed class CommunityResult
{
    public int[] Membership { get; }
    public int NumCommunities { get; }

    public CommunityResult(int[] membership)
    {
        Membership = membership;
        NumCommunities = membership.Length > 0 ? membership.Max() + 1 : 0;
    }
}

public static class LeidenClustering
{
    static LeidenClustering()
    {
        NativeLibraryResolver.Register();
    }

    public static CommunityResult FindCommunities(
        int nNodes,
        IEnumerable<(int From, int To)> edges,
        PartitionType partitionType,
        double resolution = 1.0,
        int seed = -1)
    {
        var edgeList = edges.ToList();
        var fromArr = edgeList.Select(e => e.From).ToArray();
        var toArr = edgeList.Select(e => e.To).ToArray();
        var membership = new int[nNodes];

        var numCommunities = NativeMethods.FindCommunities(
            nNodes,
            edgeList.Count,
            fromArr,
            toArr,
            (int)partitionType,
            resolution,
            seed,
            membership);

        if (numCommunities < 0)
            throw new InvalidOperationException("Leiden algorithm failed (native returned -1)");

        return new CommunityResult(membership);
    }
}
