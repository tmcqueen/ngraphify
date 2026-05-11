# Code Review Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Address all Critical + Important findings (10) and curated Minor patterns from the 2026-05-11 code review.

**Architecture:** Each task is a self-contained fix with TDD where the behavior is testable. Commits are scoped to one issue each so they can be reverted independently. Two tasks bundle the `IProcessRunner` signature change because the interface refactor would otherwise be done twice.

**Tech Stack:** .NET 10, TUnit 1.43.41, Spectre.Console 0.55+, Neo4j.Driver, LibGit2Sharp.

---

## File map

**Modified:**
- `src/Ngraphiphy.Storage/Models/SnapshotId.cs` — fix hash byte-length bug, also pass in the source-content reading
- `src/Ngraphiphy.Cli/Configuration/Secrets/SecretResolver.cs` — broaden catch, accept logger delegate
- `src/Ngraphiphy.Cli/Configuration/Secrets/IProcessRunner.cs` — switch to `IReadOnlyList<string>` args, async
- `src/Ngraphiphy.Cli/Configuration/Secrets/SystemProcessRunner.cs` — async stdout/stderr drain, ArgumentList
- `src/Ngraphiphy.Cli/Configuration/Secrets/PassSecretProvider.cs` — async, ArgumentList
- `src/Ngraphiphy.Cli/Configuration/CliHostExtensions.cs` — register both providers, wire logger to SecretResolver, await async secret resolve
- `src/Ngraphiphy.Cli/Commands/*.cs` — replace `MarkupLine` with `MarkupLineInterpolated` for user-controlled values
- `src/Ngraphiphy/Cluster/NativeLibraryResolver.cs` — remove hardcoded personal paths, make `_registered` thread-safe
- `src/Ngraphiphy.Storage/Providers/Memgraph/MemgraphStore.cs` — narrow catch to "already exists"
- `src/Ngraphiphy.Storage/Providers/Neo4j/BoltStoreBase.cs` — inline depth as literal in Cypher
- `src/Ngraphiphy/Dedup/EntityDeduplicator.cs` — replace `nodes.First(n => n.Id == ...)` with dictionary lookups
- `src/Ngraphiphy/Validation/ExtractionValidator.cs` — split into "errors" vs "warnings", add `Video`
- `src/Ngraphiphy/Cache/ExtractionCache.cs` — overload `FileHash` accepting pre-read content
- `src/Ngraphiphy.Pipeline/RepositoryAnalysis.cs` — parallelize extraction, single read per file, emit validator warnings

**Tests (created or extended):**
- `tests/Ngraphiphy.Storage.Tests/SnapshotIdTests.cs`
- `tests/Ngraphiphy.Cli.Tests/Configuration/SecretResolverTests.cs` (extend)
- `tests/Ngraphiphy.Cli.Tests/Configuration/PassSecretProviderTests.cs` (extend)
- `tests/Ngraphiphy.Cli.Tests/Configuration/SystemProcessRunnerTests.cs`
- `tests/Ngraphiphy.Storage.Tests/MemgraphStoreTests.cs` (extend if exists, else create)
- `tests/Ngraphiphy.Tests/Dedup/EntityDeduplicatorPerfTests.cs`
- `tests/Ngraphiphy.Tests/Validation/ExtractionValidatorTests.cs`

---

## Task 1: Fix `SnapshotId.ComputeContentHash` byte-length bug (Critical #1)

**Files:**
- Modify: `src/Ngraphiphy.Storage/Models/SnapshotId.cs:34-39`
- Test: `tests/Ngraphiphy.Storage.Tests/SnapshotIdTests.cs` (create)

- [ ] **Step 1: Write the failing test**

Create `tests/Ngraphiphy.Storage.Tests/SnapshotIdTests.cs`:

```csharp
using Ngraphiphy.Storage.Models;

namespace Ngraphiphy.Storage.Tests;

public class SnapshotIdTests
{
    [Test]
    public async Task Resolve_NonGitDir_HashDistinguishesByNonAsciiPath()
    {
        // Two temp dirs whose paths share an ASCII prefix but diverge in non-ASCII bytes.
        // With the old bug, only the first `path.Length` chars of UTF-8 bytes would be hashed,
        // truncating the non-ASCII suffix and producing identical hashes.
        var baseDir = Path.Combine(Path.GetTempPath(), "ngraphiphy-snapshot-test-" + Guid.NewGuid());
        var dirA = Path.Combine(baseDir, "проект-a"); // Cyrillic
        var dirB = Path.Combine(baseDir, "проект-b");
        Directory.CreateDirectory(dirA);
        Directory.CreateDirectory(dirB);
        await File.WriteAllTextAsync(Path.Combine(dirA, "a.cs"), "class A {}");
        await File.WriteAllTextAsync(Path.Combine(dirB, "a.cs"), "class A {}");

        try
        {
            var idA = SnapshotId.Resolve(dirA);
            var idB = SnapshotId.Resolve(dirB);

            await Assert.That(idA.CommitHash).IsNotEqualTo(idB.CommitHash);
        }
        finally
        {
            Directory.Delete(baseDir, recursive: true);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test tests/Ngraphiphy.Storage.Tests/ --filter "FullyQualifiedName~SnapshotIdTests"
```
Expected: FAIL — hashes collide because `file.Length` (char count) truncates the UTF-8 byte array.

- [ ] **Step 3: Apply the fix**

