# Graph Database Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add persistent graph storage (Neo4j/Memgraph) with vector embeddings (Cloudflare Workers AI) and GraphRAG support via a new `Graphiphy.Storage` project and `graphiphy-cli push` command.

**Architecture:** New `Graphiphy.Storage` project follows the provider pattern from `Graphiphy.Llm`: marker interface + sealed record configs + switch-on-type factory + unified interface. Neo4j and Memgraph share the Bolt protocol driver but override vector-search Cypher. Embeddings are abstracted separately via `IEmbeddingProvider` (Cloudflare API via OpenAI SDK). Snapshots are keyed by git commit hash (via LibGit2Sharp) for idempotency.

**Tech Stack:** Neo4j.Driver 5.x, LibGit2Sharp 0.30.x, OpenAI 2.x, Spectre.Console.Cli, TUnit 1.43.41

---

## File Structure

**New project:** `src/Graphiphy.Storage/`
- Core: `IGraphStoreConfig.cs`, `IGraphStore.cs`, `GraphStoreFactory.cs`
- Models: `SnapshotId.cs`, `SnapshotInfo.cs`, `CommunitySummary.cs`
- Providers: `Providers/Neo4j/`, `Providers/Memgraph/` (shared base `BoltStoreBase.cs`)
- Embedding: `Embedding/IEmbeddingProvider.cs`, `Embedding/CloudflareEmbeddingConfig.cs`, `Embedding/CloudflareEmbeddingProvider.cs`

**New CLI command:** `src/Graphiphy.Cli/Commands/PushCommand.cs` (follows pattern of AnalyzeCommand, ReportCommand, etc.)

**Modifications:** `src/Graphiphy.Cli/Program.cs` (add push command), `Graphiphy.sln` (add Storage project)

**Tests:** `tests/Graphiphy.Storage.Tests/` with unit tests + integration tests (skipped without live DB)

---

## Task 1: Create Graphiphy.Storage Project & Dependencies

**Files:**
- Create: `src/Graphiphy.Storage/Graphiphy.Storage.csproj`
- Modify: `Graphiphy.sln`

- [ ] **Step 1: Create project file**

```bash
cd /home/timm/ngraphify
mkdir -p src/Graphiphy.Storage
cd src/Graphiphy.Storage
```

Create `Graphiphy.Storage.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Neo4j.Driver" Version="5.18.0" />
    <PackageReference Include="LibGit2Sharp" Version="0.29.0" />
    <PackageReference Include="OpenAI" Version="2.3.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../Graphiphy/Graphiphy.csproj" />
    <ProjectReference Include="../Graphiphy.Pipeline/Graphiphy.Pipeline.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Add project to solution**

Run:
```bash
dotnet sln /home/timm/ngraphify/Graphiphy.sln add src/Graphiphy.Storage/Graphiphy.Storage.csproj
```

Expected: Solution file updated with Storage project reference.

- [ ] **Step 3: Verify project loads**

```bash
cd /home/timm/ngraphify
dotnet build src/Graphiphy.Storage/ -v minimal 2>&1 | tail -5
```

Expected: Build succeeds (no code yet, just structure).

- [ ] **Step 4: Commit**

```bash
git add src/Graphiphy.Storage/Graphiphy.Storage.csproj Graphiphy.sln
git commit -m "feat: create Graphiphy.Storage project with Neo4j, LibGit2Sharp, OpenAI packages"
```

---

## Task 2: Create Core Interfaces & Models

**Files:**
- Create: `src/Graphiphy.Storage/IGraphStoreConfig.cs`
- Create: `src/Graphiphy.Storage/IGraphStore.cs`
- Create: `src/Graphiphy.Storage/Models/SnapshotId.cs`
- Create: `src/Graphiphy.Storage/Models/SnapshotInfo.cs`
- Create: `src/Graphiphy.Storage/Models/CommunitySummary.cs`
- Test: `tests/Graphiphy.Storage.Tests/SnapshotIdTests.cs`

- [ ] **Step 1: Create IGraphStoreConfig marker interface**

`src/Graphiphy.Storage/IGraphStoreConfig.cs`:
```csharp
namespace Graphiphy.Storage;

public interface IGraphStoreConfig { }
```

- [ ] **Step 2: Create IGraphStore interface**

`src/Graphiphy.Storage/IGraphStore.cs`:
```csharp
using Graphiphy.Models;
using Graphiphy.Pipeline;

namespace Graphiphy.Storage;

public interface IGraphStore : IAsyncDisposable
{
    Task<bool> SnapshotExistsAsync(SnapshotId id, CancellationToken ct);
    Task SaveSnapshotAsync(RepositoryAnalysis analysis, SnapshotId id, CancellationToken ct);
    Task<IReadOnlyList<SnapshotInfo>> ListSnapshotsAsync(string rootPath, CancellationToken ct);

    Task EmbedNodesAsync(SnapshotId id, IEmbeddingProvider embedder, CancellationToken ct);

    Task<IReadOnlyList<Node>> SearchSimilarAsync(SnapshotId id, float[] queryVector, int topN, CancellationToken ct);
    Task<IReadOnlyList<Node>> GetNeighborsAsync(string nodeId, int depth, CancellationToken ct);

    Task SaveCommunitySummariesAsync(SnapshotId id, IReadOnlyList<CommunitySummary> summaries, CancellationToken ct);
    Task<IReadOnlyList<CommunitySummary>> GetCommunitySummariesAsync(SnapshotId id, CancellationToken ct);
}
```

- [ ] **Step 3: Create SnapshotId model**

`src/Graphiphy.Storage/Models/SnapshotId.cs`:
```csharp
using LibGit2Sharp;

namespace Graphiphy.Storage.Models;

public sealed record SnapshotId(string RootPath, string CommitHash)
{
    public string Id => $"{RootPath}::{CommitHash}";

    public static SnapshotId Resolve(string rootPath)
    {
        try
        {
            using var repo = new Repository(rootPath);
            var commitHash = repo.Head.Tip.Sha;
            return new(rootPath, commitHash);
        }
        catch
        {
            // Not a git repo or error resolving — use content hash instead
            var contentHash = ComputeContentHash(rootPath);
            return new(rootPath, contentHash);
        }
    }

    private static string ComputeContentHash(string rootPath)
    {
        // Compute SHA256 of all .cs/.py/.js/.ts files in rootPath (sorted by name)
        using var hasher = System.Security.Cryptography.SHA256.Create();
        var files = Directory.GetFiles(rootPath, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".cs") || f.EndsWith(".py") || f.EndsWith(".js") || f.EndsWith(".ts"))
            .OrderBy(f => f)
            .ToList();

