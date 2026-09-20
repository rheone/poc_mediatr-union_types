#!/usr/bin/env bash
# Installs the agent's tools into root-owned prefixes (/opt, /usr/local/bin) so the non-root agent cannot
# replace them. Runs at image build time as root, with this Feature's directory as the working directory.
# Requires the dotnet and node Features (installsAfter) for csharp-ls and the npm packages.
set -euo pipefail

# Option values arrive upper-cased as environment variables (CLAUDECODEVERSION, ...).
: "${CLAUDECODEVERSION:?}" "${CSHARPLSVERSION:?}" "${YAMLLSVERSION:?}" "${LANGSERVERSEXTRACTEDVERSION:?}" "${BASHLSVERSION:?}"
: "${MARKSMANVERSION:?}" "${MARKSMANSHA256:?}" "${CODEBASEMEMORYVERSION:?}" "${CODEBASEMEMORYSHA256:?}"

export DEBIAN_FRONTEND=noninteractive
export DOTNET_ROOT=/usr/share/dotnet
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
# The node Feature installs through nvm; its `current` symlink is the stable path to node and npm.
export PATH="/usr/local/share/nvm/current/bin:$DOTNET_ROOT:$PATH"
command -v npm >/dev/null || { echo "agent-tools: npm not found; the node Feature must be installed first" >&2; exit 1; }
command -v dotnet >/dev/null || { echo "agent-tools: dotnet not found; the dotnet Feature must be installed first" >&2; exit 1; }

# ripgrep (the repo's instructions require rg) and shellcheck (lints the scripts here) from Ubuntu's archive.
apt-get update
apt-get install -y --no-install-recommends ripgrep shellcheck unzip
rm -rf /var/lib/apt/lists/*

# Binaries from GitHub releases, verified against the SHA-256 pinned in the Feature options.
fetch() { # fetch <url> <sha256> <dest>
  curl -fsSL -o "$3" "$1"
  echo "$2  $3" | sha256sum -c -
}
fetch "https://github.com/artempyanykh/marksman/releases/download/${MARKSMANVERSION}/marksman-linux-x64" \
  "$MARKSMANSHA256" /usr/local/bin/marksman
chmod 0755 /usr/local/bin/marksman

tmp="$(mktemp -d)"
fetch "https://github.com/DeusData/codebase-memory-mcp/releases/download/v${CODEBASEMEMORYVERSION}/codebase-memory-mcp-linux-amd64-portable.tar.gz" \
  "$CODEBASEMEMORYSHA256" "$tmp/cbm.tgz"
mkdir "$tmp/cbm"
tar -xzf "$tmp/cbm.tgz" -C "$tmp/cbm"
install -m 0755 "$(find "$tmp/cbm" -type f -name codebase-memory-mcp | head -n1)" /usr/local/bin/codebase-memory-mcp
rm -rf "$tmp"

# npm and dotnet tool packages at exact versions (npm verifies registry integrity; there is no lockfile,
# so the exact versions are the pin).
npm install -g --prefix /opt/npm-global \
  "@anthropic-ai/claude-code@${CLAUDECODEVERSION}" \
  "yaml-language-server@${YAMLLSVERSION}" \
  "vscode-langservers-extracted@${LANGSERVERSEXTRACTEDVERSION}" \
  "bash-language-server@${BASHLSVERSION}"
npm cache clean --force
dotnet tool install csharp-ls --version "$CSHARPLSVERSION" --tool-path /opt/dotnet-tools
rm -rf /root/.dotnet /root/.nuget /root/.local /root/.npm

# Language-server plugins for Claude Code, as root-owned managed settings (the agent cannot edit them).
share=/usr/local/share/agent-tools
install -d -m 0755 "$share" /etc/claude-code/managed-settings.d
cp -r plugins "$share/claude-plugins"
chmod -R a+rX,go-w "$share"
install -m 0644 managed-settings.json /etc/claude-code/managed-settings.d/10-devcontainer.json
