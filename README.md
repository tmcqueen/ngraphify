# Ngraphiphy

A .NET 10 codebase analysis tool that builds knowledge graphs from source code using TreeSitter extraction and graph clustering. Analyze repositories, discover dependencies, and ask LLM questions about your codebase architecture.

## What It Does

Ngraphiphy parses source files in **9 languages** (C#, Java, Python, JavaScript, TypeScript, Go, Rust, C++, C) via TreeSitter, builds a **knowledge graph** using QuikGraph with Leiden clustering, and exposes analysis through:

- **CLI commands** — analyze, report, query, and serve
- **MCP server** — integrate with Claude and other AI clients
- **MSBuild integration** — automatically analyze during .NET builds

## Features

- **AST Extraction** — 9 languages, respects `.gitignore`, blocks sensitive files
- **Knowledge Graph** — nodes (entities), edges (relationships), Leiden clustering (optional)
- **Graph Analysis** — god nodes (highly connected), surprising connections, community detection
- **LLM Integration** — 5 providers (Anthropic, OpenAI, Ollama, Copilot, A2A)
- **MCP Server** — use with Claude Desktop, Cursor, and other MCP clients
- **Caching** — incremental analysis, `.ngraphiphy-cache/` directory
- **Markdown Reports** — detailed codebase summaries and visualizations

## Quick Start

### Installation

Install the CLI globally:

```bash
dotnet tool install -g Ngraphiphy.Cli
```

Or build from source:

```bash
git clone https://github.com/timm/ngraphify
cd ngraphify
dotnet build src/Ngraphiphy.Cli
```

### First Command

Analyze a repository:

```bash
ngraphiphy-cli analyze /path/to/repo
```

Output:

```
Metric                Value
─────────────────────────────
Files detected        42
Nodes (entities)      156
Edges (relations)     284
Top entity            UserService (Services/UserService.cs)
```

Generate a Markdown report:

```bash
ngraphiphy-cli report /path/to/repo --out analysis.md
```

Query with an LLM (requires API key):

```bash
export ANTHROPIC_API_KEY=sk-ant-...
ngraphiphy-cli query /path/to/repo "What are the main dependencies in this codebase?"
```

Start the MCP server:

```bash
ngraphiphy-cli serve /path/to/repo
```

Then add to Claude Desktop (`claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "ngraphiphy": {
      "command": "/path/to/ngraphiphy-cli",
      "args": ["serve", "/path/to/repo"]
    }
  }
}
```

## Requirements

- **.NET 10 runtime** — [download](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- **TreeSitter native libraries** — included in distribution
- **LLM API key** (optional) — for the `query` command

## Installation Methods

### 1. Global .NET Tool

Fastest and recommended:

```bash
dotnet tool install -g Ngraphiphy.Cli
```

Update:

```bash
dotnet tool update -g Ngraphiphy.Cli
```

Uninstall:

```bash
dotnet tool uninstall -g Ngraphiphy.Cli
```

### 2. Local Tool (via dotnet-tools.json)

In your repository root:

```bash
dotnet new tool-manifest  # if not already created
dotnet tool install Ngraphiphy.Cli
```

Run:

```bash
dotnet tool run ngraphiphy-cli analyze .
```

### 3. Build from Source

```bash
git clone https://github.com/timm/ngraphify
cd ngraphify
dotnet publish src/Ngraphiphy.Cli -c Release -o ./dist
./dist/ngraphiphy-cli analyze /path/to/repo
```

### 4. NuGet Package (MSBuild Integration)

Install in any .NET project:

```bash
dotnet add package Ngraphiphy.MSBuild
```

This enables automatic analysis during builds. See [MSBuild Integration](docs/MSBuild-Integration.md).

## CLI Commands

### analyze

Get repository statistics and extract the graph.

```bash
ngraphiphy-cli analyze <path> [options]
```

Options:

- `--out, -o <file>` — Write Markdown report to file
- `--cache <dir>` — Cache directory (default: `<path>/.ngraphiphy-cache`)

Example:

```bash
ngraphiphy-cli analyze /path/to/repo --out report.md
```

### report

Generate a detailed Markdown analysis report.

```bash
ngraphiphy-cli report <path> [options]
```

Options:

- `--out, -o <file>` — Write to file (prints to stdout if omitted)
- `--cache <dir>` — Cache directory (default: `<path>/.ngraphiphy-cache`)

Example:

```bash
ngraphiphy-cli report /path/to/repo --out analysis.md
cat analysis.md | less
```

### query

Ask an LLM a question about the codebase.

```bash
ngraphiphy-cli query <path> <question> [options]
```

Options:

- `--provider <name>` — `anthropic` (default), `openai`, `ollama`, `copilot`, `a2a`
- `--key <apiKey>` — API key (falls back to env vars)
- `--model <name>` — Model name
- `--agent-url <url>` — Remote agent URL (A2A only)
- `--cache <dir>` — Cache directory

Examples:

```bash
# Using Anthropic (default)
export ANTHROPIC_API_KEY=sk-ant-...
ngraphiphy-cli query /path/to/repo "What is the main entry point?"

# Using OpenAI
export OPENAI_API_KEY=sk-...
ngraphiphy-cli query /path/to/repo "Summarize the architecture" --provider openai --model gpt-4o

# Using Ollama (local)
ngraphiphy-cli query /path/to/repo "Find security issues" --provider ollama --model llama3.2

# Using GitHub Copilot
ngraphiphy-cli query /path/to/repo "List all services" --provider copilot

# Using remote A2A agent
ngraphiphy-cli query /path/to/repo "Explain this codebase" --provider a2a --agent-url http://agent.example.com
```

### serve

Start an MCP (Model Context Protocol) server on stdio.

```bash
ngraphiphy-cli serve [path] [options]
```

Arguments:

- `[path]` — Repository root (default: current directory)

Options:

- `--cache <dir>` — Cache directory

Example:

```bash
ngraphiphy-cli serve /path/to/repo
```

Diagnostics go to stderr; stdout is reserved for MCP JSON-RPC protocol.

## MCP Server Tools

When running as an MCP server, the following tools are available to AI clients:

| Tool | Description | Parameters |
|------|-------------|-----------|
| `get_god_nodes` | Most connected entities in the graph | `topN` (optional, default: 5) |
| `get_surprising_connections` | Cross-file dependencies and ambiguous edges | `topN` (optional, default: 5) |
| `get_summary_stats` | Node count, edge count, community count, top files | — |
| `search_nodes` | Find nodes by label substring | `query` (required) |
| `get_report` | Full Markdown analysis report | — |

### Example Claude Prompts

With the server running in Claude Desktop:

> "What are the most connected classes in this repository?"

> "Are there any surprising dependencies between modules?"

> "Show me everything that depends on the Router class."

> "Generate a full analysis report for this codebase."

## Configuration

### Cache Directory

By default, analysis is cached in `.ngraphiphy-cache/` at the repository root. To use a custom location:

```bash
ngraphiphy-cli analyze /path/to/repo --cache /tmp/ngraph-cache
```

Or in MSBuild:

```xml
<PropertyGroup>
  <NgraphiphyCacheDir>$(MSBuildProjectDirectory)/.ngraphiphy-cache</NgraphiphyCacheDir>
</PropertyGroup>
```

### .gitignore Handling

Ngraphiphy respects `.gitignore` files at any depth in your repository. Files matching `.gitignore` patterns are automatically excluded.

Additionally, sensitive files are **always blocked**:

- Private keys (`.pem`, `.key`, `.p8`, etc.)
- Credentials (`.env`, `config.local.*`, credentials files)
- Lock files (some)

### Environment Variables

#### For API Keys

```bash
export ANTHROPIC_API_KEY=sk-ant-...
export OPENAI_API_KEY=sk-...
export GITHUB_TOKEN=ghp_...  # For Copilot provider
```

#### For Build Integration

```bash
export NGRAPHIPHY_ANALYZING=1  # Prevents recursive analysis during MSBuild
```

## Architecture

### Pipeline

```
Source Files
    ↓
TreeSitter Extraction (9 languages)
    ↓
Entity Deduplication
    ↓
Graph Construction (QuikGraph)
    ↓
Leiden Clustering (optional)
    ↓
Analysis & Reporting
```

### Graph Model

- **Vertices (Nodes)** — Classes, functions, methods, modules, types
- **Edges** — Dependencies, calls, imports, inheritance
- **Communities** — Groups of related entities (Leiden algorithm)

### Components

- `Ngraphiphy` — Core: extraction, graph building, dedup, clustering, analysis, reporting, caching
- `Ngraphiphy.Llm` — LLM integration (MAF 1.5.0 agent, 5 providers)
- `Ngraphiphy.Pipeline` — Orchestration (`RepositoryAnalysis.RunAsync`)
- `Ngraphiphy.Cli` — Command-line interface and MCP server
- `Ngraphiphy.MSBuild` — MSBuild integration targets

## Supported Languages

Ngraphiphy extracts and analyzes code in these languages:

| Language | Extensions |
|----------|-----------|
| C# | `.cs` |
| Java | `.java` |
| Python | `.py` |
| JavaScript | `.js`, `.jsx` |
| TypeScript | `.ts`, `.tsx` |
| Go | `.go` |
| Rust | `.rs` |
| C++ | `.cpp`, `.cc`, `.h`, `.hpp` |
| C | `.c`, `.h` |

## Troubleshooting

### "ngraphiphy-cli not found"

Ensure the tool is installed:

```bash
dotnet tool list -g
```

If not listed, install it:

```bash
dotnet tool install -g Ngraphiphy.Cli
```

### "Leiden clustering library not found"

Leiden clustering is optional. If the native library is unavailable, analysis continues with a graceful warning. This is non-fatal.

To resolve:

- Ensure TreeSitter native libraries are present in your `.NET` runtime folder
- On Linux: `sudo apt-get install tree-sitter`
- On macOS: `brew install tree-sitter`
- On Windows: Use the pre-built distribution

### "API key required but not provided"

For the `query` command, provide an API key via:

1. Environment variable: `export ANTHROPIC_API_KEY=...`
2. Command-line flag: `--key sk-ant-...`
3. Default fallback: `ANTHROPIC_API_KEY` (Anthropic), `OPENAI_API_KEY` (OpenAI)

### Performance Issues

- **First run** — Full extraction takes time. Subsequent runs use cache.
- **Large repos** — For repos with 1000+ files, incremental builds are faster.
- **Memory** — Disable Leiden clustering if memory is constrained: set `<NgraphiphyEnabled>false</NgraphiphyEnabled>` in MSBuild.

### Build Integration Not Running

1. Check that source files have been modified since last build:

   ```bash
   rm .ngraphiphy-cache/.msbuild-stamp
   dotnet build
   ```

2. Verify `ngraphiphy-cli` is installed:

   ```bash
   ngraphiphy-cli analyze . > /dev/null && echo "OK" || echo "Not found"
   ```

3. Disable and re-enable in the project:

   ```xml
   <PropertyGroup>
     <NgraphiphyEnabled>true</NgraphiphyEnabled>
   </PropertyGroup>
   ```

## Documentation

- [MSBuild Integration](docs/MSBuild-Integration.md) — Automate analysis in .NET builds
- [MCP Configuration](docs/mcp-config.md) — Setup with Claude Desktop
- [Usage Guide](docs/Usage.md) — Detailed command reference and workflows

## Contributing

Contributions are welcome. The codebase follows:

- .NET conventions (C#, async/await, nullable reference types)
- TUnit for testing (`tests/` directory)
- Spectre.Console for CLI output

To build and test:

```bash
dotnet build
dotnet test
```

## License

See [LICENSE](LICENSE) for details.

## About

Ngraphiphy is a .NET reimplementation of the Python `graphify` project, providing fast, integrated codebase analysis for .NET development workflows.