        foreach (var file in files)
        {
            hasher.TransformBlock(System.Text.Encoding.UTF8.GetBytes(file), 0, file.Length, null, 0);
            hasher.TransformBlock(File.ReadAllBytes(file), 0, (int)new FileInfo(file).Length, null, 0);
        }
        hasher.TransformFinalBlock([], 0, 0);

        return Convert.ToHexString(hasher.Hash!).ToLower()[..16]; // 16 chars
    }
}
```

- [ ] **Step 4: Create SnapshotInfo model**

`src/Graphiphy.Storage/Models/SnapshotInfo.cs`:
```csharp
namespace Graphiphy.Storage.Models;

public sealed record SnapshotInfo(
    SnapshotId Id,
    DateTime Timestamp,
    int NodeCount,
    int EdgeCount,
    int CommunityCount);
```

- [ ] **Step 5: Create CommunitySummary model**

`src/Graphiphy.Storage/Models/CommunitySummary.cs`:
```csharp
namespace Graphiphy.Storage.Models;

public sealed record CommunitySummary(
    int CommunityId,
    string Summary,
    int NodeCount);
```

- [ ] **Step 6: Write test for SnapshotId resolution**

`tests/Graphiphy.Storage.Tests/SnapshotIdTests.cs`:
```csharp
using Graphiphy.Storage.Models;

namespace Graphiphy.Storage.Tests;

public class SnapshotIdTests
{
    [Test]
    public async Task Resolve_InGitRepo_ReturnsCommitHash()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"graphiphy_snap_{Guid.NewGuid().ToString("N")[..8]}");
        Directory.CreateDirectory(tempDir);
        try
        {
            // Initialize git repo and create a commit
            LibGit2Sharp.Repository.Init(tempDir);
            using var repo = new LibGit2Sharp.Repository(tempDir);
            var identity = new LibGit2Sharp.Signature("Test User", "test@example.com", System.DateTimeOffset.UtcNow);
            File.WriteAllText(Path.Combine(tempDir, "test.cs"), "// test");
            LibGit2Sharp.Commands.Stage(repo, "test.cs");
            var commit = repo.Commit("initial commit", identity, identity);

            var snapshot = SnapshotId.Resolve(tempDir);

            await Assert.That(snapshot.CommitHash).IsEqualTo(commit.Sha);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Resolve_NotGitRepo_ReturnsContentHash()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"graphiphy_snap_{Guid.NewGuid().ToString("N")[..8]}");
        Directory.CreateDirectory(tempDir);
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "test.cs"), "// test");

            var snapshot = SnapshotId.Resolve(tempDir);

            await Assert.That(snapshot.CommitHash).IsNotNull().And.HasLength(16);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
