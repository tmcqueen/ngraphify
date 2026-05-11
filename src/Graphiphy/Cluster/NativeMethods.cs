using System.Runtime.InteropServices;

namespace Graphiphy.Cluster;

internal static partial class NativeMethods
{
    private const string LibName = "leiden_interop";

    [LibraryImport(LibName, EntryPoint = "leiden_find_communities")]
    internal static partial int FindCommunities(
        int nNodes,
        int nEdges,
        [In] int[] edgeFrom,
        [In] int[] edgeTo,
        int partitionType,
        double resolution,
        int seed,
        [Out] int[] membershipOut);
}