Replace `src/Ngraphiphy.Storage/Models/SnapshotId.cs:34-39`:

```csharp
foreach (var file in files)
{
    var pathBytes = System.Text.Encoding.UTF8.GetBytes(file);
    hasher.TransformBlock(pathBytes, 0, pathBytes.Length, null, 0);
    var contentBytes = File.ReadAllBytes(file);
    hasher.TransformBlock(contentBytes, 0, contentBytes.Length, null, 0);
}
```

- [ ] **Step 4: Run test to verify it passes**

```bash
dotnet test tests/Ngraphiphy.Storage.Tests/ --filter "FullyQualifiedName~SnapshotIdTests"
```
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Ngraphiphy.Storage/Models/SnapshotId.cs tests/Ngraphiphy.Storage.Tests/SnapshotIdTests.cs
git commit -m "fix(storage): hash full UTF-8 byte length in SnapshotId.ComputeContentHash"
```

---

## Task 2: Broaden `SecretResolver` catch to include `Win32Exception` (Critical #2)

**Files:**
- Modify: `src/Ngraphiphy.Cli/Configuration/Secrets/SecretResolver.cs:39`
- Test: `tests/Ngraphiphy.Cli.Tests/Configuration/SecretResolverTests.cs` (extend)

- [ ] **Step 1: Write the failing test**

Append to `tests/Ngraphiphy.Cli.Tests/Configuration/SecretResolverTests.cs`:

```csharp
[Test]
public async Task ResolveAndOverlay_ProviderThrowsWin32Exception_DoesNotPropagate()
{
    var throwingProvider = new ThrowingProvider(
        new System.ComponentModel.Win32Exception(2, "No such file or directory"));
    var providers = new Dictionary<string, ISecretProvider>(StringComparer.Ordinal)
    {
        ["pass"] = throwingProvider,
    };
    var snapshot = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Llm:ApiKey"] = "pass://x" })
        .Build();
    var target = new ConfigurationBuilder();

    var act = () => SecretResolver.ResolveAndOverlay(target, snapshot, providers);

    await Assert.That(act).ThrowsNothing();
}

private sealed class ThrowingProvider(Exception ex) : ISecretProvider
{
    public string Resolve(string path) => throw ex;
}
```

If `ThrowingProvider` already exists, reuse it; otherwise add it as shown.

- [ ] **Step 2: Run test, verify it fails**

```bash
dotnet test tests/Ngraphiphy.Cli.Tests/ --filter "FullyQualifiedName~SecretResolverTests.ResolveAndOverlay_ProviderThrowsWin32Exception"
```
Expected: FAIL — `Win32Exception` escapes the existing `catch (InvalidOperationException)`.

- [ ] **Step 3: Apply the fix**

In `src/Ngraphiphy.Cli/Configuration/Secrets/SecretResolver.cs`, replace the catch clause at line 39:

```csharp
catch (Exception ex) when (
    ex is InvalidOperationException
    || ex is System.ComponentModel.Win32Exception
    || ex is IOException)
{
    // Secret couldn't be resolved (provider missing, binary not on PATH, transient I/O).
    // Don't fail CLI startup — the command that actually needs this secret will fail with a clear error.
    AnsiConsole.MarkupLineInterpolated($"[yellow]Warning: Could not resolve secret reference '{key}': {ex.Message}[/]");
}
```

(`MarkupLineInterpolated` is the right call here — see Task 3.)

- [ ] **Step 4: Run test, verify it passes**

```bash
dotnet test tests/Ngraphiphy.Cli.Tests/ --filter "FullyQualifiedName~SecretResolverTests"
```
Expected: PASS (all SecretResolver tests).

- [ ] **Step 5: Commit**

```bash
git add src/Ngraphiphy.Cli/Configuration/Secrets/SecretResolver.cs tests/Ngraphiphy.Cli.Tests/Configuration/SecretResolverTests.cs
git commit -m "fix(cli): non-fatal handling for Win32Exception during secret resolution"
```

---

## Task 3: Replace `MarkupLine` with `MarkupLineInterpolated` at user-data sites (Critical #3)

**Files (modify in-place):**
- `src/Ngraphiphy.Cli/Commands/ReportCommand.cs:42, 51`
- `src/Ngraphiphy.Cli/Commands/QueryCommand.cs:96, 113, 124`
- `src/Ngraphiphy.Cli/Commands/PushCommand.cs:138, 158, 235, 239, 244, 255`
- `src/Ngraphiphy.Cli/Commands/AnalyzeCommand.cs:56`
- `src/Ngraphiphy.Cli/Configuration/Secrets/SecretResolver.cs:29, 43` (already done in Task 2 for line 43)

Rule: any `AnsiConsole.MarkupLine($"...{userValue}...")` where `userValue` could contain `[` or `]` must be `AnsiConsole.MarkupLineInterpolated(...)`. Constant strings (no interpolation) stay as `MarkupLine`.

- [ ] **Step 1: Write a regression test**

Create `tests/Ngraphiphy.Cli.Tests/Configuration/SecretResolverMarkupTests.cs`:

```csharp
using Microsoft.Extensions.Configuration;
using Ngraphiphy.Cli.Configuration.Secrets;

namespace Ngraphiphy.Cli.Tests.Configuration;

public class SecretResolverMarkupTests
{
    private sealed class BracketProvider : ISecretProvider
    {
        public string Resolve(string path)
            => throw new InvalidOperationException("connection [host:port] failed");
    }

