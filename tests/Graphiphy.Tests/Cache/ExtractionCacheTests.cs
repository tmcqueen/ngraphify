using Graphiphy.Cache;
using Graphiphy.Models;
using ExtractionModel = Graphiphy.Models.Extraction;

namespace Graphiphy.Tests.Cache;

public class ExtractionCacheTests
{
    [Test]
    public async Task FileHash_IsDeterministic()
    {
        var dir = CreateTempDir();
        var file = Path.Combine(dir, "test.py");
        File.WriteAllText(file, "class Foo: pass");

        var hash1 = ExtractionCache.FileHash(file, dir);
        var hash2 = ExtractionCache.FileHash(file, dir);

        await Assert.That(hash1).IsEqualTo(hash2);
    }

    [Test]
    public async Task FileHash_ChangesWhenContentChanges()
    {
        var dir = CreateTempDir();
        var file = Path.Combine(dir, "test.py");

        File.WriteAllText(file, "class Foo: pass");
        var hash1 = ExtractionCache.FileHash(file, dir);

        File.WriteAllText(file, "class Bar: pass");
        var hash2 = ExtractionCache.FileHash(file, dir);

        await Assert.That(hash1).IsNotEqualTo(hash2);
    }

    [Test]
    public async Task CacheRoundtrip_SaveAndLoad()
    {
        var dir = CreateTempDir();
        var cacheDir = Path.Combine(dir, "cache");
        var file = Path.Combine(dir, "test.py");
        File.WriteAllText(file, "class Foo: pass");

        var extraction = new ExtractionModel
        {
            Nodes = [new Node { Id = "x::Foo", Label = "Foo", FileTypeString = "code", SourceFile = "test.py" }],
            Edges = []
        };

        var cache = new ExtractionCache(cacheDir);
        var hash = ExtractionCache.FileHash(file, dir);
        cache.Save(hash, extraction);

        var loaded = cache.Load(hash);

        await Assert.That(loaded).IsNotNull();
        await Assert.That(loaded!.Nodes.Count).IsEqualTo(1);
        await Assert.That(loaded.Nodes[0].Label).IsEqualTo("Foo");
    }

    [Test]
    public async Task Load_ReturnsNullForMissingHash()
    {
        var dir = CreateTempDir();
        var cache = new ExtractionCache(Path.Combine(dir, "cache"));

        var result = cache.Load("nonexistent_hash");

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task MarkdownFrontmatter_IgnoredInHash()
    {
        var dir = CreateTempDir();
        var file = Path.Combine(dir, "doc.md");

        File.WriteAllText(file, "---\ntitle: V1\n---\n# Hello\nBody text");
        var hash1 = ExtractionCache.FileHash(file, dir);

        File.WriteAllText(file, "---\ntitle: V2\ndate: today\n---\n# Hello\nBody text");
        var hash2 = ExtractionCache.FileHash(file, dir);

        await Assert.That(hash1).IsEqualTo(hash2);
    }

    [Test]
    public async Task MarkdownBody_ChangesHash()
    {
        var dir = CreateTempDir();
        var file = Path.Combine(dir, "doc.md");

        File.WriteAllText(file, "---\ntitle: X\n---\n# Hello\nBody text");
        var hash1 = ExtractionCache.FileHash(file, dir);

        File.WriteAllText(file, "---\ntitle: X\n---\n# Hello\nDifferent body");
        var hash2 = ExtractionCache.FileHash(file, dir);

        await Assert.That(hash1).IsNotEqualTo(hash2);
    }

    private static string CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "graphiphy_cache_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(path);
        return path;
    }
}