```

- [ ] **Step 7: Run tests**

```bash
cd /home/timm/ngraphify
dotnet run --project tests/Graphiphy.Storage.Tests/ 2>&1 | grep -E "passed|failed|Passed"
```

Expected: 2 tests pass.

- [ ] **Step 8: Commit**

```bash
git add src/Graphiphy.Storage/IGraphStoreConfig.cs \
         src/Graphiphy.Storage/IGraphStore.cs \
         src/Graphiphy.Storage/Models/*.cs \
         tests/Graphiphy.Storage.Tests/SnapshotIdTests.cs
git commit -m "feat: add IGraphStore interface and snapshot models

- IGraphStoreConfig marker interface (follows IAgentConfig pattern)
- IGraphStore with lifecycle, embedding, and GraphRAG query methods
- SnapshotId: git-commit-keyed snapshots with fallback content hash
- SnapshotInfo, CommunitySummary DTOs
- SnapshotIdTests: validate git and non-git repo handling"
```

---

## Task 3: Create Neo4j Provider

**Files:**
- Create: `src/Graphiphy.Storage/Providers/Neo4j/Neo4jConfig.cs`
- Create: `src/Graphiphy.Storage/Providers/Neo4j/BoltStoreBase.cs`
- Create: `src/Graphiphy.Storage/Providers/Neo4j/Neo4jStore.cs`
- Test: `tests/Graphiphy.Storage.Tests/Neo4jStoreTests.cs`

- [ ] **Step 1: Create Neo4jConfig**

`src/Graphiphy.Storage/Providers/Neo4j/Neo4jConfig.cs`:
```csharp
namespace Graphiphy.Storage.Providers.Neo4j;

public sealed record Neo4jConfig(
    string Uri = "bolt://localhost:7687",
    string Username = "neo4j",
    string Password = "") : IGraphStoreConfig;
```

- [ ] **Step 2: Create BoltStoreBase (shared between Neo4j and Memgraph)**

`src/Graphiphy.Storage/Providers/Neo4j/BoltStoreBase.cs`:
```csharp
using Neo4j.Driver;
using Graphiphy.Models;
using Graphiphy.Pipeline;
using Graphiphy.Storage.Models;

namespace Graphiphy.Storage.Providers.Neo4j;

public abstract class BoltStoreBase : IGraphStore
{
    protected readonly IDriver _driver;
    protected readonly int _vectorDimensions;

    protected BoltStoreBase(IDriver driver, int vectorDimensions = 768)
    {
        _driver = driver;
        _vectorDimensions = vectorDimensions;
    }

    public async Task<bool> SnapshotExistsAsync(SnapshotId id, CancellationToken ct)
    {
        var session = _driver.AsyncSession();
        try
        {
            var result = await session.ExecuteReadAsync(
                tx => tx.RunAsync(
                    "MATCH (s:Snapshot {id: $id}) RETURN count(s) as cnt",
                    new { id = id.Id })
                    .Then(r => r.FetchAsync()),
                ct);
            return result.Count > 0;
        }
        finally
        {
            await session.CloseAsync();
        }
    }

    public async Task SaveSnapshotAsync(RepositoryAnalysis analysis, SnapshotId id, CancellationToken ct)
    {
        var session = _driver.AsyncSession();
        try
        {
            await session.ExecuteWriteAsync(async tx =>
            {
                // Create snapshot node
                await tx.RunAsync(
                    @"CREATE (s:Snapshot {
                        id: $id,
                        rootPath: $rootPath,
                        commitHash: $commitHash,
                        timestamp: datetime(),
                        nodeCount: $nodeCount,
                        edgeCount: $edgeCount
                      })",
                    new
                    {
                        id = id.Id,
                        rootPath = id.RootPath,
                        commitHash = id.CommitHash,
                        nodeCount = analysis.Graph.VertexCount,
                        edgeCount = analysis.Graph.EdgeCount
                    });

                // Create nodes
                foreach (var node in analysis.Graph.Vertices)
                {
                    await tx.RunAsync(
                        @"CREATE (n:GraphNode {
                            id: $id,
                            label: $label,
                            fileType: $fileType,
                            sourceFile: $sourceFile,
                            sourceLocation: $sourceLocation,
                            community: $community,
                            normLabel: $normLabel,
                            snapshotId: $snapshotId
                          })",
                        new
                        {
                            id = node.Id,
                            label = node.Label,
                            fileType = node.FileTypeString,
                            sourceFile = node.SourceFile,
                            sourceLocation = node.SourceLocation,
                            community = node.Community,
                            normLabel = node.NormLabel,
                            snapshotId = id.Id
                        });
                }

                // Create edges
                foreach (var edge in analysis.Graph.Edges)
                {
                    await tx.RunAsync(
                        @"MATCH (s:GraphNode {id: $source, snapshotId: $snapshotId})
                          MATCH (t:GraphNode {id: $target, snapshotId: $snapshotId})
                          CREATE (s)-[e:EDGE {
                            relation: $relation,
                            confidence: $confidence,
                            weight: $weight,
                            context: $context,
                            sourceFile: $sourceFile,
                            sourceLocation: $sourceLocation,
                            snapshotId: $snapshotId
                          }]->(t)",
                        new
                        {
                            source = edge.Source.Id,
                            target = edge.Target.Id,
                            relation = edge.Relation,
                            confidence = edge.ConfidenceString,
                            weight = edge.Weight,
                            context = edge.Context,
                            sourceFile = edge.SourceFile,
                            sourceLocation = edge.SourceLocation,
                            snapshotId = id.Id
                        });
                }
            }, ct);
        }
        finally
        {
            await session.CloseAsync();
        }
    }

    public async Task<IReadOnlyList<SnapshotInfo>> ListSnapshotsAsync(string rootPath, CancellationToken ct)
    {
        var session = _driver.AsyncSession();
        try
        {
            var results = new List<SnapshotInfo>();
            var records = await session.ExecuteReadAsync(
                tx => tx.RunAsync(
                    @"MATCH (s:Snapshot {rootPath: $rootPath})
                      RETURN s.id as id, s.timestamp as timestamp,
                             s.nodeCount as nodeCount, s.edgeCount as edgeCount
                      ORDER BY s.timestamp DESC",
                    new { rootPath })
                    .Then(r => r.ToListAsync()),
                ct);

            foreach (var record in records)
            {
                var id = (string)record["id"];
                var commitHash = id.Split("::")[1];
                results.Add(new(
                    new SnapshotId(rootPath, commitHash),
                    ((ZonedDateTime)record["timestamp"]).ToDateTimeOffset().DateTime,
                    (int)(long)record["nodeCount"],
                    (int)(long)record["edgeCount"],
                    0 // Community count not stored in snapshot node; would need separate query
                ));
            }

            return results;
        }
        finally
        {
            await session.CloseAsync();
        }
    }

    public virtual async Task EmbedNodesAsync(SnapshotId id, IEmbeddingProvider embedder, CancellationToken ct)
    {
        throw new NotImplementedException("Subclasses must implement vector embedding");
    }

    public virtual async Task<IReadOnlyList<Node>> SearchSimilarAsync(SnapshotId id, float[] queryVector, int topN, CancellationToken ct)
    {
        throw new NotImplementedException("Subclasses must implement vector search");
    }

    public async Task<IReadOnlyList<Node>> GetNeighborsAsync(string nodeId, int depth, CancellationToken ct)
    {
        var session = _driver.AsyncSession();
        try
        {
            var results = new List<Node>();
            var records = await session.ExecuteReadAsync(
                tx => tx.RunAsync(
                    @"MATCH (n:GraphNode {id: $id})-[*1.." + depth + @"]->(neighbor:GraphNode)
                      RETURN DISTINCT neighbor.id as id, neighbor.label as label,
                             neighbor.fileType as fileType, neighbor.sourceFile as sourceFile,
                             neighbor.sourceLocation as sourceLocation, neighbor.community as community,
                             neighbor.normLabel as normLabel",
                    new { id = nodeId })
                    .Then(r => r.ToListAsync()),
                ct);

            foreach (var record in records)
            {
                results.Add(new Node(
                    (string)record["id"],
                    (string)record["label"],
                    (string)record["fileType"],
                    (string)record["sourceFile"],
                    (string?)record["sourceLocation"],
                    (int?)record["community"],
                    (string?)record["normLabel"]
                ));
            }

            return results;
        }
        finally
        {
            await session.CloseAsync();
        }
    }

    public async Task SaveCommunitySummariesAsync(SnapshotId id, IReadOnlyList<CommunitySummary> summaries, CancellationToken ct)
    {
        var session = _driver.AsyncSession();
        try
        {
            await session.ExecuteWriteAsync(async tx =>
            {
                foreach (var summary in summaries)
                {
                    await tx.RunAsync(
                        @"CREATE (c:Community {
                            id: $id,
                            snapshotId: $snapshotId,
                            communityId: $communityId,
                            summary: $summary,
                            nodeCount: $nodeCount
                          })",
                        new
                        {
                            id = $"{id.Id}::{summary.CommunityId}",
                            snapshotId = id.Id,
                            communityId = summary.CommunityId,
                            summary = summary.Summary,
                            nodeCount = summary.NodeCount
                        });
                }
            }, ct);
        }
        finally
        {
            await session.CloseAsync();
        }
    }

    public async Task<IReadOnlyList<CommunitySummary>> GetCommunitySummariesAsync(SnapshotId id, CancellationToken ct)
    {
        var session = _driver.AsyncSession();
        try
        {
            var results = new List<CommunitySummary>();
            var records = await session.ExecuteReadAsync(
                tx => tx.RunAsync(
                    @"MATCH (c:Community {snapshotId: $snapshotId})
                      RETURN c.communityId as communityId, c.summary as summary, c.nodeCount as nodeCount
                      ORDER BY c.communityId",
                    new { snapshotId = id.Id })
                    .Then(r => r.ToListAsync()),
                ct);

            foreach (var record in records)
            {
                results.Add(new(
                    (int)(long)record["communityId"],
                    (string?)record["summary"] ?? "",
                    (int)(long)record["nodeCount"]
                ));
            }

            return results;
        }
        finally
        {
            await session.CloseAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _driver.CloseAsync();
    }
}
```

- [ ] **Step 3: Create Neo4jStore (vector search for Neo4j 5.x)**

`src/Graphiphy.Storage/Providers/Neo4j/Neo4jStore.cs`:
```csharp
using Neo4j.Driver;
using Graphiphy.Models;
using Graphiphy.Storage.Models;

namespace Graphiphy.Storage.Providers.Neo4j;

public sealed class Neo4jStore : BoltStoreBase
{
    private Neo4jStore(IDriver driver, int vectorDimensions = 768)
        : base(driver, vectorDimensions)
    {
    }

    public static async Task<Neo4jStore> CreateAsync(Neo4jConfig config, int vectorDimensions = 768, CancellationToken ct = default)
    {
        var driver = GraphDatabase.Driver(config.Uri, AuthTokens.Basic(config.Username, config.Password));
        var store = new Neo4jStore(driver, vectorDimensions);

        // Initialize vector index (idempotent)
        var session = driver.AsyncSession();
        try
        {
            await session.ExecuteWriteAsync(async tx =>
            {
                // Create constraints
                await tx.RunAsync("CREATE CONSTRAINT snapshot_id IF NOT EXISTS FOR (s:Snapshot) REQUIRE s.id IS UNIQUE");
                await tx.RunAsync("CREATE CONSTRAINT graphnode_id IF NOT EXISTS FOR (n:GraphNode) REQUIRE (n.id, n.snapshotId) IS UNIQUE");

                // Create vector index (Neo4j 5.x syntax)
                await tx.RunAsync(
                    $@"CREATE VECTOR INDEX nodeEmbeddings IF NOT EXISTS
                       FOR (n:GraphNode) ON n.embedding
                       OPTIONS {{indexConfig: {{'vector.dimensions': {vectorDimensions},
                                               'vector.similarity_function': 'cosine'}}}}");
            }, ct);
        }
        finally
        {
            await session.CloseAsync();
        }

        return store;
    }

    public override async Task EmbedNodesAsync(SnapshotId id, IEmbeddingProvider embedder, CancellationToken ct)
    {
        // Retrieve all nodes in snapshot
        var session = _driver.AsyncSession();
        try
        {
            var nodes = await session.ExecuteReadAsync(
                tx => tx.RunAsync(
                    "MATCH (n:GraphNode {snapshotId: $snapshotId}) RETURN n.id as id, n.label as label",
                    new { snapshotId = id.Id })
                    .Then(r => r.ToListAsync()),
                ct);

            // Embed each node
            await session.ExecuteWriteAsync(async tx =>
            {
                foreach (var record in nodes)
                {
                    var nodeId = (string)record["id"];
                    var label = (string)record["label"];
                    var embedding = await embedder.EmbedAsync(label, ct);

                    await tx.RunAsync(
                        @"MATCH (n:GraphNode {id: $id, snapshotId: $snapshotId})
                          SET n.embedding = $embedding",
                        new { id = nodeId, snapshotId = id.Id, embedding = embedding });
                }
            }, ct);
        }
        finally
        {
            await session.CloseAsync();
        }
    }

    public override async Task<IReadOnlyList<Node>> SearchSimilarAsync(SnapshotId id, float[] queryVector, int topN, CancellationToken ct)
    {
        var session = _driver.AsyncSession();
        try
        {
            var results = new List<Node>();
            var records = await session.ExecuteReadAsync(
                tx => tx.RunAsync(
                    @"CALL db.index.vector.queryNodes('nodeEmbeddings', $topN, $queryVector) AS (node, score)
                      WHERE node.snapshotId = $snapshotId
                      RETURN node.id as id, node.label as label, node.fileType as fileType,
                             node.sourceFile as sourceFile, node.sourceLocation as sourceLocation,
                             node.community as community, node.normLabel as normLabel",
                    new { topN, queryVector, snapshotId = id.Id })
                    .Then(r => r.ToListAsync()),
                ct);

            foreach (var record in records)
            {
                results.Add(new Node(
                    (string)record["id"],
                    (string)record["label"],
                    (string)record["fileType"],
                    (string)record["sourceFile"],
                    (string?)record["sourceLocation"],
                    (int?)record["community"],
                    (string?)record["normLabel"]
                ));
            }

            return results;
        }
        finally
        {
            await session.CloseAsync();
        }
    }
}
```

- [ ] **Step 4: Write Neo4jStoreTests (marked to skip without live DB)**

`tests/Graphiphy.Storage.Tests/Neo4jStoreTests.cs`:
```csharp
using Graphiphy.Storage.Providers.Neo4j;
using Graphiphy.Storage.Models;
using Graphiphy.Pipeline;

namespace Graphiphy.Storage.Tests;

[Skip("Requires Neo4j instance on bolt://localhost:7687")]
public class Neo4jStoreTests
{
    private Neo4jStore? _store;

    [Before]
    public async Task Setup()
    {
        var config = new Neo4jConfig("bolt://localhost:7687", "neo4j", "password");
        _store = await Neo4jStore.CreateAsync(config);
    }

    [After]
    public async Task Teardown()
    {
        if (_store is not null)
            await _store.DisposeAsync();
    }

    [Test]
    public async Task SaveSnapshot_CreatesNodesAndEdges()
    {
        // Create a minimal RepositoryAnalysis
        var dir = Path.Combine(Path.GetTempPath(), $"graphiphy_neo4j_{Guid.NewGuid().ToString("N")[..8]}");
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "test.cs"), "class X { void Y() { } }");
            var analysis = await RepositoryAnalysis.RunAsync(dir);
            var snapshotId = SnapshotId.Resolve(dir);

            await _store!.SaveSnapshotAsync(analysis, snapshotId, CancellationToken.None);

            var exists = await _store.SnapshotExistsAsync(snapshotId, CancellationToken.None);
            await Assert.That(exists).IsTrue();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task SnapshotExists_ReturnsFalseForNonExistentSnapshot()
    {
        var snapshotId = new SnapshotId("/nonexistent", "abc123");

        var exists = await _store!.SnapshotExistsAsync(snapshotId, CancellationToken.None);

        await Assert.That(exists).IsFalse();
    }
}
```

- [ ] **Step 5: Commit**

```bash
git add src/Graphiphy.Storage/Providers/Neo4j/*.cs \
         tests/Graphiphy.Storage.Tests/Neo4jStoreTests.cs
git commit -m "feat: implement Neo4j provider with vector indexing

- BoltStoreBase: shared Bolt protocol logic (Neo4j/Memgraph)
- Neo4jConfig: sealed record with connection defaults
- Neo4jStore: vector search via db.index.vector.queryNodes (Neo4j 5.x)
- Initialize constraints and vector index at CreateAsync time
- Neo4jStoreTests: integration tests (skipped without live instance)"
```

---

## Task 4: Create Memgraph Provider

**Files:**
- Create: `src/Graphiphy.Storage/Providers/Memgraph/MemgraphConfig.cs`
- Create: `src/Graphiphy.Storage/Providers/Memgraph/MemgraphStore.cs`
- Test: `tests/Graphiphy.Storage.Tests/MemgraphStoreTests.cs`

- [ ] **Step 1: Create MemgraphConfig**

`src/Graphiphy.Storage/Providers/Memgraph/MemgraphConfig.cs`:
```csharp
namespace Graphiphy.Storage.Providers.Memgraph;

public sealed record MemgraphConfig(
    string Host = "localhost",
    int Port = 7687,
    string Username = "",
    string Password = "") : IGraphStoreConfig
{
    public string Uri => $"bolt://{Host}:{Port}";
}
```

- [ ] **Step 2: Create MemgraphStore (vector search via mg.vector)**

`src/Graphiphy.Storage/Providers/Memgraph/MemgraphStore.cs`:
```csharp
using Neo4j.Driver;
using Graphiphy.Models;
using Graphiphy.Storage.Models;
using Graphiphy.Storage.Providers.Neo4j;

namespace Graphiphy.Storage.Providers.Memgraph;

public sealed class MemgraphStore : BoltStoreBase
{
    private MemgraphStore(IDriver driver, int vectorDimensions = 768)
        : base(driver, vectorDimensions)
    {
    }

    public static async Task<MemgraphStore> CreateAsync(MemgraphConfig config, int vectorDimensions = 768, CancellationToken ct = default)
    {
        var driver = GraphDatabase.Driver(config.Uri,
            config.Username != "" ? AuthTokens.Basic(config.Username, config.Password) : AuthTokens.None);
        var store = new MemgraphStore(driver, vectorDimensions);

        // Initialize vector index (Memgraph-specific)
        var session = driver.AsyncSession();
        try
        {
            await session.ExecuteWriteAsync(async tx =>
            {
                // Create constraints
                await tx.RunAsync("CREATE CONSTRAINT snapshot_id IF NOT EXISTS FOR (s:Snapshot) REQUIRE s.id IS UNIQUE");
                await tx.RunAsync("CREATE CONSTRAINT graphnode_id IF NOT EXISTS FOR (n:GraphNode) REQUIRE (n.id, n.snapshotId) IS UNIQUE");

                // Memgraph vector index (mg.vector procedures)
                // Note: Memgraph syntax differs from Neo4j; uses mg.index.vector.create
                await tx.RunAsync(
                    $@"CALL mg.index.vector.create('nodeEmbeddings', 'GraphNode', 'embedding', {vectorDimensions}, 'cosine')");
            }, ct);
        }
        catch
        {
            // Index may already exist; continue
        }
        finally
        {
            await session.CloseAsync();
        }

        return store;
    }

    public override async Task EmbedNodesAsync(SnapshotId id, IEmbeddingProvider embedder, CancellationToken ct)
    {
        // Same as Neo4jStore
        var session = _driver.AsyncSession();
        try
        {
            var nodes = await session.ExecuteReadAsync(
                tx => tx.RunAsync(
                    "MATCH (n:GraphNode {snapshotId: $snapshotId}) RETURN n.id as id, n.label as label",
                    new { snapshotId = id.Id })
                    .Then(r => r.ToListAsync()),
                ct);

            await session.ExecuteWriteAsync(async tx =>
            {
                foreach (var record in nodes)
                {
                    var nodeId = (string)record["id"];
                    var label = (string)record["label"];
                    var embedding = await embedder.EmbedAsync(label, ct);

                    await tx.RunAsync(
                        @"MATCH (n:GraphNode {id: $id, snapshotId: $snapshotId})
                          SET n.embedding = $embedding",
                        new { id = nodeId, snapshotId = id.Id, embedding = embedding });
                }
            }, ct);
        }
        finally
        {
            await session.CloseAsync();
        }
    }

    public override async Task<IReadOnlyList<Node>> SearchSimilarAsync(SnapshotId id, float[] queryVector, int topN, CancellationToken ct)
    {
        var session = _driver.AsyncSession();
        try
        {
            var results = new List<Node>();
            // Memgraph uses mg.vector.query for similarity search
            var records = await session.ExecuteReadAsync(
                tx => tx.RunAsync(
                    @"CALL mg.index.vector.query('nodeEmbeddings', $queryVector, $topN) AS (node, score)
                      WHERE node.snapshotId = $snapshotId
                      RETURN node.id as id, node.label as label, node.fileType as fileType,
                             node.sourceFile as sourceFile, node.sourceLocation as sourceLocation,
                             node.community as community, node.normLabel as normLabel",
                    new { queryVector, topN, snapshotId = id.Id })
                    .Then(r => r.ToListAsync()),
                ct);

            foreach (var record in records)
            {
                results.Add(new Node(
                    (string)record["id"],
                    (string)record["label"],
                    (string)record["fileType"],
                    (string)record["sourceFile"],
                    (string?)record["sourceLocation"],
                    (int?)record["community"],
                    (string?)record["normLabel"]
                ));
            }

            return results;
        }
        finally
        {
            await session.CloseAsync();
        }
    }
}
```

- [ ] **Step 3: Write MemgraphStoreTests**

`tests/Graphiphy.Storage.Tests/MemgraphStoreTests.cs`:
```csharp
using Graphiphy.Storage.Providers.Memgraph;
using Graphiphy.Storage.Models;
using Graphiphy.Pipeline;

namespace Graphiphy.Storage.Tests;

[Skip("Requires Memgraph instance on localhost:7687")]
public class MemgraphStoreTests
{
    private MemgraphStore? _store;

    [Before]
    public async Task Setup()
    {
        var config = new MemgraphConfig("localhost", 7687);
        _store = await MemgraphStore.CreateAsync(config);
    }

    [After]
    public async Task Teardown()
    {
        if (_store is not null)
            await _store.DisposeAsync();
    }

    [Test]
    public async Task SaveSnapshot_CreatesNodesAndEdges()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"graphiphy_mem_{Guid.NewGuid().ToString("N")[..8]}");
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "test.py"), "class X:\n    def y(self): pass");
            var analysis = await RepositoryAnalysis.RunAsync(dir);
            var snapshotId = SnapshotId.Resolve(dir);

            await _store!.SaveSnapshotAsync(analysis, snapshotId, CancellationToken.None);

            var exists = await _store.SnapshotExistsAsync(snapshotId, CancellationToken.None);
            await Assert.That(exists).IsTrue();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
```

- [ ] **Step 4: Commit**

```bash
git add src/Graphiphy.Storage/Providers/Memgraph/*.cs \
         tests/Graphiphy.Storage.Tests/MemgraphStoreTests.cs
git commit -m "feat: implement Memgraph provider with vector search

- MemgraphConfig: Bolt connection to Memgraph (localhost:7687)
- MemgraphStore: vector search via mg.index.vector.query
- Reuses BoltStoreBase for node/edge persistence
- MemgraphStoreTests: integration tests (skipped without live instance)"
```

---

## Task 5: Create Embedding Abstractions

**Files:**
- Create: `src/Graphiphy.Storage/Embedding/IEmbeddingProviderConfig.cs`
- Create: `src/Graphiphy.Storage/Embedding/IEmbeddingProvider.cs`
- Create: `src/Graphiphy.Storage/Embedding/CloudflareEmbeddingConfig.cs`
- Create: `src/Graphiphy.Storage/Embedding/CloudflareEmbeddingProvider.cs`
- Create: `src/Graphiphy.Storage/Embedding/EmbeddingProviderFactory.cs`
- Test: `tests/Graphiphy.Storage.Tests/CloudflareEmbeddingProviderTests.cs`

- [ ] **Step 1: Create IEmbeddingProviderConfig and IEmbeddingProvider interfaces**

`src/Graphiphy.Storage/Embedding/IEmbeddingProviderConfig.cs`:
```csharp
namespace Graphiphy.Storage.Embedding;

public interface IEmbeddingProviderConfig { }
```

`src/Graphiphy.Storage/Embedding/IEmbeddingProvider.cs`:
```csharp
namespace Graphiphy.Storage.Embedding;

public interface IEmbeddingProvider
{
    int Dimensions { get; }
    Task<float[]> EmbedAsync(string text, CancellationToken ct);
}
```

- [ ] **Step 2: Create CloudflareEmbeddingConfig**

`src/Graphiphy.Storage/Embedding/CloudflareEmbeddingConfig.cs`:
```csharp
namespace Graphiphy.Storage.Embedding;

public sealed record CloudflareEmbeddingConfig(
    string AccountId,
    string ApiToken,
    string Model = "@cf/baai/bge-base-en-v1.5") : IEmbeddingProviderConfig
{
    public int GetDimensions() => Model switch
    {
        "@cf/baai/bge-small-en-v1.5" => 384,
        "@cf/baai/bge-base-en-v1.5" => 768,
        "@cf/baai/bge-large-en-v1.5" => 1024,
        _ => 768
    };
}
```

- [ ] **Step 3: Create CloudflareEmbeddingProvider**

`src/Graphiphy.Storage/Embedding/CloudflareEmbeddingProvider.cs`:
```csharp
using OpenAI;

namespace Graphiphy.Storage.Embedding;

public sealed class CloudflareEmbeddingProvider : IEmbeddingProvider
{
    private readonly OpenAIClient _client;
    private readonly CloudflareEmbeddingConfig _config;

    public int Dimensions { get; }

    public CloudflareEmbeddingProvider(CloudflareEmbeddingConfig config)
    {
        _config = config;
        Dimensions = config.GetDimensions();

        // Cloudflare Workers AI endpoint (OpenAI-compatible)
        var endpoint = new Uri($"https://api.cloudflare.com/client/v4/accounts/{config.AccountId}/ai/v1/");
        _client = new OpenAIClient(new(config.ApiToken), new() { Endpoint = endpoint });
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct)
    {
        var response = await _client.GetEmbeddingClient(_config.Model)
            .GenerateEmbeddingAsync(text, cancellationToken: ct);

        return response.Value.Embedding.ToArray();
    }
}
```

- [ ] **Step 4: Create EmbeddingProviderFactory**

`src/Graphiphy.Storage/Embedding/EmbeddingProviderFactory.cs`:
```csharp
namespace Graphiphy.Storage.Embedding;

public static class EmbeddingProviderFactory
{
    public static IEmbeddingProvider Create(IEmbeddingProviderConfig config) => config switch
    {
        CloudflareEmbeddingConfig cf => new CloudflareEmbeddingProvider(cf),
        _ => throw new ArgumentException($"Unknown embedding config type: {config.GetType().Name}", nameof(config))
    };
}
```

- [ ] **Step 5: Write CloudflareEmbeddingProviderTests (mocked HTTP)**

`tests/Graphiphy.Storage.Tests/CloudflareEmbeddingProviderTests.cs`:
```csharp
using Graphiphy.Storage.Embedding;

namespace Graphiphy.Storage.Tests;

public class CloudflareEmbeddingProviderTests
{
    [Test]
    public async Task GetDimensions_ReturnsCorrectDim_ForEachModel()
    {
        var models = new[]
        {
            ("@cf/baai/bge-small-en-v1.5", 384),
            ("@cf/baai/bge-base-en-v1.5", 768),
            ("@cf/baai/bge-large-en-v1.5", 1024),
        };

        foreach (var (model, expectedDims) in models)
        {
            var config = new CloudflareEmbeddingConfig("test-account", "test-token", model);
            await Assert.That(config.GetDimensions()).IsEqualTo(expectedDims);
        }
    }

    [Test]
    public async Task Constructor_SetsCorrectDimensions()
    {
        var config = new CloudflareEmbeddingConfig("test", "test", "@cf/baai/bge-base-en-v1.5");
        var provider = new CloudflareEmbeddingProvider(config);

        await Assert.That(provider.Dimensions).IsEqualTo(768);
    }

    [Test]
    [Skip("Requires live Cloudflare API token")]
    public async Task EmbedAsync_CallsCloudflareAndReturnsVector()
    {
        var token = Environment.GetEnvironmentVariable("CF_API_TOKEN") ?? "";
        if (string.IsNullOrEmpty(token))
            return;

        var accountId = Environment.GetEnvironmentVariable("CF_ACCOUNT_ID") ?? "";
        var config = new CloudflareEmbeddingConfig(accountId, token);
        var provider = new CloudflareEmbeddingProvider(config);

        var result = await provider.EmbedAsync("test query", CancellationToken.None);

        await Assert.That(result).HasLength(768);
        await Assert.That(result[0]).IsNotEqualTo(0f); // At least one non-zero value
    }
}
```

- [ ] **Step 6: Run tests**

```bash
cd /home/timm/ngraphify
dotnet run --project tests/Graphiphy.Storage.Tests/ 2>&1 | grep -E "Dimensions|passed|failed"
```

Expected: 2 non-skipped tests pass (model tests), 1 integration test skipped.

- [ ] **Step 7: Commit**

```bash
git add src/Graphiphy.Storage/Embedding/*.cs \
         tests/Graphiphy.Storage.Tests/CloudflareEmbeddingProviderTests.cs
git commit -m "feat: implement Cloudflare Workers AI embedding provider

- IEmbeddingProviderConfig and IEmbeddingProvider abstractions
- CloudflareEmbeddingConfig: API token + model selection
- CloudflareEmbeddingProvider: calls Cloudflare via OpenAI SDK (compatible endpoint)
- Model dimensions: 384 (small), 768 (base), 1024 (large)
- EmbeddingProviderFactory: switch-on-type pattern
- CloudflareEmbeddingProviderTests: dimension mapping + live API test (skipped by default)"
```

---

## Task 6: Create GraphStoreFactory

**Files:**
- Create: `src/Graphiphy.Storage/GraphStoreFactory.cs`

- [ ] **Step 1: Create GraphStoreFactory**

`src/Graphiphy.Storage/GraphStoreFactory.cs`:
```csharp
using Graphiphy.Storage.Providers.Neo4j;
using Graphiphy.Storage.Providers.Memgraph;

namespace Graphiphy.Storage;

public static class GraphStoreFactory
{
    public static async Task<IGraphStore> CreateAsync(
        IGraphStoreConfig config,
        int vectorDimensions = 768,
        CancellationToken ct = default)
    {
        return config switch
        {
            Neo4jConfig neo4j => await Neo4jStore.CreateAsync(neo4j, vectorDimensions, ct),
            MemgraphConfig mem => await MemgraphStore.CreateAsync(mem, vectorDimensions, ct),
            _ => throw new ArgumentException($"Unknown graph store config: {config.GetType().Name}", nameof(config))
        };
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add src/Graphiphy.Storage/GraphStoreFactory.cs
git commit -m "feat: add GraphStoreFactory for provider dispatch

Follows IAgentConfig pattern from Graphiphy.Llm"
```

---

## Task 7: Create PushCommand

**Files:**
- Create: `src/Graphiphy.Cli/Commands/PushCommand.cs`
- Modify: `src/Graphiphy.Cli/Program.cs`

- [ ] **Step 1: Create PushCommand**

`src/Graphiphy.Cli/Commands/PushCommand.cs`:
```csharp
using System.ComponentModel;
using Graphiphy.Cli.Mcp;
using Graphiphy.Llm;
using Graphiphy.Pipeline;
using Graphiphy.Storage;
using Graphiphy.Storage.Embedding;
using Graphiphy.Storage.Models;
using Graphiphy.Storage.Providers.Memgraph;
using Graphiphy.Storage.Providers.Neo4j;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Graphiphy.Cli.Commands;

public sealed class PushSettings : CommandSettings
{
    [CommandArgument(0, "<path>")]
    [Description("Repository root directory.")]
    public required string Path { get; init; }

    [CommandOption("--backend <backend>")]
    [Description("Graph database: neo4j or memgraph")]
    public required string Backend { get; init; }

    [CommandOption("--uri <uri>")]
    [Description("Neo4j URI. Default: bolt://localhost:7687")]
    public string? Uri { get; init; }

    [CommandOption("--host <host>")]
    [Description("Memgraph host. Default: localhost")]
    public string Host { get; init; } = "localhost";

    [CommandOption("--port <port>")]
    [Description("Memgraph port. Default: 7687")]
    public int Port { get; init; } = 7687;

    [CommandOption("--username <username>")]
    [Description("Database username.")]
    public string? Username { get; init; }

    [CommandOption("--password <password>")]
    [Description("Database password.")]
    public string? Password { get; init; }

    [CommandOption("--embed")]
    [Description("Embed nodes after push.")]
    public bool Embed { get; init; }

    [CommandOption("--cf-account <id>")]
    [Description("Cloudflare account ID (for --embed).")]
    public string? CloudflareAccount { get; init; }

    [CommandOption("--cf-token <token>")]
    [Description("Cloudflare API token (or CF_API_TOKEN env).")]
    public string? CloudflareToken { get; init; }

    [CommandOption("--cf-model <model>")]
    [Description("Cloudflare embedding model. Default: @cf/baai/bge-base-en-v1.5")]
    public string CloudflareModel { get; init; } = "@cf/baai/bge-base-en-v1.5";

    [CommandOption("--summarize")]
    [Description("Generate community summaries.")]
    public bool Summarize { get; init; }

    [CommandOption("--provider <provider>")]
    [Description("LLM provider for summaries: anthropic (default), openai, ollama, copilot, a2a")]
    public string LlmProvider { get; init; } = "anthropic";

    [CommandOption("--key <key>")]
    [Description("LLM API key (or env var).")]
    public string? ApiKey { get; init; }

    [CommandOption("--model <model>")]
    [Description("LLM model name.")]
    public string? Model { get; init; }

    [CommandOption("--force")]
    [Description("Re-push even if snapshot exists.")]
    public bool Force { get; init; }

    [CommandOption("--cache <dir>")]
    [Description("Cache directory. Default: <path>/.graphiphy-cache")]
    public string? CacheDir { get; init; }
}

public sealed class PushCommand : AsyncCommand<PushSettings>
{
    protected override async Task<int> ExecuteAsync(
        CommandContext context, PushSettings settings, CancellationToken cancellationToken)
    {
        // 1. Resolve snapshot ID
        Console.Error.WriteLine("[graphiphy] Resolving snapshot ID...");
        var snapshotId = SnapshotId.Resolve(settings.Path);
        Console.Error.WriteLine($"[graphiphy] Snapshot: {snapshotId.Id}");

        // 2. Check if snapshot already exists
        IGraphStoreConfig storeConfig = settings.Backend.ToLowerInvariant() switch
        {
            "neo4j" => new Neo4jConfig(
                settings.Uri ?? "bolt://localhost:7687",
                settings.Username ?? "neo4j",
                settings.Password ?? ""),
            "memgraph" => new MemgraphConfig(
                settings.Host,
                settings.Port,
                settings.Username ?? "",
                settings.Password ?? ""),
            _ => throw new InvalidOperationException($"Unknown backend: {settings.Backend}")
        };

        using var store = await GraphStoreFactory.CreateAsync(storeConfig, cancellationToken: cancellationToken);

        var exists = await store.SnapshotExistsAsync(snapshotId, cancellationToken);
        if (exists && !settings.Force)
        {
            AnsiConsole.MarkupLine($"[yellow][graphiphy] Snapshot already exists, skipping.[/]");
            return 0;
        }

        // 3. Run analysis
        RepositoryAnalysis? analysis = null;
        await AnsiConsole.Status().Spinner(Spinner.Known.Dots)
            .StartAsync("Analyzing repository...", async ctx =>
            {
                analysis = await RepositoryAnalysis.RunAsync(
                    settings.Path, cacheDir: settings.CacheDir,
                    onProgress: msg => ctx.Status(msg), ct: cancellationToken);
            });

        if (analysis is null)
            return 1;

        // 4. Save snapshot
        AnsiConsole.MarkupLine("[blue][graphiphy] Saving snapshot...[/]");
        await store.SaveSnapshotAsync(analysis, snapshotId, cancellationToken);
        AnsiConsole.MarkupLine($"[green][graphiphy] Snapshot saved: {analysis.Graph.VertexCount} nodes, {analysis.Graph.EdgeCount} edges[/]");

        // 5. Embed nodes (optional)
        if (settings.Embed)
        {
            var cfToken = settings.CloudflareToken ?? Environment.GetEnvironmentVariable("CF_API_TOKEN");
            if (string.IsNullOrEmpty(settings.CloudflareAccount) || string.IsNullOrEmpty(cfToken))
            {
                AnsiConsole.MarkupLine("[red]Error: --cf-account and --cf-token (or CF_API_TOKEN) required for --embed[/]");
                return 1;
            }

            var embedConfig = new CloudflareEmbeddingConfig(settings.CloudflareAccount, cfToken, settings.CloudflareModel);
            var embedder = new CloudflareEmbeddingProvider(embedConfig);

            AnsiConsole.MarkupLine("[blue][graphiphy] Embedding nodes...[/]");
            await AnsiConsole.Status().Spinner(Spinner.Known.Dots)
                .StartAsync("Embedding...", async ctx =>
                {
                    await store.EmbedNodesAsync(snapshotId, embedder, cancellationToken);
                });
            AnsiConsole.MarkupLine("[green][graphiphy] Nodes embedded[/]");
        }

        // 6. Generate community summaries (optional)
        if (settings.Summarize)
        {
            IAgentConfig llmConfig = settings.LlmProvider.ToLowerInvariant() switch
            {
                "openai" => new OpenAiConfig(
                    ApiKey: settings.ApiKey ?? Env("OPENAI_API_KEY") ?? Error("--key or OPENAI_API_KEY required"),
                    Model: settings.Model ?? "gpt-4o"),
                "ollama" => new OllamaConfig(Model: settings.Model ?? "llama3.2"),
                "copilot" => new CopilotConfig(),
                "a2a" => new A2AConfig(
                    AgentUrl: Env("A2A_AGENT_URL") ?? Error("A2A_AGENT_URL required"),
                    ApiKey: settings.ApiKey),
                _ => new AnthropicConfig(
                    ApiKey: settings.ApiKey ?? Env("ANTHROPIC_API_KEY") ?? Error("--key or ANTHROPIC_API_KEY required"),
                    Model: settings.Model ?? "claude-sonnet-4-6"),
            };

            AnsiConsole.MarkupLine("[blue][graphiphy] Generating community summaries...[/]");
            await using var agent = await GraphAgentFactory.CreateAsync(llmConfig, analysis.Graph, cancellationToken);
            await using var session = await agent.CreateSessionAsync(cancellationToken);

            var summaries = new List<CommunitySummary>();
            var communities = analysis.Graph.Vertices
                .Where(n => n.Community.HasValue)
                .GroupBy(n => n.Community!.Value)
                .ToList();

            foreach (var community in communities)
            {
                var nodes = string.Join(", ", community.Select(n => n.Label).Take(5));
                var prompt = $"Summarize this software module in 1-2 sentences: {nodes}";

                try
                {
                    var summary = await agent.AnswerAsync(prompt, session, cancellationToken);
                    summaries.Add(new(community.Key, summary, community.Count()));
                    AnsiConsole.MarkupLine($"[dim]  Community {community.Key}: summarized[/]");
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[yellow]  Community {community.Key}: summary failed ({ex.Message})[/]");
                }
            }

            await store.SaveCommunitySummariesAsync(snapshotId, summaries, cancellationToken);
            AnsiConsole.MarkupLine($"[green][graphiphy] Saved {summaries.Count} community summaries[/]");
        }

        AnsiConsole.MarkupLine("[green][graphiphy] Push complete[/]");
        return 0;
    }

    private static string? Env(string name) => Environment.GetEnvironmentVariable(name);

    private static string Error(string message)
    {
        AnsiConsole.MarkupLine($"[red]{message}[/]");
        throw new InvalidOperationException(message);
    }
}
```

- [ ] **Step 2: Update Program.cs**

Modify `src/Graphiphy.Cli/Program.cs` to add the push command:

```csharp
using Graphiphy.Cli.Commands;
using Spectre.Console.Cli;

var app = new CommandApp();
app.Configure(config =>
{
    config.SetApplicationName("graphiphy");
    config.AddCommand<AnalyzeCommand>("analyze")
          .WithDescription("Analyze a repository and print graph statistics.");
    config.AddCommand<ReportCommand>("report")
          .WithDescription("Generate a Markdown report for a repository.");
    config.AddCommand<QueryCommand>("query")
          .WithDescription("Ask an LLM a question about the repository graph.");
    config.AddCommand<ServeCommand>("serve")
          .WithDescription("Start an MCP server over stdio for the given repository.");
    config.AddCommand<PushCommand>("push")
          .WithDescription("Push repository graph to Neo4j or Memgraph with optional embeddings and summaries.");
});
return app.Run(args);
```

- [ ] **Step 3: Verify PushCommand compiles**

```bash
cd /home/timm/ngraphify
dotnet build src/Graphiphy.Cli/ -v minimal 2>&1 | tail -5
```

Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add src/Graphiphy.Cli/Commands/PushCommand.cs src/Graphiphy.Cli/Program.cs
git commit -m "feat: add push CLI command for graph persistence

- Resolve snapshot ID via git commit or content hash
- Save graph to Neo4j/Memgraph with --backend flag
- Optional: embed nodes via Cloudflare Workers AI (--embed)
- Optional: generate community summaries via LLM (--summarize)
- Idempotent: skip if snapshot exists (unless --force)
- Reuses existing LLM provider infrastructure"
```

---

## Task 8: Test Everything

**Files:**
- Create: `tests/Graphiphy.Storage.Tests/Graphiphy.Storage.Tests.csproj`

- [ ] **Step 1: Create test project file**

`tests/Graphiphy.Storage.Tests/Graphiphy.Storage.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="TUnit" Version="1.43.41" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../../src/Graphiphy.Storage/Graphiphy.Storage.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Add test project to solution**

```bash
dotnet sln /home/timm/ngraphify/Graphiphy.sln add tests/Graphiphy.Storage.Tests/Graphiphy.Storage.Tests.csproj
```

- [ ] **Step 3: Run all unit tests**

```bash
cd /home/timm/ngraphify
dotnet run --project tests/Graphiphy.Storage.Tests/ 2>&1 | tail -15
```

Expected: All unit tests pass, integration tests skipped.

- [ ] **Step 4: Smoke test the CLI**

```bash
dotnet run --project src/Graphiphy.Cli/ -- push --help 2>&1 | head -20
```

Expected: Push command help text displayed.

- [ ] **Step 5: Commit**

```bash
git add tests/Graphiphy.Storage.Tests/Graphiphy.Storage.Tests.csproj Graphiphy.sln
git commit -m "test: create Graphiphy.Storage.Tests project with TUnit

All unit tests pass; integration tests skipped without live DB instance"
```

---

## Task 9: Update Documentation

**Files:**
- Create: `docs/superpowers/plans/2026-05-10-graph-database-integration.md` (this file)

- [ ] **Step 1: Document the feature**

Create comprehensive documentation of the new `push` command and graph storage integration.

---

## Summary

Total commits: 9

- Task 1: Project setup + dependencies
- Task 2: Core interfaces & models
- Task 3: Neo4j provider
- Task 4: Memgraph provider
- Task 5: Embedding abstractions
- Task 6: GraphStoreFactory
- Task 7: PushCommand + Program.cs
- Task 8: Test project
- Task 9: Documentation

After all tasks are complete:
1. All unit tests pass
2. CLI `push` command available
3. Ready for manual integration testing against live Neo4j/Memgraph instances
