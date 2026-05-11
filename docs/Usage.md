# Ngraphiphy Usage Guide

Complete reference for Ngraphiphy commands, LLM provider setup, MCP integration, and common workflows.

## Table of Contents

1. [CLI Commands Reference](#cli-commands-reference)
2. [LLM Providers](#llm-providers)
3. [MCP Server Setup](#mcp-server-setup)
4. [Common Workflows](#common-workflows)
5. [Performance Tips](#performance-tips)
6. [Output Formats](#output-formats)
7. [Advanced Configuration](#advanced-configuration)
8. [Troubleshooting](#troubleshooting)

## CLI Commands Reference

### analyze

Extract and analyze a repository, returning statistics.

**Syntax:**

```bash
ngraphiphy-cli analyze <path> [--out <file>] [--cache <dir>]
```

**Arguments:**

- `<path>` — Repository root directory (required)

**Options:**

- `--out, -o <file>` — Write Markdown report to file
- `--cache <dir>` — Cache directory (default: `<path>/.ngraphiphy-cache`)

**Output:**

Table with metrics:
- Files detected — number of supported source files
- Nodes (entities) — extracted classes, functions, modules
- Edges (relations) — dependencies and connections
- Top entity — most connected entity with its source file

**Examples:**

```bash
# Basic analysis
ngraphiphy-cli analyze ~/my-project

# Save report to file
ngraphiphy-cli analyze ~/my-project --out report.md

# Use custom cache location
ngraphiphy-cli analyze ~/my-project --cache /tmp/cache
```

**Exit Code:**

- `0` — Success
- `1` — Analysis failed

### report

Generate a detailed Markdown analysis report.

**Syntax:**

```bash
ngraphiphy-cli report <path> [--out <file>] [--cache <dir>]
```

**Arguments:**

- `<path>` — Repository root directory (required)

**Options:**

- `--out, -o <file>` — Write to file (prints to stdout if omitted)
- `--cache <dir>` — Cache directory (default: `<path>/.ngraphiphy-cache`)

**Output Format:**

The generated report includes:
- Summary statistics (files, nodes, edges, communities)
- Top connected entities (god nodes)
- Surprising connections (cross-module dependencies)
- Community breakdown
- Per-file analysis

**Examples:**

```bash
# Print report to terminal
ngraphiphy-cli report ~/my-project | less

# Save to file and open in editor
ngraphiphy-cli report ~/my-project --out analysis.md
code analysis.md

# Use with other tools
ngraphiphy-cli report ~/my-project | grep -A5 "Top Entities"

# Generate and immediately view
ngraphiphy-cli report . --out /tmp/report.md && cat /tmp/report.md
```

**Report Sections:**

```markdown
# Repository Analysis: my-project

## Summary Statistics

- **Files:** 42
- **Entities:** 156
- **Relationships:** 284
- **Communities:** 8

## Top Entities (Most Connected)

1. UserService — 24 connections
2. Database — 19 connections
3. Router — 17 connections

## Surprising Connections

- AuthService → PaymentService (cross-module)
- CacheManager → EmailService (unexpected)

## Communities

### Community 1: Auth & Security
- AuthService
- TokenManager
- PermissionChecker

## Per-File Breakdown

[Detailed analysis of each file]
```

### query

Ask an LLM a question about the codebase using the knowledge graph.

**Syntax:**

```bash
ngraphiphy-cli query <path> <question> [--provider <name>] [--key <apiKey>] [--model <name>] [--agent-url <url>] [--cache <dir>]
```

**Arguments:**

- `<path>` — Repository root directory (required)
- `<question>` — Question to ask about the codebase (required)

**Options:**

- `--provider <name>` — LLM provider (default: `anthropic`)
  - `anthropic` — Claude (Anthropic)
  - `openai` — GPT (OpenAI)
  - `ollama` — Local models (Ollama)
  - `copilot` — GitHub Copilot
  - `a2a` — Remote agent (Anthropic A2A)
- `--key <apiKey>` — API key (optional, falls back to env vars)
- `--model <name>` — Model name (optional, uses provider default if omitted)
- `--agent-url <url>` — Remote agent URL (required for `a2a` provider)
- `--cache <dir>` — Cache directory (default: `<path>/.ngraphiphy-cache`)

**Examples:**

```bash
# Anthropic (default, requires ANTHROPIC_API_KEY)
export ANTHROPIC_API_KEY=sk-ant-...
ngraphiphy-cli query . "What is the main entry point of this application?"

# OpenAI
export OPENAI_API_KEY=sk-...
ngraphiphy-cli query . "List all database operations" --provider openai

# Ollama (local, no API key needed)
ngraphiphy-cli query . "Find potential circular dependencies" --provider ollama

# GitHub Copilot (requires gh auth login)
gh auth login
ngraphiphy-cli query . "Identify security issues" --provider copilot

# Remote A2A agent
ngraphiphy-cli query . "Summarize this codebase" --provider a2a --agent-url https://agent.example.com

# Custom model
ngraphiphy-cli query . "What does UserService do?" --model claude-opus-4
```

**Output:**

```
Querying LLM...

╭──────────────────────────────────────╮
│ Answer                               │
├──────────────────────────────────────┤
│ The UserService class is located in  │
│ Services/UserService.cs and handles  │
│ user authentication, profile updates, │
│ and permission management. It's used  │
│ by the Router and AuthService        │
│ classes.                              │
╰──────────────────────────────────────╯
```

**Error Handling:**

- API key not provided: Error message with hint about env var or `--key` flag
- LLM request failed: Error with service-specific message
- Analysis failed: Error with root cause

### serve

Start an MCP (Model Context Protocol) server on stdio for use with Claude, Cursor, etc.

**Syntax:**

```bash
ngraphiphy-cli serve [path] [--cache <dir>]
```

**Arguments:**

- `[path]` — Repository root (default: current directory)

**Options:**

- `--cache <dir>` — Cache directory (default: `<path>/.ngraphiphy-cache`)

**Protocol:**

- Listens on stdin/stdout for MCP JSON-RPC 2.0 requests
- Diagnostics written to stderr (not part of MCP protocol)
- Server runs indefinitely until killed or connection closes

**Examples:**

```bash
# Start server for current repo
ngraphiphy-cli serve

# Start server for specific repo
ngraphiphy-cli serve /path/to/repo

# Start with custom cache
ngraphiphy-cli serve /path/to/repo --cache /tmp/cache

# Start and log diagnostics to file
ngraphiphy-cli serve /path/to/repo 2> server.log
```

**Startup Messages (stderr):**

```
[ngraphiphy] Analyzing /path/to/repo...
[ngraphiphy] Extracting C# files...
[ngraphiphy] Building graph...
[ngraphiphy] Ready — 156 nodes, 284 edges.
[ngraphiphy] Starting MCP server on stdio...
```

**For Integration:**

See [MCP Configuration](mcp-config.md) for Claude Desktop, Cursor, and other client setup.

## LLM Providers

### Anthropic (Claude)

**Setup:**

1. Get API key from [console.anthropic.com](https://console.anthropic.com)
2. Set environment variable:

   ```bash
   export ANTHROPIC_API_KEY=sk-ant-...
   ```

**Usage:**

```bash
ngraphiphy-cli query . "What are the main services?" --provider anthropic
ngraphiphy-cli query . "Summarize the architecture" --model claude-opus-4
```

**Available Models:**

- `claude-opus-4` — Flagship model (most capable)
- `claude-sonnet-4-6` — **Default**, best balance
- `claude-3-5-haiku` — Fast, lightweight

**Default Model:** `claude-sonnet-4-6`

**Max Tokens:** 4096 (fixed)

**Cost:** Pay-as-you-go, based on input/output tokens

### OpenAI (GPT)

**Setup:**

1. Get API key from [platform.openai.com](https://platform.openai.com/api-keys)
2. Set environment variable:

   ```bash
   export OPENAI_API_KEY=sk-...
   ```

**Usage:**

```bash
ngraphiphy-cli query . "Explain the data flow" --provider openai
ngraphiphy-cli query . "List all classes" --provider openai --model gpt-4-turbo
```

**Available Models:**

- `gpt-4o` — **Default**, latest and most capable
- `gpt-4-turbo` — Turbo variant
- `gpt-4` — Standard GPT-4
- `gpt-3.5-turbo` — Faster, cheaper

**Default Model:** `gpt-4o`

**Cost:** Higher than Anthropic for equivalent usage

### Ollama (Local Models)

**Setup:**

1. Install [Ollama](https://ollama.ai)
2. Pull a model:

   ```bash
   ollama pull llama3.2
   ```

3. Start Ollama service:

   ```bash
   ollama serve
   ```

**Usage:**

```bash
ngraphiphy-cli query . "What is the main architecture?" --provider ollama
ngraphiphy-cli query . "Find potential bugs" --provider ollama --model mistral
```

**Available Models:**

- `llama3.2` — **Default**, good quality
- `mistral` — Fast, efficient
- `neural-chat` — Optimized for Q&A
- `dolphin-mixtral` — High quality
- Others from [ollama.ai/library](https://ollama.ai/library)

**Default Model:** `llama3.2`

**Endpoint:** `http://localhost:11434` (default)

**Cost:** Free (runs locally)

**Note:** Accuracy depends on model size. Larger models (7B+) recommended for code analysis.

### GitHub Copilot

**Setup:**

1. Authenticate with GitHub CLI:

   ```bash
   gh auth login
   ```

   Or set token:

   ```bash
   export GITHUB_TOKEN=ghp_...
   ```

2. Copilot extension installed via GitHub CLI

**Usage:**

```bash
ngraphiphy-cli query . "What services are imported here?" --provider copilot
```

**Model:** Managed by GitHub (typically Claude or GPT)

**Cost:** Included in GitHub Copilot subscription ($10/month)

**Note:** Model and capabilities managed by GitHub; no custom model selection

### A2A (Remote Anthropic Agent)

**Setup:**

1. Get remote agent URL and optional API token from your organization
2. Provide agent URL:

   ```bash
   ngraphiphy-cli query . "Summarize this repo" --provider a2a \
     --agent-url https://agent.your-org.com
   ```

3. Optional: Provide API key for authentication:

   ```bash
   ngraphiphy-cli query . "..." --provider a2a \
     --agent-url https://agent.your-org.com \
     --key your-bearer-token
   ```

**Usage:**

```bash
ngraphiphy-cli query . "Analyze this codebase" \
  --provider a2a \
  --agent-url https://agent.example.com \
  --key optional-bearer-token
```

**Cost:** Managed by your organization/agent provider

**Note:** Requires network access to the remote agent

## MCP Server Setup

### Claude Desktop (macOS/Windows/Linux)

**Build the CLI:**

```bash
dotnet publish src/Ngraphiphy.Cli -c Release -o ~/dist
```

**Configuration File Location:**

- **macOS:** `~/Library/Application Support/Claude/claude_desktop_config.json`
- **Windows:** `%APPDATA%\Claude\claude_desktop_config.json`
- **Linux:** `~/.config/claude/claude_desktop_config.json`

**Add Ngraphiphy:**

```json
{
  "mcpServers": {
    "ngraphiphy": {
      "command": "/absolute/path/to/dist/ngraphiphy-cli",
      "args": ["serve", "/absolute/path/to/your/repo"]
    }
  }
}
```

**Example (Linux):**

```json
{
  "mcpServers": {
    "ngraphiphy": {
      "command": "/home/user/dist/ngraphiphy-cli",
      "args": ["serve", "/home/user/projects/my-app"]
    }
  }
}
```

**Verify:**

1. Restart Claude Desktop
2. Open a conversation
3. Click the `@` button — you should see "ngraphiphy" in the tools list
4. Use a prompt: "What are the top dependencies in this codebase?"

### Cursor

**Setup:**

1. Build CLI as above
2. Add to `.cursor/settings.json` or Cursor settings UI
3. Restart Cursor

**Configuration (via settings.json):**

```json
{
  "mcp": {
    "servers": {
      "ngraphiphy": {
        "command": "/path/to/ngraphiphy-cli",
        "args": ["serve", "/path/to/repo"]
      }
    }
  }
}
```

### Other MCP Clients

Any MCP 1.3.0-compatible client can use Ngraphiphy. Start the server:

```bash
ngraphiphy-cli serve /path/to/repo
```

Then configure your client to connect to the stdio server.

## Common Workflows

### Workflow 1: Quick Repository Assessment

Understand a new codebase quickly.

**Steps:**

```bash
# 1. Analyze the repo
ngraphiphy-cli analyze ~/new-project

# 2. Generate a detailed report
ngraphiphy-cli report ~/new-project --out overview.md
cat overview.md

# 3. Ask follow-up questions
export ANTHROPIC_API_KEY=sk-ant-...
ngraphiphy-cli query ~/new-project "What is the main architecture?"
ngraphiphy-cli query ~/new-project "List the core services"
```

**Time:** ~1-2 minutes for most repos

### Workflow 2: Dependency Analysis

Find and understand cross-module dependencies.

**Steps:**

```bash
# Generate report with surprising connections section
ngraphiphy-cli report . --out deps.md
grep -A10 "Surprising Connections" deps.md

# Ask LLM for detailed analysis
ngraphiphy-cli query . "Are there any circular dependencies?"
ngraphiphy-cli query . "Which services should be decoupled?"
```

### Workflow 3: Onboarding New Developers

Help team members understand the codebase.

**Steps:**

```bash
# 1. Generate and share report
ngraphiphy-cli report ~/my-project --out ARCHITECTURE.md
git add ARCHITECTURE.md
git commit -m "docs: add auto-generated architecture guide"

# 2. Create a shared MCP server
ngraphiphy-cli serve ~/my-project &

# 3. Share server URL with team, add to Claude Desktop
# Team members can now ask questions about the codebase
```

### Workflow 4: Continuous Monitoring (MSBuild Integration)

Analyze automatically during development.

**Steps:**

```bash
# 1. Install NuGet package
dotnet add package Ngraphiphy.MSBuild

# 2. Build as usual
dotnet build

# 3. Analysis runs automatically, cache updated
# No additional steps needed

# 4. Periodically review analysis
ngraphiphy-cli report . --out report.md
```

### Workflow 5: Code Review Preparation

Prepare insights for pull request reviews.

**Steps:**

```bash
# 1. Analyze the current branch
ngraphiphy-cli analyze . --out pr-analysis.md

# 2. Ask about specific impacts
ngraphiphy-cli query . "What other services depend on AuthService?"
ngraphiphy-cli query . "Are there any new cross-module dependencies in this change?"

# 3. Include findings in PR description
```

## Performance Tips

### 1. Use Cache

**First run:** Full extraction and analysis

```bash
ngraphiphy-cli analyze /path/to/repo  # ~30-60s for 100+ files
```

**Subsequent runs:** Cache reused, faster

```bash
ngraphiphy-cli analyze /path/to/repo  # ~5-10s
```

**Keep cache between runs** (except when you need fresh analysis):

```bash
rm -rf .ngraphiphy-cache && ngraphiphy-cli analyze .
```

### 2. Filter Large Repositories

For very large repos (1000+ files), exclude directories:

```bash
# Add to .gitignore
echo "node_modules/" >> .gitignore
echo "dist/" >> .gitignore
echo ".git/" >> .gitignore

# Rerun analysis
rm -rf .ngraphiphy-cache
ngraphiphy-cli analyze .
```

### 3. Use Incremental Builds

With MSBuild integration, analysis only runs when source files change:

```bash
dotnet build  # Analysis runs
dotnet build  # Cache reused, no analysis
# Change a file
dotnet build  # Analysis runs again
```

### 4. Parallel Analysis (MSBuild)

MSBuild integration is safe with parallel builds (`-m` flag):

```bash
dotnet build -m  # Multiple projects, one analysis
```

### 5. Offline Mode (Ollama)

For offline work, use Ollama with local models:

```bash
ngraphiphy-cli query . "Summarize classes" --provider ollama --model llama3.2
```

No network, no API key, instant (or slow, depending on model size).

### 6. Batch Queries

For multiple questions, run them sequentially to reuse cache:

```bash
ngraphiphy-cli query . "What services exist?"
ngraphiphy-cli query . "What are the main dependencies?"
# Cache persists, second query is faster
```

## Output Formats

### Console Output (analyze)

```
Metric                Value
─────────────────────────────
Files detected        42
Nodes (entities)      156
Edges (relations)     284
Top entity            UserService (Services/UserService.cs)
```

### Markdown Report (report)

```markdown
# Repository Analysis: my-project

## Summary Statistics
- Files: 42
- Entities: 156
- Relationships: 284
- Communities: 8

## Top Entities (Most Connected)
1. UserService — 24 connections
2. Database — 19 connections

## Surprising Connections
- AuthService → PaymentService (cross-module)

## Per-File Breakdown
...
```

### Query Response (query)

```
╭─────────────────────────────────────────────╮
│ Answer                                      │
├─────────────────────────────────────────────┤
│ The UserService class handles authentication│
│ and user profile management. It's connected │
│ to AuthService, Database, and Router classes│
╰─────────────────────────────────────────────╯
```

### MCP Tool Results (serve)

Tools return JSON-RPC responses:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": {
    "content": [
      {
        "type": "text",
        "text": "Most connected entities:\n1. UserService (24 connections)\n2. Database (19 connections)..."
      }
    ]
  }
}
```

## Advanced Configuration

### Custom Cache Location

For CI/CD or shared caches:

```bash
export CACHE_DIR=/shared/ngraph-cache
ngraphiphy-cli analyze . --cache $CACHE_DIR
ngraphiphy-cli report . --cache $CACHE_DIR
```

### MSBuild per-Project Override

```xml
<PropertyGroup>
  <NgraphiphyCacheDir>$(MSBuildProjectDirectory)/.ngraphiphy-custom</NgraphiphyCacheDir>
</PropertyGroup>
```

### Disable Analysis (MSBuild)

```xml
<PropertyGroup>
  <NgraphiphyEnabled>false</NgraphiphyEnabled>
</PropertyGroup>
```

### Environment-Based Configuration

```bash
# CI/CD: use custom cache
if [ "$CI" = "true" ]; then
  ngraphiphy-cli analyze . --cache /tmp/ci-cache
else
  ngraphiphy-cli analyze .
fi
```

### Batch Analysis Script

Analyze multiple repositories:

```bash
#!/bin/bash
for repo in ~/projects/*; do
  echo "Analyzing $repo"
  ngraphiphy-cli analyze "$repo" --out "$repo/analysis.md"
done
```

## Troubleshooting

### "command not found: ngraphiphy-cli"

**Check installation:**

```bash
dotnet tool list -g | grep Ngraphiphy
```

**Reinstall:**

```bash
dotnet tool uninstall -g Ngraphiphy.Cli
dotnet tool install -g Ngraphiphy.Cli
```

**Check PATH:**

```bash
echo $PATH | tr ':' '\n' | grep dotnet
# Should include ~/.dotnet/tools
```

### "API key required but not provided"

**For Anthropic:**

```bash
export ANTHROPIC_API_KEY=sk-ant-...
ngraphiphy-cli query . "your question"
```

**For OpenAI:**

```bash
export OPENAI_API_KEY=sk-...
ngraphiphy-cli query . "your question" --provider openai
```

**Or pass directly:**

```bash
ngraphiphy-cli query . "your question" --key sk-ant-...
```

### "Analysis failed: DllNotFoundException"

Leiden clustering library missing (non-fatal warning). Analysis continues without clustering:

```bash
[warn] Could not load Leiden library; continuing without clustering
```

To fix:

```bash
# Linux
sudo apt-get install libtreesitter

# macOS
brew install tree-sitter

# Windows
# Use the pre-built distribution which includes all libraries
```

### "Cache directory is full"

Clear cache:

```bash
rm -rf .ngraphiphy-cache
ngraphiphy-cli analyze .  # Rebuilds cache
```

Or use a custom location:

```bash
ngraphiphy-cli analyze . --cache /tmp/cache
```

### "MCP Server not connecting in Claude Desktop"

**Check configuration:**

```bash
# Verify file exists
cat ~/Library/Application\ Support/Claude/claude_desktop_config.json
```

**Verify paths are absolute:**

```json
{
  "mcpServers": {
    "ngraphiphy": {
      "command": "/absolute/path/to/ngraphiphy-cli",
      "args": ["serve", "/absolute/path/to/repo"]
    }
  }
}
```

**Check command works:**

```bash
/absolute/path/to/ngraphiphy-cli serve /absolute/path/to/repo > /dev/null 2>&1
echo $?  # Should be 0 or interrupted
```

**Restart Claude Desktop** and check the server list (click `@`).

### "Query returns generic/unhelpful answers"

**Improve question clarity:**

Instead of: "Tell me about the code"

Ask: "What are the main classes and their dependencies in this codebase?"

**Use a more capable model:**

```bash
ngraphiphy-cli query . "..." --model claude-opus-4
```

**Provide context in question:**

```bash
ngraphiphy-cli query . "I'm implementing a new feature. What services handle authentication and how are they used?"
```

### "Build is slow with MSBuild integration"

**Disable for slow machines:**

```xml
<PropertyGroup>
  <NgraphiphyEnabled>false</NgraphiphyEnabled>
</PropertyGroup>
```

**Or run async:**

The integration already runs asynchronously and doesn't block builds. To verify:

```bash
dotnet build --verbosity=minimal
# [ngraphiphy] messages appear in background, build completes independently
```

### "Can't find Ollama endpoint"

**Start Ollama:**

```bash
ollama serve  # or use: brew services start ollama (macOS)
```

**Verify endpoint:**

```bash
curl http://localhost:11434/api/tags
# Should return model list
```

**Use custom endpoint:**

```bash
ngraphiphy-cli query . "..." --provider ollama --model llama3.2 \
  # Note: Currently uses default endpoint; for custom, file a feature request
```

### Performance Regression After Update

**Clear cache:**

```bash
rm -rf .ngraphiphy-cache
ngraphiphy-cli analyze .
```

**Verify extraction:**

```bash
ngraphiphy-cli analyze . | grep "Files detected"
```

If files detected drops, check `.gitignore` hasn't changed or files weren't moved.
