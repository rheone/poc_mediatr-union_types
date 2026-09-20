#!/usr/bin/env bash
# End-to-end check that the dev container does what docs/DevContainer.md says. Run inside the
# container from anywhere: .devcontainer/smoke-test.sh   (takes a few minutes on a cold NuGet cache)
set -uo pipefail
cd "$(dirname "$0")/.." || exit 1
here=".devcontainer"
fails=0
step() { printf '\n== %s\n' "$1"; }
ok() { printf 'PASS  %s\n' "$1"; }
bad() { printf 'FAIL  %s\n' "$1"; fails=$((fails + 1)); }
run() { local d="$1"; shift; if "$@"; then ok "$d"; else bad "$d"; fi; }

step "SDK matches global.json"
want="$(jq -r .sdk.version global.json)"
have="$(dotnet --version)"
if [ "$want" = "$have" ]; then ok "dotnet $have"; else bad "dotnet $have, global.json wants $want"; fi

step "build and test"
run "dotnet build" dotnet build --nologo -v q
run "dotnet test" dotnet test --nologo --no-build

step "isolation"
run "verify-isolation.sh" bash "$here/verify-isolation.sh"

step "language servers: symbol queries (headless LSP)"
lsp() { # lsp <description> <file> <lang> <expected symbol> -- <server cmd...>
  local d="$1" f="$2" l="$3" e="$4"
  shift 4
  if out="$(node "$here/lsp-query.mjs" --root . --file "$f" --lang "$l" --expect "$e" --timeout 120 -- "$@" 2>/dev/null)"; then
    ok "$d"
  else
    bad "$d"
    echo "$out" | cut -c1-400
  fi
}
lsp "csharp-ls: ProductNames.Normalize in a regular file" src/MediatrUnionPoc.Domain/ProductNames.cs csharp Normalize csharp-ls
lsp "marksman: README heading" README.md markdown "$(head -n1 README.md | sed 's/^# //')" marksman server
lsp "bash-language-server: is_ipv4 in init-firewall.sh" "$here/features/egress-firewall/init-firewall.sh" shellscript is_ipv4 bash-language-server start
# Known gap, reported rather than failed: csharp-ls does not surface the C# 15 `union` declaration as a symbol.
u=src/MediatrUnionPoc.Application/Features/Products/Create/CreateProductResult.cs
if node "$here/lsp-query.mjs" --root . --file "$u" --lang csharp --expect CreateProductResult --timeout 30 -- csharp-ls >/dev/null 2>&1; then
  echo "NOTE  csharp-ls now lists the union declaration as a document symbol: update docs/DevContainer.md"
else
  echo "NOTE  csharp-ls does not list the 'union' declaration as a document symbol (expected on this SDK; see docs)"
fi

step "code graph: who calls ProductNames.Normalize?"
codebase-memory-mcp cli --quiet index_repository --repo-path "$PWD" >/dev/null
proj="$(codebase-memory-mcp cli --quiet list_projects --format json | jq -r --arg p "$PWD" '.projects[] | select(.root_path == $p) | .name')"
if [ -z "$proj" ]; then
  bad "project not indexed"
else
  qn="$proj.src.MediatrUnionPoc.Domain.ProductNames.ProductNames.Normalize"
  callers="$(codebase-memory-mcp cli --quiet trace_path --project "$proj" --function-name "$qn" --direction inbound)"
  if echo "$callers" | rg -q "PatchProductHandler" && echo "$callers" | rg -q "ProductRepository"; then
    ok "trace_path finds the Patch handler and the repository as callers"
  else
    bad "trace_path did not return the expected callers"; echo "$callers" | head -n 20
  fi
fi

step "Claude Code"
run "claude --version" claude --version
echo "MANUAL  run 'claude', log in, then '/mcp' (codebase-memory-mcp connected) and '/plugin' (mattpocock-skills, csharp-lsp enabled)"

echo
if [ "$fails" -eq 0 ]; then echo "smoke-test: all automated checks passed"; else echo "smoke-test: $fails check(s) FAILED"; fi
exit $((fails > 0))
