# Local Graph Database Containers

Run one at a time — both use Bolt on port 7687 by default (Memgraph is mapped to 7688).

## Neo4j Community

```bash
docker compose -f .docker/neo4j.docker-compose.yml up -d
```

| | |
|---|---|
| Browser | http://localhost:7474 |
| Bolt | bolt://localhost:7687 |
| Username | `neo4j` |
| Password | `ngraphiphy` |

Matching `appsettings.json`:
```json
"GraphDatabase": {
  "Backend": "neo4j",
  "Neo4j": {
    "Uri": "bolt://localhost:7687",
    "Username": "neo4j",
    "Password": "ngraphiphy"
  }
}
```

## Memgraph + Memgraph Lab

```bash
docker compose -f .docker/memgraph.docker-compose.yml up -d
```

| | |
|---|---|
| Memgraph Lab | http://localhost:3000 |
| Bolt | bolt://localhost:7688 |
| Auth | none (anonymous) |

Matching `appsettings.json`:
```json
"GraphDatabase": {
  "Backend": "memgraph",
  "Memgraph": {
    "Host": "localhost",
    "Port": 7688
  }
}
```

## Stop

```bash
docker compose -f .docker/neo4j.docker-compose.yml down
docker compose -f .docker/memgraph.docker-compose.yml down
```

Add `-v` to also remove data volumes.
