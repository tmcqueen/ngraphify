# Graphiphy

.NET 10 reimplementation of the graphify Python app. Parses source files via TreeSitter, builds a knowledge graph (QuikGraph + Leiden clustering), and exposes analysis via CLI (Spectre.Console) and MCP stdio server (ModelContextProtocol 1.3.0).

## Structure

- `src/Graphiphy/` — core: extraction (9 languages), graph build, dedup, cluster, analysis, report, cache
- `src/Graphiphy.Llm/` — MAF 1.5.0 agent; 5 providers (OpenAI, Anthropic, Ollama, Copilot, A2A)
- `src/Graphiphy.Pipeline/` — `RepositoryAnalysis.RunAsync` orchestrates the full pipeline
- `src/Graphiphy.Cli/` — commands: `analyze`, `report`, `query`, `serve`; MCP server under `Mcp/`
- `tests/` — TUnit 1.43.41; run with `dotnet run --project tests/<Project>/`

## Key facts

- CLI binary name: `graphiphy-cli` (not `graphiphy` — Linux CLR collision fix)
- File detection respects `.gitignore` files at any depth; sensitive files always blocked
- Leiden clustering catch: `DllNotFoundException or EntryPointNotFoundException or InvalidOperationException`

## Instructions

- **Do not trust training data over official documentation.** APIs change; read the source or docs first.
- **Do not speculate to the user.** If you don't know, read the code, run a command, or say you don't know. The user trusts you to give answers backed by facts.
