# Graphiphy MSBuild Integration

Automatically analyze your codebase during the build process using MSBuild targets.

## Repo-Level Integration (Dogfooding)

The Graphiphy repository uses `Directory.Build.targets` at the root to analyze itself before each build.

### How It Works

- Runs `graphiphy analyze` before the build starts
- Only analyzes when source files change (incremental builds)
- Runs exactly once per build via the core `Graphiphy` project
- Cache stored in `.graphiphy-cache/` (git-ignored)

### Configuration

**Opt out per project:**
```xml
<PropertyGroup>
  <GraphiphyEnabled>false</GraphiphyEnabled>
</PropertyGroup>
```

## Distributable Package

Install the `Graphiphy.MSBuild` NuGet package in any .NET project to enable analysis.

### Installation

```bash
dotnet add package Graphiphy.MSBuild
```

Or via NuGet Package Manager:
```
Install-Package Graphiphy.MSBuild
```

### Prerequisites

The `graphiphy-cli` tool must be available:

```bash
dotnet tool install -g Graphiphy.Cli
```

Or for local tool restore, add to `dotnet-tools.json`:
```json
{
  "version": 1,
  "isRoot": true,
  "tools": {
    "Graphiphy.Cli": {
      "version": "latest"
    }
  }
}
```

### How It Works

- Runs `dotnet tool run graphiphy-cli analyze` before each build
- Only analyzes when source files change
- Cache stored in `.graphiphy-cache/` (should be git-ignored)

### Configuration

**Opt out per project:**
```xml
<PropertyGroup>
  <GraphiphyEnabled>false</GraphiphyEnabled>
</PropertyGroup>
```

**Override cache directory:**
```xml
<PropertyGroup>
  <GraphiphyCacheDir>$(MSBuildProjectDirectory)/.graphiphy-cache</GraphiphyCacheDir>
</PropertyGroup>
```

## Implementation Details

### Target Behavior

- **Target:** `GraphiphyAnalyze`
- **Runs:** Before `Build` target
- **Incremental:** Skipped if source files haven't changed since last run
- **Concurrency:** Safe with parallel builds (stamp file prevents race conditions)
- **Failures:** Non-fatal (analysis errors don't fail the build)

### Preventing Nested Runs

The implementation prevents analysis from recursively spawning:
1. Only the core `Graphiphy` project triggers analysis in the repo
2. External projects using the NuGet package always trigger analysis
3. The `GRAPHIPHY_ANALYZING` environment variable prevents recursive invocations
4. The `Graphiphy.Cli` and `Graphiphy.MSBuild` projects are explicitly excluded

### Cache Location

- **Repo:** `.graphiphy-cache/` at root
- **NuGet:** `.graphiphy-cache/` in project directory (configurable)

Add to `.gitignore`:
```
.graphiphy-cache/
```

## Troubleshooting

**Analysis not running:**
- Check that source files have been modified since last build
- Verify `.graphiphy-cache/.msbuild-stamp` exists
- Delete stamp file to force re-analysis: `rm .graphiphy-cache/.msbuild-stamp`

**Build failures:**
- Analysis failures don't block builds (see `ContinueOnError="true"`)
- Check `graphiphy-cli` is installed: `dotnet tool list -g`
- Verify `graphiphy-cli` can analyze: `dotnet tool run graphiphy-cli analyze .`

**Performance issues:**
- Analysis runs asynchronously in background; initial runs may take time
- Cache is reused between builds—subsequent runs should be faster
- For large repos, set `<GraphiphyEnabled>false</GraphiphyEnabled>` if analysis is too slow

## What Gets Analyzed

Graphiphy analyzes:
- Source code in 9 languages (C#, Java, Python, JavaScript, TypeScript, Go, Rust, C++, C)
- Respects `.gitignore` files at any depth
- Blocks sensitive files (private keys, credentials)

For details, see [codebase documentation](../README.md).