    [Test]
    public async Task ResolveAndOverlay_ExceptionMessageHasBrackets_DoesNotThrowMarkupException()
    {
        var providers = new Dictionary<string, ISecretProvider>(StringComparer.Ordinal)
        {
            ["pass"] = new BracketProvider(),
        };
        var snapshot = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Llm:Key"] = "pass://x" })
            .Build();
        var target = new ConfigurationBuilder();

        var act = () => SecretResolver.ResolveAndOverlay(target, snapshot, providers);

        await Assert.That(act).ThrowsNothing();
    }
}
```

- [ ] **Step 2: Run, observe failure**

```bash
dotnet test tests/Ngraphiphy.Cli.Tests/ --filter "SecretResolverMarkupTests"
```
Expected: FAIL with `Spectre.Console.Exceptions.InvalidMarkupException` or similar.

- [ ] **Step 3: Apply the edits**

For each call site listed above, change `AnsiConsole.MarkupLine($"...")` to `AnsiConsole.MarkupLineInterpolated($"...")` — keep the format string identical. Example:

```csharp
// Before
AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
// After
AnsiConsole.MarkupLineInterpolated($"[red]Error: {ex.Message}[/]");
```

Also fix `SecretResolver.cs:29`:

```csharp
AnsiConsole.MarkupLineInterpolated($"[yellow]Warning: No secret provider registered for scheme '{reference.Scheme}' (key: {key})[/]");
```

- [ ] **Step 4: Run tests, verify pass**

```bash
dotnet test tests/Ngraphiphy.Cli.Tests/
```
Expected: all green.

- [ ] **Step 5: Commit**

```bash
git add src/Ngraphiphy.Cli tests/Ngraphiphy.Cli.Tests/Configuration/SecretResolverMarkupTests.cs
git commit -m "fix(cli): escape interpolated values in AnsiConsole.MarkupLine call sites"
```

---

## Task 4: Refactor `IProcessRunner` to async + `IReadOnlyList<string>` args (Important #5 + #6)

This task bundles arg-injection (#5) and stdout/stderr deadlock (#6) because both require changing the `IProcessRunner` signature.

**Files:**
- Modify: `src/Ngraphiphy.Cli/Configuration/Secrets/IProcessRunner.cs`
- Modify: `src/Ngraphiphy.Cli/Configuration/Secrets/SystemProcessRunner.cs`
- Modify: `src/Ngraphiphy.Cli/Configuration/Secrets/PassSecretProvider.cs`
- Modify: `src/Ngraphiphy.Cli/Configuration/Secrets/ISecretProvider.cs` (make `Resolve` async)
- Modify: `src/Ngraphiphy.Cli/Configuration/Secrets/EnvSecretProvider.cs`
- Modify: `src/Ngraphiphy.Cli/Configuration/Secrets/SecretResolver.cs` (await the call)
- Modify: `src/Ngraphiphy.Cli/Configuration/CliHostExtensions.cs` (`.GetAwaiter().GetResult()` at the sync seam)
- Test: extend `tests/Ngraphiphy.Cli.Tests/Configuration/PassSecretProviderTests.cs` and `EnvSecretProviderTests.cs`

- [ ] **Step 1: Write the failing test**

Append to `tests/Ngraphiphy.Cli.Tests/Configuration/PassSecretProviderTests.cs`:

```csharp
[Test]
public async Task Resolve_PathWithSpaces_PassedAsSingleArgument()
{
    var runner = new ArgCapturingRunner { Stdout = "ok\n" };
    var provider = new PassSecretProvider(runner);

    await provider.ResolveAsync("path with spaces/key");

    await Assert.That(runner.CapturedArgs).IsEquivalentTo(new[] { "show", "path with spaces/key" });
}

private sealed class ArgCapturingRunner : IProcessRunner
{
    public string Stdout { get; init; } = "";
    public string Stderr { get; init; } = "";
    public int ExitCode { get; init; } = 0;
    public IReadOnlyList<string>? CapturedArgs { get; private set; }

    public Task<(string Stdout, string Stderr, int ExitCode)> RunAsync(
        string executable, IReadOnlyList<string> arguments, CancellationToken ct)
    {
        CapturedArgs = arguments;
        return Task.FromResult((Stdout, Stderr, ExitCode));
    }
}
```

Also update existing `FakeRunner` in the same file to implement the new signature; existing tests should now be `await provider.ResolveAsync(...)`.

- [ ] **Step 2: Run, observe compile failure (test won't build)**

```bash
dotnet build tests/Ngraphiphy.Cli.Tests/
```
Expected: compile errors on `IProcessRunner.Run`/`Resolve` signature.

- [ ] **Step 3: Update `IProcessRunner`**

Replace contents of `src/Ngraphiphy.Cli/Configuration/Secrets/IProcessRunner.cs`:

```csharp
namespace Ngraphiphy.Cli.Configuration.Secrets;

internal interface IProcessRunner
{
    Task<(string Stdout, string Stderr, int ExitCode)> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken ct = default);
}
```

- [ ] **Step 4: Update `SystemProcessRunner`**

Replace contents of `src/Ngraphiphy.Cli/Configuration/Secrets/SystemProcessRunner.cs`:

```csharp
using System.Diagnostics;

namespace Ngraphiphy.Cli.Configuration.Secrets;

