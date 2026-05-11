# Ngraphiphy MSBuild Integration

Automatically analyze your codebase during the build process using MSBuild targets.

## Repo-Level Integration (Dogfooding)

The Ngraphiphy repository uses `Directory.Build.targets` at the root to analyze itself before each build.

### How It Works

- Runs `ngraphiphy analyze` before the build starts
- Only analyzes when source files change (incremental builds)
- Runs exactly once per build via the core `Ngraphiphy` project
- Cache stored in `.ngraphiphy-cache/` (git-ignored)

### Configuration

**Opt out per project:**
```xml
<PropertyGroup>
  <NgraphiphyEnabled>false</NgraphiphyEnabled>
</PropertyGroup>
```

## Distributable Package

Install the `Ngraphiphy.MSBuild` NuGet package in any .NET project to enable analysis.

### Installation

```bash
dotnet add package Ngraphiphy.MSBuild
```

Or via NuGet Package Manager:
```
Install-Package Ngraphiphy.MSBuild
```

### Prerequisites

The `ngraphiphy-cli` tool must be available:

```bash
dotnet tool install -g Ngraphiphy.Cli
```

Or for local tool restore, add to `dotnet-tools.json`:
```json
{
  "version": 1,
  "isRoot": true,
  "tools": {
    "Ngraphiphy.Cli": {
      "version": "latest"
    }
  }
}
```

### How It Works

- Runs `dotnet tool run ngraphiphy-cli analyze` before each build
- Only analyzes when source files change
- Cache stored in `.ngraphiphy-cache/` (should be git-ignored)

### Configuration

**Opt out per project:**
```xml
<PropertyGroup>
  <NgraphiphyEnabled>false</NgraphiphyEnabled>
</PropertyGroup>
```

**Override cache directory:**
```xml
<PropertyGroup>
  <NgraphiphyCacheDir>$(MSBuildProjectDirectory)/.ngraphiphy-cache</NgraphiphyCacheDir>
</PropertyGroup>
```

## Implementation Details

### Target Behavior

- **Target:** `NgraphiphyAnalyze`
- **Runs:** Before `Build` target
- **Incremental:** Skipped if source files haven't changed since last run
- **Concurrency:** Safe with parallel builds (stamp file prevents race conditions)
- **Failures:** Non-fatal (analysis errors don't fail the build)

### Preventing Nested Runs

The implementation prevents analysis from recursively spawning:
1. Only the core `Ngraphiphy` project triggers analysis in the repo
2. External projects using the NuGet package always trigger analysis
3. The `NGRAPHIPHY_ANALYZING` environment variable prevents recursive invocations
4. The `Ngraphiphy.Cli` and `Ngraphiphy.MSBuild` projects are explicitly excluded

### Cache Location

- **Repo:** `.ngraphiphy-cache/` at root
- **NuGet:** `.ngraphiphy-cache/` in project directory (configurable)

Add to `.gitignore`:
```
.ngraphiphy-cache/
```

## Troubleshooting

**Analysis not running:**
- Check that source files have been modified since last build
- Verify `.ngraphiphy-cache/.msbuild-stamp` exists
- Delete stamp file to force re-analysis: `rm .ngraphiphy-cache/.msbuild-stamp`

**Build failures:**
- Analysis failures don't block builds (see `ContinueOnError="true"`)
- Check `ngraphiphy-cli` is installed: `dotnet tool list -g`
- Verify `ngraphiphy-cli` can analyze: `dotnet tool run ngraphiphy-cli analyze .`

**Performance issues:**
- Analysis runs asynchronously in background; initial runs may take time
- Cache is reused between builds—subsequent runs should be faster
- For large repos, set `<NgraphiphyEnabled>false</NgraphiphyEnabled>` if analysis is too slow

## What Gets Analyzed

Ngraphiphy analyzes:
- Source code in 9 languages (C#, Java, Python, JavaScript, TypeScript, Go, Rust, C++, C)
- Respects `.gitignore` files at any depth
- Blocks sensitive files (private keys, credentials)

For details, see [codebase documentation](../README.md).
