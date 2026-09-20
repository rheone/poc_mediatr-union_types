# syntax=docker/dockerfile:1
# Linux dev container for the union POC. Every download is pinned to an exact version and, where
# the publisher provides one, verified against a SHA. Bump them together with docs/DevContainer.md.

# Pinned by digest so a rebuild months later starts from the same OS layer (bump deliberately).
FROM ubuntu:24.04@sha256:008173c23f95b170204355c12626cb5a965d779a7e1283b09e9cffbb1bf33ca3

# Must equal global.json's "sdk.version". The tarball is fetched from the official .NET build CDN
# (dotnet-install.sh resolves to the same URL); the SHA-512 is the value published beside it in
# <tarball>.sha512. An exact pre-release SDK is not available as a stable apt package or MCR tag.
ARG DOTNET_SDK_VERSION=11.0.100-rc.1.26425.128
ARG DOTNET_SDK_SHA512=608518577aab9db33db92ebd74f0fc9bfbcc7b3d473619f4731aca2edba80af6c571a38d5be6994862fc4679f6f3d13db767712fcb6f70a7683974a18824a0cc
ARG NODE_VERSION=24.21.0
ARG NODE_SHA256=fd8e59d5a511510f6a298afb548f18c7d2b1be404d8b4a27d94fbe49f56cb2d6
ARG GH_VERSION=2.101.0
ARG GH_SHA256=9bca2d1c16825f109907a23307628a2f0698fbf99662b73a5cf0b020293072b8
ARG MARKSMAN_VERSION=2026-02-08
ARG MARKSMAN_SHA256=be5098e8213219269c47fc0d916a66fa31ce0602ec967475c722260aabf26087
ARG CBM_VERSION=0.11.0
ARG CBM_SHA256=1f9e8293eb2bc5c05cfa27a7e8fc033da6d729ffad525ccfcdaa3fd606306683
ARG CLAUDE_CODE_VERSION=2.1.278
ARG CSHARP_LS_VERSION=0.27.0
ARG YAML_LS_VERSION=1.24.0
ARG LANGSERVERS_EXTRACTED_VERSION=4.10.0
ARG BASH_LS_VERSION=5.8.1

ENV DEBIAN_FRONTEND=noninteractive \
    LANG=C.UTF-8 \
    DOTNET_ROOT=/usr/share/dotnet \
    DOTNET_NOLOGO=1 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_GENERATE_ASPNET_CERTIFICATE=false \
    DOTNET_ROLL_FORWARD=Major \
    NUGET_XMLDOC_MODE=skip