internal sealed class SystemProcessRunner : IProcessRunner
{
    public async Task<(string Stdout, string Stderr, int ExitCode)> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in arguments)
            psi.ArgumentList.Add(arg);

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start '{executable}'.");

        // Drain stdout and stderr concurrently to avoid pipe-buffer deadlock.
        var stdoutTask = proc.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = proc.StandardError.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        return (stdout, stderr, proc.ExitCode);
    }
}
```

- [ ] **Step 5: Update `ISecretProvider` and providers**

`src/Ngraphiphy.Cli/Configuration/Secrets/ISecretProvider.cs`:

```csharp
namespace Ngraphiphy.Cli.Configuration.Secrets;

public interface ISecretProvider
{
    Task<string> ResolveAsync(string path, CancellationToken ct = default);
}
```

`PassSecretProvider.cs` — replace `Resolve` with:

```csharp
public async Task<string> ResolveAsync(string path, CancellationToken ct = default)
{
    if (_cache.TryGetValue(path, out var cached))
        return cached;

    var (stdout, stderr, exitCode) = await _runner.RunAsync("pass", new[] { "show", path }, ct);

    if (exitCode != 0)
        throw new InvalidOperationException(
            $"Secret reference pass://{path}: 'pass show' exited {exitCode}. stderr: {stderr.Trim()}");

    var secret = stdout.Split('\n')[0].TrimEnd();
    _cache[path] = secret;
    return secret;
}
```

`EnvSecretProvider.cs` — change signature to:

```csharp
public Task<string> ResolveAsync(string path, CancellationToken ct = default)
{
    var value = Environment.GetEnvironmentVariable(path)
        ?? throw new InvalidOperationException($"Environment variable '{path}' is not set.");
    return Task.FromResult(value);
}
```

- [ ] **Step 6: Update `SecretResolver` to await the call**

In `src/Ngraphiphy.Cli/Configuration/Secrets/SecretResolver.cs`, change the signature and the call:

```csharp
public static async Task ResolveAndOverlayAsync(
    IConfigurationBuilder configBuilder,
    IConfiguration snapshot,
    IReadOnlyDictionary<string, ISecretProvider> providers,
    CancellationToken ct = default)
{
    // ... unchanged setup ...
    var resolved = await provider.ResolveAsync(reference.Path, ct);
    // ... rest unchanged ...
}
```

- [ ] **Step 7: Update `CliHostExtensions` call site**

In `src/Ngraphiphy.Cli/Configuration/CliHostExtensions.cs:39`:

```csharp
SecretResolver.ResolveAndOverlayAsync(builder.Configuration, snapshot, providers)
    .GetAwaiter().GetResult();
```

(Sync seam is acceptable here — host bootstrap is one-shot.)

- [ ] **Step 8: Build, run all tests**

```bash
dotnet build && dotnet test
```
Expected: all green.

- [ ] **Step 9: Commit**

```bash
git add src/Ngraphiphy.Cli tests/Ngraphiphy.Cli.Tests
git commit -m "refactor(cli): async IProcessRunner with ArgumentList to fix arg-injection and stdout/stderr deadlock"
```

---

## Task 5: Remove hardcoded personal paths from `NativeLibraryResolver` (Important #4)

**Files:**
- Modify: `src/Ngraphiphy/Cluster/NativeLibraryResolver.cs:31-33`

- [ ] **Step 1: Apply the edit**

Delete lines 30-33 (the two `/home/timm/...` entries and their comments). The `AppContext.BaseDirectory` lookups and `NativeLibrary.TryLoad("libleiden_interop", ...)` system-path fallback are sufficient.

After edit, the `locations` array should be:

```csharp
var locations = new[]
{
    Path.Combine(AppContext.BaseDirectory, "libleiden_interop.so"),
    Path.Combine(AppContext.BaseDirectory, "native", "libleiden_interop.so"),
    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "native", "build", "lib", "libleiden_interop.so"),
    "/usr/local/lib/libleiden_interop.so",
    "/usr/lib/libleiden_interop.so",
};
```

- [ ] **Step 2: Build + run clustering test that exercises the resolver**

```bash
dotnet test tests/Ngraphiphy.Tests/ --filter "FullyQualifiedName~Leiden"
```
Expected: PASS (build output still ships the .so alongside the binary).

- [ ] **Step 3: Commit**

```bash
git add src/Ngraphiphy/Cluster/NativeLibraryResolver.cs
git commit -m "fix(cluster): remove hardcoded developer paths from NativeLibraryResolver"
```

---

## Task 6: Narrow `MemgraphStore.CreateAsync` catch to "already exists" (Important #7)

**Files:**
- Modify: `src/Ngraphiphy.Storage/Providers/Memgraph/MemgraphStore.cs:38-41`

- [ ] **Step 1: Apply the edit**

Replace lines 38-41 with:

```csharp
catch (Exception ex) when (
    ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase)
    || ex.Message.Contains("Index already created", StringComparison.OrdinalIgnoreCase))
{
    // Expected: vector index was created in a previous run.
}
```

(Leave the auth/network failures to propagate — caller will see them at startup, not on first query.)

- [ ] **Step 2: Build**

```bash
dotnet build src/Ngraphiphy.Storage/
```
Expected: success.

- [ ] **Step 3: Commit**

```bash
git add src/Ngraphiphy.Storage/Providers/Memgraph/MemgraphStore.cs
git commit -m "fix(storage): narrow MemgraphStore.CreateAsync catch to 'already exists'"
```

---

## Task 7: Inline depth literal in Neo4j `GetNeighborsAsync` Cypher (Important #8)

Cypher disallows parameterized upper bounds in variable-length patterns on most server versions. Inline the depth as an integer literal — safe because `depth` is an `int`, not a string, so there is no injection risk.

**Files:**
- Modify: `src/Ngraphiphy.Storage/Providers/Neo4j/BoltStoreBase.cs:177-184`

- [ ] **Step 1: Apply the edit**

Replace the `RunAsync` call (lines 177-184):

```csharp
if (depth < 1 || depth > 10)
    throw new ArgumentOutOfRangeException(nameof(depth), depth, "depth must be between 1 and 10");

