#!/usr/bin/env bash
# Runs once after the container is created, as the non-root user, from the workspace root.
set -euo pipefail
cd "$(dirname "$0")/.."

# Fail fast if the workspace is a Windows/macOS bind mount or any other isolation check is violated
# (this also waits for the egress firewall to be in place).
bash .devcontainer/verify-isolation.sh

dotnet tool restore
dotnet restore

# Code-graph MCP server: no local web UI, and index the repo now so the first query is instant.
# `.mcp.json` starts the server itself; its background watcher then keeps the index fresh.
codebase-memory-mcp config set ui_enabled false >/dev/null
codebase-memory-mcp config set auto_index true >/dev/null
codebase-memory-mcp cli --quiet index_repository --repo-path "$PWD" >/dev/null ||
  echo "post-create: code-graph indexing failed; run it by hand: codebase-memory-mcp cli index_repository --repo-path \$PWD" >&2

echo "post-create: done. Next: run 'claude' and log in inside this container (see docs/DevContainer.md)."
