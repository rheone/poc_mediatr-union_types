#!/usr/bin/env bash
# PostToolUse hook (Edit|Write): whitespace-format the C# file Claude just edited so agent edits
# arrive already formatted. Runs only inside the dev container (DEVCONTAINER=true) so it never
# changes behaviour on a host without jq or the pinned SDK. Never blocks: always exits 0.
# `--folder` mode skips MSBuild project loading (about 1s instead of several); it applies the same
# .editorconfig whitespace rules that `dotnet format whitespace --verify-no-changes` checks.
[ "${DEVCONTAINER:-}" = true ] || exit 0
command -v jq >/dev/null 2>&1 || exit 0
file="$(jq -r '.tool_input.file_path // empty')"
case "$file" in *.cs) ;; *) exit 0 ;; esac
[ -f "$file" ] || exit 0
cd "${CLAUDE_PROJECT_DIR:-.}" || exit 0
dotnet format whitespace --folder --include "$file" >/dev/null 2>&1 || true
exit 0