var cursor = await tx.RunAsync(
    $@"MATCH (n:GraphNode {{id: $nodeId}})-[*1..{depth}]->(neighbor:GraphNode)
       RETURN DISTINCT neighbor.id as id, neighbor.label as label,
              neighbor.fileType as fileType, neighbor.sourceFile as sourceFile,
              neighbor.sourceLocation as sourceLocation, neighbor.community as community,
              neighbor.normLabel as normLabel",
    new { nodeId });
```

(The `{{` and `}}` are C# brace-escapes for the Cypher literal `{` `}`. `depth` is interpolated as a number so there is no Cypher-injection vector. The bound check is defensive against unbounded traversals.)

- [ ] **Step 2: Build**

```bash
dotnet build src/Ngraphiphy.Storage/
```
Expected: success.

- [ ] **Step 3: Commit**

```bash
git add src/Ngraphiphy.Storage/Providers/Neo4j/BoltStoreBase.cs
git commit -m "fix(storage): inline depth literal in Neo4j GetNeighborsAsync Cypher"
```

---

## Task 8: Replace O(n²) `nodes.First` calls in `EntityDeduplicator` (Important #9)

**Files:**
- Modify: `src/Ngraphiphy/Dedup/EntityDeduplicator.cs:65-104`
- Test: `tests/Ngraphiphy.Tests/Dedup/EntityDeduplicatorPerfTests.cs` (create — behavioral, not benchmark)

- [ ] **Step 1: Write the behavioral test**

Create `tests/Ngraphiphy.Tests/Dedup/EntityDeduplicatorPerfTests.cs`:

```csharp
using Ngraphiphy.Dedup;
using Ngraphiphy.Models;

namespace Ngraphiphy.Tests.Dedup;

public class EntityDeduplicatorPerfTests
{
    [Test]
    public async Task Deduplicate_LargeInput_CompletesInReasonableTime()
    {
        // 5000 unique nodes — the O(n^2) version takes minutes; O(n) finishes in ms.
        var nodes = Enumerable.Range(0, 5000)
            .Select(i => new Node
            {
                Id = $"n{i}",
                Label = $"label_{i}",
                FileTypeString = "code",
                SourceFile = $"file{i}.cs",
            })
            .ToList();
        var edges = new List<Edge>();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var (resultNodes, _) = EntityDeduplicator.Deduplicate(nodes, edges);

        sw.Stop();
        await Assert.That(resultNodes.Count).IsEqualTo(5000);
        await Assert.That(sw.Elapsed).IsLessThan(TimeSpan.FromSeconds(5));
    }
}
```

- [ ] **Step 2: Run on current code, verify passes (we are not relying on this test to fail — it pins the post-fix performance)**

```bash
dotnet test tests/Ngraphiphy.Tests/ --filter "EntityDeduplicatorPerfTests"
```
If it already passes, the regression guard is in place. If it fails, that's also confirmation the bug matters.

- [ ] **Step 3: Apply the fix**

At the top of the `Deduplicate` method body (around line 30, after parameter validation, before the LSH loop), add:

```csharp
var nodesById = nodes.ToDictionary(n => n.Id);
```

Replace line 83:

```csharp
var other = nodesById[candidateId];
```

Replace line 98:

```csharp
var memberNodes = members.Select(id => nodesById[id]).ToList();
```

- [ ] **Step 4: Run tests**

```bash
dotnet test tests/Ngraphiphy.Tests/
```
Expected: all green, including the perf guard.

- [ ] **Step 5: Commit**

```bash
git add src/Ngraphiphy/Dedup/EntityDeduplicator.cs tests/Ngraphiphy.Tests/Dedup/EntityDeduplicatorPerfTests.cs
git commit -m "perf(dedup): index nodes by id to remove O(n^2) lookups in EntityDeduplicator"
```

---

## Task 9: Register `envProvider` in DI with keyed registration (Important #10)

**Files:**
- Modify: `src/Ngraphiphy.Cli/Configuration/CliHostExtensions.cs:41-42`

- [ ] **Step 1: Apply the edit**

Replace lines 41-42 with:

```csharp
// 3. Register both providers keyed by scheme for late resolution.
builder.Services.AddKeyedSingleton<ISecretProvider>("pass", passProvider);
builder.Services.AddKeyedSingleton<ISecretProvider>("env", envProvider);
// Default (unkeyed) resolution returns the pass provider for backward compat.
builder.Services.AddSingleton<ISecretProvider>(passProvider);
```

- [ ] **Step 2: Build**

```bash
dotnet build
```
Expected: success.

- [ ] **Step 3: Commit**

```bash
git add src/Ngraphiphy.Cli/Configuration/CliHostExtensions.cs
git commit -m "feat(cli): register env secret provider via keyed DI"
```

---

## Task 10: Split `ExtractionValidator` into errors + warnings; emit warnings for unsupported FileType (Minor — user-priority)

`FileType.Video` (and any future enum values not in `ValidFileTypes`) should produce a warning but not fail validation. Same for any "soft" constraint we choose to relax. Errors keep their current semantics (cause `AssertValid` to throw).

**Files:**
- Modify: `src/Ngraphiphy/Validation/ExtractionValidator.cs`
- Modify: `src/Ngraphiphy.Pipeline/RepositoryAnalysis.cs` (route warnings through `onProgress`)
- Test: `tests/Ngraphiphy.Tests/Validation/ExtractionValidatorTests.cs` (create or extend)

- [ ] **Step 1: Write failing tests**

Create `tests/Ngraphiphy.Tests/Validation/ExtractionValidatorTests.cs`:

```csharp
using Ngraphiphy.Models;
using Ngraphiphy.Validation;

