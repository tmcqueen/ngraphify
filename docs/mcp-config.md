# Using Ngraphiphy as an MCP Server with Claude Desktop

## Build

```bash
dotnet publish src/Ngraphiphy.Cli/ -c Release -o ./dist
```

## Claude Desktop Configuration

Add to `claude_desktop_config.json`:

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

- macOS: `~/Library/Application Support/Claude/claude_desktop_config.json`
- Linux: `~/.config/claude/claude_desktop_config.json`
- Windows: `%APPDATA%\Claude\claude_desktop_config.json`

## Available Tools

| Tool | Description |
|------|-------------|
| `get_god_nodes` | Most connected entities (optional `topN`) |
| `get_surprising_connections` | Cross-file and ambiguous edges (optional `topN`) |
| `get_summary_stats` | Node count, edge count, community count, top files |
| `search_nodes` | Filter nodes by label substring |
| `get_report` | Full Markdown analysis report |

## Example Prompts

> "What are the most connected classes in this repository?"

> "Are there any surprising dependencies between modules?"

> "Show me everything that depends on the Router class."
