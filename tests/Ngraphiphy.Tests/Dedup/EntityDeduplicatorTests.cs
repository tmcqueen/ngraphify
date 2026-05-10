using Ngraphiphy.Dedup;
using Ngraphiphy.Models;

namespace Ngraphiphy.Tests.Dedup;

public class EntityDeduplicatorTests
{
    [Test]
    public async Task ExactDuplicates_AreMerged()
    {
        var nodes = new List<Node>
        {
            new() { Id = "a::Foo", Label = "Foo", FileTypeString = "code", SourceFile = "a.py" },
            new() { Id = "b::foo", Label = "foo", FileTypeString = "code", SourceFile = "b.py" },
        };
        var edges = new List<Edge>
        {
            new() { Source = "a::Foo", Target = "b::foo", Relation = "calls", ConfidenceString = "EXTRACTED", SourceFile = "a.py" }
        };

        var (dedupNodes, dedupEdges) = EntityDeduplicator.Deduplicate(nodes, edges);

        await Assert.That(dedupNodes.Count).IsEqualTo(1);
    }

    [Test]
    public async Task LowEntropy_NotDeduped()
    {
        // Short single-character labels have low entropy — skip MinHash comparison
        var nodes = new List<Node>
        {
            new() { Id = "a::x", Label = "x", FileTypeString = "code", SourceFile = "a.py" },
            new() { Id = "b::x", Label = "x", FileTypeString = "code", SourceFile = "b.py" },
        };
        var edges = new List<Edge>();

        var (dedupNodes, _) = EntityDeduplicator.Deduplicate(nodes, edges);

        // Low entropy labels still get exact-match merged
        await Assert.That(dedupNodes.Count).IsEqualTo(1);
    }

    [Test]
    public async Task TypoMerging_JaroWinklerAboveThreshold()
    {
        var nodes = new List<Node>
        {
            new() { Id = "a::UserRepository", Label = "UserRepository", FileTypeString = "code", SourceFile = "a.py" },
            new() { Id = "b::UserRepostory", Label = "UserRepostory", FileTypeString = "code", SourceFile = "b.py" },
        };
        var edges = new List<Edge>();

        var (dedupNodes, _) = EntityDeduplicator.Deduplicate(nodes, edges);

        await Assert.That(dedupNodes.Count).IsEqualTo(1);
    }

    [Test]
    public async Task DifferentEntities_NotMerged()
    {
        var nodes = new List<Node>
        {
            new() { Id = "a::UserService", Label = "UserService", FileTypeString = "code", SourceFile = "a.py" },
            new() { Id = "b::OrderService", Label = "OrderService", FileTypeString = "code", SourceFile = "b.py" },
        };
        var edges = new List<Edge>();

        var (dedupNodes, _) = EntityDeduplicator.Deduplicate(nodes, edges);

        await Assert.That(dedupNodes.Count).IsEqualTo(2);
    }

    [Test]
    public async Task EdgeRewiring_AfterMerge()
    {
        var nodes = new List<Node>
        {
            new() { Id = "a::Foo", Label = "Foo", FileTypeString = "code", SourceFile = "a.py" },
            new() { Id = "b::foo", Label = "foo", FileTypeString = "code", SourceFile = "b.py" },
            new() { Id = "c::Bar", Label = "Bar", FileTypeString = "code", SourceFile = "c.py" },
        };
        var edges = new List<Edge>
        {
            new() { Source = "b::foo", Target = "c::Bar", Relation = "calls", ConfidenceString = "EXTRACTED", SourceFile = "b.py" }
        };

        var (dedupNodes, dedupEdges) = EntityDeduplicator.Deduplicate(nodes, edges);

        await Assert.That(dedupNodes.Count).IsEqualTo(2);
        await Assert.That(dedupEdges.Count).IsEqualTo(1);
        // Edge should be rewired to the winner node
        await Assert.That(dedupEdges[0].Source).IsEqualTo("a::Foo");
    }

    [Test]
    public async Task SelfLoops_DroppedAfterMerge()
    {
        var nodes = new List<Node>
        {
            new() { Id = "a::Foo", Label = "Foo", FileTypeString = "code", SourceFile = "a.py" },
            new() { Id = "b::foo", Label = "foo", FileTypeString = "code", SourceFile = "b.py" },
        };
        var edges = new List<Edge>
        {
            new() { Source = "a::Foo", Target = "b::foo", Relation = "calls", ConfidenceString = "EXTRACTED", SourceFile = "a.py" }
        };

        var (_, dedupEdges) = EntityDeduplicator.Deduplicate(nodes, edges);

        // After merge, source==target → self-loop → dropped
        await Assert.That(dedupEdges.Count).IsEqualTo(0);
    }
}