# iptables/ipset/dnsutils/iproute2 are for the egress firewall; ripgrep because the repo requires
# rg in tooling; shellcheck lints the scripts here. No sudo, ssh client or docker CLI on purpose.
RUN apt-get update \
 && apt-get install -y --no-install-recommends \
      ca-certificates curl git jq ripgrep shellcheck less unzip xz-utils procps \
      iptables ipset dnsutils iproute2 libicu74 libssl3t64 zlib1g libstdc++6 tzdata \
 && rm -rf /var/lib/apt/lists/* \
 && userdel -r ubuntu \
 && useradd --create-home --uid 1000 --shell /bin/bash vscode

RUN curl -fsSL -o /tmp/sdk.tgz "https://builds.dotnet.microsoft.com/dotnet/Sdk/${DOTNET_SDK_VERSION}/dotnet-sdk-${DOTNET_SDK_VERSION}-linux-x64.tar.gz" \
 && echo "${DOTNET_SDK_SHA512}  /tmp/sdk.tgz" | sha512sum -c - \
 && mkdir -p "$DOTNET_ROOT" && tar -xzf /tmp/sdk.tgz -C "$DOTNET_ROOT" && rm /tmp/sdk.tgz \
 && ln -s "$DOTNET_ROOT/dotnet" /usr/bin/dotnet

RUN curl -fsSL -o /tmp/node.txz "https://nodejs.org/dist/v${NODE_VERSION}/node-v${NODE_VERSION}-linux-x64.tar.xz" \
 && echo "${NODE_SHA256}  /tmp/node.txz" | sha256sum -c - \
 && tar -xJf /tmp/node.txz -C /usr/local --strip-components=1 && rm /tmp/node.txz \
 && curl -fsSL -o /tmp/gh.tgz "https://github.com/cli/cli/releases/download/v${GH_VERSION}/gh_${GH_VERSION}_linux_amd64.tar.gz" \
 && echo "${GH_SHA256}  /tmp/gh.tgz" | sha256sum -c - \
 && tar -xzf /tmp/gh.tgz -C /tmp && install -m 0755 "/tmp/gh_${GH_VERSION}_linux_amd64/bin/gh" /usr/local/bin/gh && rm -rf /tmp/gh* \
 && curl -fsSL -o /usr/local/bin/marksman "https://github.com/artempyanykh/marksman/releases/download/${MARKSMAN_VERSION}/marksman-linux-x64" \
 && echo "${MARKSMAN_SHA256}  /usr/local/bin/marksman" | sha256sum -c - && chmod 0755 /usr/local/bin/marksman \
 && curl -fsSL -o /tmp/cbm.tgz "https://github.com/DeusData/codebase-memory-mcp/releases/download/v${CBM_VERSION}/codebase-memory-mcp-linux-amd64-portable.tar.gz" \
 && echo "${CBM_SHA256}  /tmp/cbm.tgz" | sha256sum -c - \
 && mkdir /tmp/cbm && tar -xzf /tmp/cbm.tgz -C /tmp/cbm \
 && install -m 0755 "$(find /tmp/cbm -type f -name codebase-memory-mcp | head -n1)" /usr/local/bin/codebase-memory-mcp && rm -rf /tmp/cbm*

# Tools live in root-owned prefixes so the (non-root) agent cannot replace them. npm's registry
# integrity check covers these; there is no lockfile, so the exact versions above are the pin.
RUN npm install -g --prefix /opt/npm-global \
      "@anthropic-ai/claude-code@${CLAUDE_CODE_VERSION}" \
      "yaml-language-server@${YAML_LS_VERSION}" \
      "vscode-langservers-extracted@${LANGSERVERS_EXTRACTED_VERSION}" \
      "bash-language-server@${BASH_LS_VERSION}" \
 && npm cache clean --force \
 && dotnet tool install csharp-ls --version "${CSHARP_LS_VERSION}" --tool-path /opt/dotnet-tools \
 && rm -rf /root/.dotnet /root/.nuget /root/.local /tmp/*

ENV PATH=/opt/npm-global/bin:/opt/dotnet-tools:${PATH} \
    CLAUDE_CONFIG_DIR=/home/vscode/.claude \
    DISABLE_AUTOUPDATER=1

# Volume mount points must exist with the right owner: Docker copies that ownership into a new
# named volume on first use, so the non-root user can write to it. Then strip every setuid and
# setgid bit so nothing in the image can raise privileges.
RUN install -d -o vscode -g vscode /home/vscode/.claude /home/vscode/.nuget /home/vscode/.nuget/packages /home/vscode/.cache /workspaces \
 && (find / -xdev -type f \( -perm -4000 -o -perm -2000 \) -exec chmod a-s {} + 2>/dev/null || true)

COPY allowed-hosts.txt init-firewall.sh entrypoint.sh /usr/local/share/devcontainer/
# Language-server plugins for Claude Code, delivered as root-owned managed settings (the agent cannot edit them).
COPY claude/plugins /usr/local/share/devcontainer/claude-plugins
COPY claude/managed-settings.json /etc/claude-code/managed-settings.d/10-devcontainer.json
RUN chmod 0755 /usr/local/share/devcontainer/*.sh \
 && chmod 0644 /usr/local/share/devcontainer/allowed-hosts.txt /etc/claude-code/managed-settings.d/10-devcontainer.json \
 && chmod -R a+rX /usr/local/share/devcontainer/claude-plugins

# PID 1 stays root only to program the firewall (needs NET_ADMIN); every interactive process is
# started by the tooling as `vscode` (remoteUser) and has no capabilities. See docs/DevContainer.md.
USER root
ENTRYPOINT ["/usr/local/share/devcontainer/entrypoint.sh"]
