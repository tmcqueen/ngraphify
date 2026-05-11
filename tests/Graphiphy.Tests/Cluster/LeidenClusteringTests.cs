using Graphiphy.Cluster;

namespace Graphiphy.Tests.Cluster;

public class LeidenClusteringTests
{
    [Test]
    public async Task Triangle_AllSameCommunity()
    {
        var edges = new (int, int)[] { (0, 1), (1, 2), (2, 0) };

        var result = LeidenClustering.FindCommunities(3, edges, PartitionType.Modularity);

        await Assert.That(result.Membership.Length).IsEqualTo(3);
        await Assert.That(result.Membership[0]).IsEqualTo(result.Membership[1]);
        await Assert.That(result.Membership[1]).IsEqualTo(result.Membership[2]);
    }

    [Test]
    public async Task TwoClusters_FindsTwo()
    {
        var edges = new (int, int)[]
        {
            (0, 1), (0, 2), (0, 3), (1, 2), (1, 3), (2, 3), // clique A
            (4, 5), (4, 6), (4, 7), (5, 6), (5, 7), (6, 7), // clique B
            (3, 4), // bridge
        };

        var result = LeidenClustering.FindCommunities(8, edges, PartitionType.CPM, resolution: 0.5, seed: 42);

        await Assert.That(result.NumCommunities).IsEqualTo(2);
        await Assert.That(result.Membership[0]).IsEqualTo(result.Membership[1]);
        await Assert.That(result.Membership[4]).IsEqualTo(result.Membership[5]);
        await Assert.That(result.Membership[0]).IsNotEqualTo(result.Membership[4]);
    }

    [Test]
    public async Task Deterministic_WithSeed()
    {
        var edges = new (int, int)[] { (0, 1), (1, 2), (2, 3), (3, 0), (0, 2) };

        var r1 = LeidenClustering.FindCommunities(4, edges, PartitionType.Modularity, seed: 99);
        var r2 = LeidenClustering.FindCommunities(4, edges, PartitionType.Modularity, seed: 99);

        await Assert.That(r1.Membership).IsEquivalentTo(r2.Membership);
    }
}