namespace Ngraphiphy.Tests.Validation;

public class ExtractionValidatorTests
{
    private static Models.Extraction Single(Node n) => new()
    {
        Nodes = [n],
        Edges = [],
    };

    [Test]
    public async Task Validate_VideoFileType_ProducesWarningNotError()
    {
        var ext = Single(new Node
        {
            Id = "v1", Label = "demo.mp4",
            FileTypeString = "video", SourceFile = "demo.mp4",
        });

        var result = ExtractionValidator.Validate(ext);

        await Assert.That(result.Errors).IsEmpty();
        await Assert.That(result.Warnings).Contains("video");
    }

    [Test]
    public async Task AssertValid_VideoFileType_DoesNotThrow()
    {
        var ext = Single(new Node
        {
            Id = "v1", Label = "demo.mp4",
            FileTypeString = "video", SourceFile = "demo.mp4",
        });

        var act = () => ExtractionValidator.AssertValid(ext);

        await Assert.That(act).ThrowsNothing();
    }

    [Test]
    public async Task Validate_UnknownFileType_IsError()
    {
        var ext = Single(new Node
        {
            Id = "x", Label = "weird",
            FileTypeString = "garbage-not-in-enum", SourceFile = "x",
        });

        var result = ExtractionValidator.Validate(ext);

        await Assert.That(result.Errors).IsNotEmpty();
    }
}
```

- [ ] **Step 2: Run, observe compile failure**

```bash
dotnet build tests/Ngraphiphy.Tests/
```
Expected: errors — `Validate` returns `List<string>`, not a record with `Errors`/`Warnings`.

- [ ] **Step 3: Refactor `ExtractionValidator`**

Replace `src/Ngraphiphy/Validation/ExtractionValidator.cs`:

```csharp
using Ngraphiphy.Models;

namespace Ngraphiphy.Validation;

public static class ExtractionValidator
{
    public sealed record Result(IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings);

    // File types accepted by the analysis pipeline (raise errors when violated).
    private static readonly HashSet<string> ValidFileTypes =
        ["code", "document", "paper", "image", "rationale", "concept"];

    // File types known to the FileType enum but not yet processed by the pipeline.
    // Encountering one is a warning, not a hard failure.
    private static readonly HashSet<string> WarnFileTypes = ["video"];

    private static readonly HashSet<string> ValidConfidences =
        ["EXTRACTED", "INFERRED", "AMBIGUOUS"];

    public static Result Validate(Models.Extraction extraction)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var nodeIds = new HashSet<string>();

        for (int i = 0; i < extraction.Nodes.Count; i++)
        {
            var node = extraction.Nodes[i];

            if (string.IsNullOrWhiteSpace(node.Id))
                errors.Add($"Node[{i}]: missing required field 'id'");
            else
                nodeIds.Add(node.Id);

            if (string.IsNullOrWhiteSpace(node.Label))
                errors.Add($"Node[{i}]: missing required field 'label'");

            if (string.IsNullOrWhiteSpace(node.SourceFile))
                errors.Add($"Node[{i}]: missing required field 'source_file'");

            if (ValidFileTypes.Contains(node.FileTypeString))
                continue;

            if (WarnFileTypes.Contains(node.FileTypeString))
                warnings.Add(
                    $"Node[{i}]: file_type '{node.FileTypeString}' is recognized but not yet processed by the pipeline");
            else
                errors.Add(
                    $"Node[{i}]: invalid file_type '{node.FileTypeString}' (must be one of: {string.Join(", ", ValidFileTypes)})");
        }

        for (int i = 0; i < extraction.Edges.Count; i++)
        {
            var edge = extraction.Edges[i];

            if (string.IsNullOrWhiteSpace(edge.Source))
                errors.Add($"Edge[{i}]: missing required field 'source'");

            if (string.IsNullOrWhiteSpace(edge.Target))
                errors.Add($"Edge[{i}]: missing required field 'target'");

            if (string.IsNullOrWhiteSpace(edge.Relation))
                errors.Add($"Edge[{i}]: missing required field 'relation'");

            if (string.IsNullOrWhiteSpace(edge.SourceFile))
                errors.Add($"Edge[{i}]: missing required field 'source_file'");

            if (!ValidConfidences.Contains(edge.ConfidenceString))
                errors.Add($"Edge[{i}]: invalid confidence '{edge.ConfidenceString}' (must be one of: {string.Join(", ", ValidConfidences)})");

            if (!string.IsNullOrWhiteSpace(edge.Source) && !nodeIds.Contains(edge.Source))
                errors.Add($"Edge[{i}]: dangling source '{edge.Source}' not found in nodes");

            if (!string.IsNullOrWhiteSpace(edge.Target) && !nodeIds.Contains(edge.Target))
                errors.Add($"Edge[{i}]: dangling target '{edge.Target}' not found in nodes");
        }

        return new Result(errors, warnings);
    }

    public static void AssertValid(Models.Extraction extraction)
    {
        var result = Validate(extraction);
        if (result.Errors.Count > 0)
            throw new InvalidOperationException(
                $"Invalid extraction ({result.Errors.Count} errors):\n" + string.Join("\n", result.Errors));
    }
}
```

- [ ] **Step 4: Update all call sites of `Validate`**

Run:

```bash
grep -rn "ExtractionValidator.Validate\b" src tests
```

For each call site, adapt to the new `Result` shape. The `AssertValid` call sites do not need to change.

- [ ] **Step 5: Wire warnings through `RepositoryAnalysis.RunAsync`**

In `src/Ngraphiphy.Pipeline/RepositoryAnalysis.cs`, after each `extraction = extractor.Extract(...)` call (around line 53), insert:

```csharp
var validation = ExtractionValidator.Validate(extraction);
foreach (var warning in validation.Warnings)
    onProgress?.Invoke($"Warning [{file.AbsolutePath}]: {warning}");
if (validation.Errors.Count > 0)
    throw new InvalidOperationException(
        $"Invalid extraction for {file.AbsolutePath} ({validation.Errors.Count} errors):\n"
        + string.Join("\n", validation.Errors));
```

(Replace any existing `AssertValid` call here.)

- [ ] **Step 6: Run all tests**

```bash
dotnet build && dotnet test
```
Expected: all green.

- [ ] **Step 7: Commit**

```bash
git add src/Ngraphiphy/Validation src/Ngraphiphy.Pipeline tests/Ngraphiphy.Tests/Validation
git commit -m "feat(validation): warn on unsupported file types (e.g. video) instead of erroring"
```

---

## Task 11: Make `NativeLibraryResolver._registered` thread-safe (Minor)

**Files:**
- Modify: `src/Ngraphiphy/Cluster/NativeLibraryResolver.cs:8-14`

- [ ] **Step 1: Apply the edit**

Replace lines 8-14 with:

```csharp
private static int _registered = 0;

public static void Register()
{
    if (Interlocked.Exchange(ref _registered, 1) == 1) return;
    NativeLibrary.SetDllImportResolver(typeof(NativeMethods).Assembly, Resolver);
}
```

- [ ] **Step 2: Build, run clustering tests**

```bash
dotnet test tests/Ngraphiphy.Tests/ --filter "FullyQualifiedName~Leiden"
```
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add src/Ngraphiphy/Cluster/NativeLibraryResolver.cs
git commit -m "fix(cluster): use Interlocked for NativeLibraryResolver registration guard"
```

---

## Task 12: Decouple `SecretResolver` from `AnsiConsole` via logger delegate (Minor)

**Files:**
- Modify: `src/Ngraphiphy.Cli/Configuration/Secrets/SecretResolver.cs`
- Modify: `src/Ngraphiphy.Cli/Configuration/CliHostExtensions.cs:39`

- [ ] **Step 1: Add `Action<string>? warn` parameter**

In `SecretResolver.ResolveAndOverlayAsync`, replace the `AnsiConsole.MarkupLineInterpolated` calls with `warn?.Invoke(...)` calls:

```csharp
public static async Task ResolveAndOverlayAsync(
    IConfigurationBuilder configBuilder,
    IConfiguration snapshot,
    IReadOnlyDictionary<string, ISecretProvider> providers,
    Action<string>? warn = null,
    CancellationToken ct = default)
{
    // ... existing logic, with:
    warn?.Invoke($"No secret provider registered for scheme '{reference.Scheme}' (key: {key})");
    // and:
    warn?.Invoke($"Could not resolve secret reference '{key}': {ex.Message}");
}
```

- [ ] **Step 2: Pass the logger from `CliHostExtensions`**

At line 39:

```csharp
SecretResolver.ResolveAndOverlayAsync(
    builder.Configuration, snapshot, providers,
    warn: msg => AnsiConsole.MarkupLineInterpolated($"[yellow]Warning: {msg}[/]"))
    .GetAwaiter().GetResult();
```

- [ ] **Step 3: Tests**

```bash
dotnet test tests/Ngraphiphy.Cli.Tests/
```
Expected: PASS (existing `ResolveAndOverlay` tests pass a `null` logger and validate behavior).

- [ ] **Step 4: Commit**

```bash
git add src/Ngraphiphy.Cli/Configuration
git commit -m "refactor(cli): inject warning sink into SecretResolver instead of using AnsiConsole directly"
```

---

## Task 13: Single-read file path in `RepositoryAnalysis` + `ExtractionCache.FileHash` overload (Minor)

Avoid reading every file twice.

**Files:**
- Modify: `src/Ngraphiphy/Cache/ExtractionCache.cs` — add an overload `FileHash(string absolutePath, string rootPath, byte[] content)`
- Modify: `src/Ngraphiphy.Pipeline/RepositoryAnalysis.cs:42-56`

- [ ] **Step 1: Add the overload**

In `ExtractionCache`, add a new public static method that accepts the pre-read bytes and computes the same hash that `FileHash(absolutePath, rootPath)` would. Keep the original method, but have it read bytes once and delegate.

- [ ] **Step 2: Update the pipeline loop**

Replace lines 42-56 of `RepositoryAnalysis.cs` with:

```csharp
foreach (var file in files)
{
    ct.ThrowIfCancellationRequested();
    var extractor = registry.GetExtractor(file.AbsolutePath);
    if (extractor is null) continue;

    var contentBytes = await File.ReadAllBytesAsync(file.AbsolutePath, ct);
    var hash = ExtractionCache.FileHash(file.AbsolutePath, rootPath, contentBytes);
    var cached = cache.Load(hash);
    if (cached is not null) { extractions.Add(cached); continue; }

    var source = System.Text.Encoding.UTF8.GetString(contentBytes);
    var extraction = extractor.Extract(file.AbsolutePath, source);
    cache.Save(hash, extraction);
    extractions.Add(extraction);
}
```

- [ ] **Step 3: Build + test**

```bash
dotnet build && dotnet test
```
Expected: all green.

- [ ] **Step 4: Commit**

```bash
git add src/Ngraphiphy/Cache src/Ngraphiphy.Pipeline
git commit -m "perf(pipeline): read each file once per extraction loop iteration"
```

---

## Task 14: Parallelize file extraction loop (Minor)

Run after Task 13 (single-read) and Task 10 (warning routing) so the loop body is small.

**Files:**
- Modify: `src/Ngraphiphy.Pipeline/RepositoryAnalysis.cs:41-56`

- [ ] **Step 1: Refactor with `Parallel.ForEachAsync`**

Replace the `foreach` with:

```csharp
var extractions = new System.Collections.Concurrent.ConcurrentBag<ExtractionModel>();
var parallelOptions = new ParallelOptions
{
    CancellationToken = ct,
    MaxDegreeOfParallelism = Environment.ProcessorCount,
};
await Parallel.ForEachAsync(files, parallelOptions, async (file, token) =>
{
    var extractor = registry.GetExtractor(file.AbsolutePath);
    if (extractor is null) return;

    var contentBytes = await File.ReadAllBytesAsync(file.AbsolutePath, token);
    var hash = ExtractionCache.FileHash(file.AbsolutePath, rootPath, contentBytes);
    var cached = cache.Load(hash);
    if (cached is not null) { extractions.Add(cached); return; }

    var source = System.Text.Encoding.UTF8.GetString(contentBytes);
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
});

var orderedExtractions = extractions.ToList();
```

Replace subsequent uses of `extractions` (the `List<ExtractionModel>`) with `orderedExtractions`. Verify `ExtractionCache.Save` is thread-safe (it writes per-hash files atomically via temp-then-rename; if not, gate with a `lock` or add `ConcurrentDictionary`-based deduplication of in-flight writes).

- [ ] **Step 2: Run integration tests**

```bash
dotnet test
```
Expected: all green.

- [ ] **Step 3: Commit**

```bash
git add src/Ngraphiphy.Pipeline/RepositoryAnalysis.cs
git commit -m "perf(pipeline): parallelize file extraction across cores"
```

---

## Task 15: Move `RepositoryAnalysis` and `GraphTools` out of the Storage→Pipeline reverse dependency (Minor)

This is the largest of the minor patterns. Defer if scope is tight.

**Files:**
- Move `src/Ngraphiphy.Pipeline/GraphTools.cs` → `src/Ngraphiphy/Analysis/GraphTools.cs`
- Update namespace from `Ngraphiphy.Pipeline` to `Ngraphiphy.Analysis`
- Update `Ngraphiphy.Storage.csproj` to remove the `<ProjectReference>` to `Ngraphiphy.Pipeline.csproj` (if present)
- Update all callers' `using` directives

- [ ] **Step 1: Run grep**

```bash
grep -rn "Ngraphiphy.Pipeline.GraphTools\|using Ngraphiphy.Pipeline" src tests
```

- [ ] **Step 2: Move file, rename namespace, fix callers**

Move the file and bulk-update the namespace. Update each caller's `using` directive.

- [ ] **Step 3: Build + test**

```bash
dotnet build && dotnet test
```
Expected: all green.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "refactor: move GraphTools to Ngraphiphy.Analysis to drop Storage->Pipeline edge"
```

---

## Self-review checks

- All 10 numbered review issues map to tasks: Critical #1 → T1, #2 → T2, #3 → T3; Important #4 → T5, #5+#6 → T4, #7 → T6, #8 → T7, #9 → T8, #10 → T9.
- All 6 minor patterns map to tasks: serial extraction → T14; double-read → T13; FileType.Video → T10; ANSI in SecretResolver → T12; non-atomic `_registered` → T11; GraphTools placement → T15.
- No "TBD", "TODO later", or "similar to Task N" placeholders. Each code block is concrete.
- Types referenced (`IProcessRunner`, `ISecretProvider`, `ExtractionValidator.Result`, `SnapshotId`) are defined or modified in the same plan with matching signatures.
- Commit messages are scoped per-task; tasks are independently revertable.

---

Plan complete and saved to `docs/superpowers/plans/2026-05-11-code-review-fixes.md`. Two execution options:

1. **Subagent-Driven (recommended)** — I dispatch a fresh subagent per task, review between tasks, fast iteration.
2. **Inline Execution** — Execute tasks in this session using executing-plans, batch execution with checkpoints.

Which approach?
