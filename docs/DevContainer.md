# Dev container for agentic development

An isolated **Linux** container (Docker Desktop with the WSL 2 backend, on a Windows host) in which Claude
Code can build, test and navigate this repository. Its blast radius is the container: it cannot read the
Windows file system, has no host credentials, and can reach only an allow-list of hosts. Windows containers
are deliberately not used (the language servers, Claude Code and the code-graph tool all target Linux).

This is a **best-effort** boundary, not a hard one. Read [Safety model and limits](#safety-model-and-limits).

## Prerequisites

- Windows 11 with **WSL 2** and **Docker Desktop** (WSL 2 backend). Give WSL at least 8 GB of memory
  (`%UserProfile%\.wslconfig`, `[wsl2] memory=8GB`, then `wsl --shutdown`); the first restore and the tests
  are the heaviest steps.
- **VS Code** with the **Dev Containers** extension (`ms-vscode-remote.remote-containers`).
- A GitHub account that can push to this repo, for the token step below.

## Open it as a container volume (the only supported way)

The repository must live in a **named Docker volume**, not a bind mount of your Windows folder. A bind mount
would make `C:\...` readable and writable from inside the container, and is slow and CRLF-prone.

1. In VS Code run **Dev Containers: Clone Repository in Container Volume...**, paste the repo URL
   (`https://github.com/rheone/poc_mediatr-union_types.git`) and accept the defaults. Or open
   `vscode://ms-vscode-remote.remote-containers/cloneInVolume?url=https://github.com/rheone/poc_mediatr-union_types.git`
   (the link form is from the [VS Code Dev Containers docs](https://code.visualstudio.com/docs/devcontainers/containers)).
2. Wait for the build (a few minutes the first time) and for `postCreateCommand`, which runs
   `verify-isolation.sh`, `dotnet tool restore`, `dotnet restore` and the first code-graph index.

`devcontainer.json` does not set `workspaceMount`. If you use *Reopen in Container* from a local folder,
VS Code bind-mounts that folder, and `post-create.sh` then **fails** (`verify-isolation.sh` detects the
`9p`/`virtiofs` mount) instead of continuing. That is intended; reopen with the command above.

### VS Code settings on your machine

The repo cannot force these, because they are *user* settings of the host VS Code. Set them before opening the
container so credentials do not flow in implicitly:

| Setting (User settings) | Value | Why |
| --- | --- | --- |
| `dev.containers.copyGitConfig` | `false` | Do not copy your host `~/.gitconfig` (identity, helpers) into the container |
| `dev.containers.gitCredentialHelperConfigLocation` | `"none"` | Do not install a helper that forwards your host Git credential manager (a known issue, [vscode-remote-release#4426](https://github.com/microsoft/vscode-remote-release/issues/4426), reports it still being copied in some versions) |
| SSH agent | do not run `ssh-agent` with keys loaded when opening the container | VS Code forwards a running agent automatically; there is no per-project switch |

Setting names come from the Dev Containers issue tracker and were not checkable offline. The **arbiter is
`verify-isolation.sh`**: it fails if an SSH agent socket, a credential helper or `~/.ssh` etc. show up.

## First run

1. **Log in to Claude Code inside the container**: `claude`, then follow the printed URL (open it in your host
   browser and paste the code back). The login lands in `~/.claude`, which is its own named volume
   (`mediatr-union-poc-claude`), separate from your host's `~/.claude`, and survives rebuilds.
   Accept the workspace-trust prompt and approve the `codebase-memory-mcp` server and the `mattpocock`
   marketplace when asked.
2. **Authenticate `gh`/git inside the container with a scoped token, never a host credential.** Create a
   fine-grained personal access token limited to this one repository, with *Contents: read and write* (add
   *Pull requests* only if you want `gh pr create`), expiring in 30 days or less. Then, in the container:

   ```bash
   read -rs GH_PAT && echo "$GH_PAT" | gh auth login --with-token && unset GH_PAT
   gh auth setup-git
   ```

   The token is stored in the container's home directory (not a volume), so a rebuild forgets it. Revoke it on
   GitHub when you are done. Data leaves the container only through `git push` and explicit file export.
3. Run `.devcontainer/smoke-test.sh` once (see [Smoke test](#smoke-test)).

## Isolation model

| What | Shared with the host? | How it is enforced | Verified by `verify-isolation.sh` |
| --- | --- | --- | --- |
| Repository | No, lives in a named volume | "Clone Repository in Container Volume"; no `workspaceMount` | Workspace not on `9p`/`virtiofs`/`fuse`/`cifs`/`nfs` |
| Other host files, home, `.ssh`, `.aws`, browser profile | No | The only mounts are the workspace volume and two named volumes | No host-backed filesystem mounted; no `/mnt/c`, `/host_mnt`, `/run/desktop`; no `~/.ssh`, `~/.aws`, ... |
| Docker socket | No | Never mounted, no Docker CLI | Socket absent, `DOCKER_HOST` unset, no `docker` binary |
| Privileges | No `--privileged`, no sudo, non-root user | `devcontainer.json`: `runArgs` `--cap-drop=ALL`, `securityOpt` `no-new-privileges`; the `egress-firewall` Feature's `capAdd` adds `NET_ADMIN`, `NET_RAW`; the `lockdown` Feature purges sudo and strips setuid bits | uid is not 0, no sudo, no setuid files, `NoNewPrivs=1`, seccomp active, `CapEff=0`, `CapBnd` is at most `NET_ADMIN`+`NET_RAW` |
| Devices, GPU, USB, clipboard | No | No `--device`, no GPU flags, no X11/Wayland mounts | No `/dev/dri`, `nvidia*`, `bus/usb`, `kvm`, `fuse`, audio/video; `DISPLAY`/`WAYLAND_DISPLAY` unset |
| Credentials | No implicit flow | Host VS Code settings above; `containerEnv` has static values only, no `${localEnv:...}` | No `SSH_AUTH_SOCK`, no credential helper, no token variables |
| Network | Outbound allow-list only | `egress-firewall` Feature: `features/egress-firewall/init-firewall.sh` (below) | `example.com`, `1.1.1.1`, IPv6 and the host gateway are unreachable; GitHub, Anthropic and NuGet are reachable |
| Ports | Only the API's `5233` | `forwardPorts`, `otherPortsAttributes: ignore`, `remote.autoForwardPorts: false` | Not checkable from inside; check the Ports panel |
| Claude login and memory | Own volume | `mediatr-union-poc-claude` mounted at `~/.claude` | `HOME/.claude` is its own mount |
| NuGet cache | Own volume | `mediatr-union-poc-nuget` at `~/.nuget/packages` | Own mount |
| Tools and firewall config | Read-only to the user | `lockdown` Feature re-owns nvm to root; `agent-tools` installs into root-owned `/opt`, `/usr/local/bin`; the firewall list is in root-owned `/usr/local/share/egress-firewall` | Not writable by the user |

Run `bash .devcontainer/verify-isolation.sh` after every rebuild; it exits non-zero on any violation.
I confirmed it also fails (exit 1) when the workspace is a Windows bind mount and when the Docker socket is mounted.

## Egress firewall

`.devcontainer/features/egress-firewall/init-firewall.sh` is adapted from the reference in
[anthropics/claude-code `.devcontainer`](https://github.com/anthropics/claude-code/tree/main/.devcontainer),
which I read before writing it. Differences: it is started as root by the Feature's `entrypoint` (so the user needs no
sudo), the host/gateway network is **not** allowed (the reference allows the host's /24), SSH is not allowed,
IPv6 is dropped, and the host list is a file.

- Default policy is DROP for INPUT, FORWARD and OUTPUT; loopback, replies to allowed traffic, DNS to private
  ranges only, and destinations in an `ipset` are allowed; everything else is rejected immediately.
- The set holds GitHub's published ranges (`api.github.com/meta`, web + api + git) and the addresses of each host in
  `allowed-hosts.txt`, resolved 20 times at start and then every 15 seconds (add-only), because CDN-backed hosts
  such as `api.nuget.org` (Azure Front Door) rotate addresses. Right after a rotation an allowed host can be
  rejected for a few seconds; `verify-isolation.sh` retries the allowed checks for that reason.
- **Fail closed:** if any step fails, or the self-check reaches `example.com` or cannot reach `api.github.com`,
  the script exits non-zero, `entrypoint.sh` kills the CLI's wrapper shell (so the image's default command never starts) and the
  container stops; the tooling then reports the start as failed. Observed for an unresolvable host entry and for a missing `NET_ADMIN` capability.

| Allowed host | Needed for | Source |
| --- | --- | --- |
| `api.anthropic.com`, `claude.ai`, `platform.claude.com` | Claude Code API and login | [Claude Code network config](https://code.claude.com/docs/en/network-config#network-access-requirements) |
| GitHub `web`, `api`, `git` ranges | git over HTTPS, `gh`, cloning plugin marketplaces | `api.github.com/meta` |
| `api.nuget.org` | `dotnet restore`, `dotnet tool restore`, NuGet audit | Observed: a clean restore and tool restore through the firewall succeeded with only this host |
| `update.code.visualstudio.com`, `marketplace.visualstudio.com`, `vscode.blob.core.windows.net` | VS Code Server and extension install | Same hosts as the Anthropic reference; **not exercised** here (I used the Dev Containers CLI, not VS Code). Delete them for a CLI-only setup |

Not allowed by default: `registry.npmjs.org` (only needed at image build, for `npx` MCP servers and npm plugins),
`downloads.claude.ai` (native installer, plugin executables), `raw.githubusercontent.com`, telemetry hosts
(`CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC`, `DISABLE_TELEMETRY`, `DISABLE_ERROR_REPORTING` are set), and
`mcp-proxy.anthropic.com` (`ENABLE_CLAUDEAI_MCP_SERVERS=false`).

**To allow a new host:** add a line to `.devcontainer/features/egress-firewall/allowed-hosts.txt` and run *Dev Containers: Rebuild
Container*. The list is baked into the image so that the agent, which can edit the workspace but not the image,
cannot widen its own network access. Wildcards are not supported; list each hostname.

### Documentation MCP servers (optional, off by default)

`.mcp.json` also defines `microsoft-learn` (`https://learn.microsoft.com/api/mcp`) and `context7`
(`https://mcp.context7.com/mcp`), both listed in `disabledMcpjsonServers` and **not** in the firewall list. To
enable both together: uncomment `learn.microsoft.com` and `mcp.context7.com` in `allowed-hosts.txt`, remove the
two names from `disabledMcpjsonServers` in `.claude/settings.json`, rebuild, and approve them under `/mcp`.
Both are remote services: your queries leave the container to them. Context7's README says its server takes an
API key in an `Authorization: Bearer` header; this repo does not configure one. Neither was tested.

## What is in the image

`.devcontainer/` is a stock base image, four published Features and three small local Features. There is no Dockerfile.

```text
.devcontainer/
  devcontainer.json          base image, Feature list and versions, isolation settings, volumes, env
  devcontainer-lock.json     digests of the published Features (`devcontainer upgrade`)
  features/
    egress-firewall/         iptables/ipset, init-firewall.sh, allowed-hosts.txt, root entrypoint
    agent-tools/             pinned Claude Code, language servers, code-graph binary, rg, shellcheck, managed settings
    lockdown/                no sudo or ssh client, no setuid bits, root-owned nvm, volume mount points
  post-create.sh  verify-isolation.sh  smoke-test.sh  lsp-query.mjs
```

### Published pieces

| Piece | Version | Maintainer | Pinned how | Integrity check |
| --- | --- | --- | --- | --- |
| `mcr.microsoft.com/devcontainers/base` (Ubuntu 24.04) | `3.0.8-noble` | Microsoft (dev container team) | Tag plus image digest in `devcontainer.json` | Docker verifies the digest. Ships user `vscode` (uid 1000), git and `common-utils` (its image metadata lists `common-utils:2` and `git:1`), so those are not repeated |
| Feature `ghcr.io/devcontainers/features/dotnet` | `2.5.0`, option `version` = the `global.json` SDK | devcontainers/features | Exact tag, digest in `devcontainer-lock.json` | **None for the SDK archive**: the Feature runs its vendored `dotnet-install.sh`, which downloads over HTTPS from `builds.dotnet.microsoft.com` and only checks the installed version afterwards (source read). No SHA-512 of the SDK archive is pinned. `smoke-test.sh` fails if `dotnet --version` differs from `global.json` |
| Feature `ghcr.io/devcontainers/features/node` | `2.1.0`, options `version` = `24.21.0`, `nvmVersion` = `0.40.7` | devcontainers/features | Exact tag and exact Node version | Installed by nvm, which checks the archive against `SHASUMS256.txt` from nodejs.org; the hash is not pinned in this repo |
| Feature `ghcr.io/devcontainers/features/github-cli` | `1.1.2`, option `version` = `2.101.0` | devcontainers/features | Exact tag and exact `gh` version | `installDirectlyFromGitHubRelease: false` installs from the GPG-signed apt repository at cli.github.com (signing key fetched by fingerprint from a keyserver at build time); the default would `dpkg -i` an unverified .deb |

`ghcr.io/anthropics/devcontainer-features/claude-code` (MIT, Anthropic) exists but is not used: it has no options (no way to pin a version, it runs
`npm install -g @anthropic-ai/claude-code` unpinned), installs with whatever `npm` is first on the path (the nvm prefix, which the node Feature makes writable by the user) and was last changed in June 2025.
`ripgrep` and `shellcheck` community Features (`devcontainers-extra`) download release binaries without a checksum through a
nested, separately versioned installer, so both come from Ubuntu's archive instead (ripgrep 14.1.0, shellcheck 0.9.0).

### Local Features (custom, and why)

| Feature | What it does | Why no published Feature does it |
| --- | --- | --- |
| `egress-firewall` | Installs iptables, ipset and dig; installs `init-firewall.sh`, `allowed-hosts.txt` and the entrypoint. Its metadata carries `entrypoint` and `capAdd` (`NET_ADMIN`, `NET_RAW`) | The default-deny policy is this repo's own script; the Anthropic reference firewall is a copy-in script, not a Feature |
| `agent-tools` | Claude Code, `csharp-ls`, `marksman`, the YAML/JSON/Bash language servers and `codebase-memory-mcp` at exact versions (Feature options, defaults in `devcontainer-feature.json`), SHA-256 checked where a binary is downloaded, in root-owned prefixes; the managed settings and LSP marketplace | See the Claude Code note above; the rest are tools with no maintained Feature |
| `lockdown` | Removes sudo and the ssh client, strips setuid/setgid bits, re-owns nvm to root, creates the volume mount points for `vscode` | Isolation policy specific to this repo. Must run last (`overrideFeatureInstallOrder`) |

`agent-tools` puts these on `PATH`: `/opt/npm-global/bin` (Claude Code and the npm language servers), `/opt/dotnet-tools` (`csharp-ls`); binaries go
to `/usr/local/bin`. `DOTNET_ROLL_FORWARD=Major` is set by that Feature because `csharp-ls` targets .NET 10 and only the .NET 11 runtime is installed.
`csharpier` 1.3.0 and `husky` 0.9.1 come from `dotnet tool restore`.

The built image is about 2.6 GB (measured with `docker images`). The base image adds zsh, Oh My Zsh and a from-source git that the tools here do not need.

## Language servers

Claude Code loads LSP servers from plugins (per the [plugins reference](https://code.claude.com/docs/en/plugins-reference):
`lspServers` in a plugin or marketplace entry, with `command` and `extensionToLanguage`).

- **C#:** the official `csharp-lsp@claude-plugins-official` plugin (already enabled in `.claude/settings.json`; its
  entry runs `csharp-ls`, which is on `PATH`).
- **Markdown, JSON, YAML, Bash:** `.devcontainer/features/agent-tools/plugins` is a small marketplace (`devcontainer-lsp`) copied into the
  image and enabled by `/etc/claude-code/managed-settings.d/10-devcontainer.json`. Managed settings are used so these
  servers (which do not exist on your Windows host) are not enabled in your host's Claude Code sessions, and the agent
  cannot edit them.

### Does any C# server understand `union`?

Checked by running each server headless against the repo (`.devcontainer/lsp-query.mjs`), asking for the document symbols of
`CreateProductResult.cs` (a `public union ...` declaration) and of a normal class file:

| Server | Regular class file | `union` file |
| --- | --- | --- |
| `csharp-ls` 0.27.0 and 0.28.0 | All symbols | Methods declared inside the union are listed; the **`union` declaration itself is missing**. `workspace/symbol` does find the union type by name |
| Roslyn language server `Microsoft.CodeAnalysis.LanguageServer` 5.4.0-2.26179.14 (newest package on the `vs-impl` feed I found; not reachable from the container firewall) | Not compared | Only the namespace; the union and its members are missing. It needs a custom `solution/open` notification from the client (sent by hand in the test); whether Claude Code sends it was not checked |

**Chosen: `csharp-ls` 0.27.0.** Neither server fully handles C# 15 `union` on this SDK, so what degrades is: the
`union` declaration is absent from document symbols and outline-style queries, and features that depend on the syntax
tree of a union file are unreliable. Ordinary types, members and cross-file references work, and the compiler
(`dotnet build`) remains the authority for diagnostics. Pull diagnostics returned nothing for the union file or the controller that
switches over unions (so no false errors were seen; whether csharp-ls supports pull diagnostics was not confirmed). Re-run the check when either server updates (see [Updating](#updating-pins)).

## Code graph (MCP)

The agent can ask "who calls this" instead of searching text. Candidates, from their READMEs, releases and licences
(pulled from GitHub, September 2026):

| Server | Licence | Last release | C# | Offline / local | Footprint | Verdict |
| --- | --- | --- | --- | --- | --- | --- |
| **codebase-memory-mcp** 0.11.0 | MIT | 2026-09-15 | Tree-sitter, plus a C# type-resolution layer | Yes: README states everything runs locally, no telemetry, no network request of its own | One static binary (~40 MB release archive), no runtime; SQLite index | **Picked** |
| Serena 1.7.0 | GPL-3.0-or-later (application), MIT (SolidLSP) | 2026-08-09 | Through an LSP (`csharp_language_server.py`, `omnisharp.py` in its tree) | Local, but downloads language servers on first use (not tested behind the firewall) | Needs `uv` and Python 3.11-3.14 | Rejected: heaviest, GPL, and its C# backend is an LSP, so it inherits the `union` gap above |
| code-graph-rag 0.0.945 | MIT | 2026-09-16 | Tree-sitter | Needs Memgraph (Docker), Qdrant and an LLM provider for natural-language queries | Python 3.12+, cmake, Docker | Rejected: needs services and sends prompts to a model provider |

**Indexing and refresh.** `post-create.sh` runs `codebase-memory-mcp cli index_repository --repo-path $PWD` (about 6 s, ~4,400
nodes and ~17,000 edges here); the MCP server started from `.mcp.json` runs a background watcher that re-indexes on
change, and `auto_index` is on. Re-index by hand with the same command. The index is a SQLite file under
`~/.cache/codebase-memory-mcp` (rebuilt on rebuild); its optional `.codebase-memory/` snapshot is git-ignored.
The local web UI is switched off (`ui_enabled false`).

**Known gap.** The Tree-sitter C# grammar predates `union`: the indexer reports about 20 files as "parse_partial"
(the union declarations and some C# 14 extension members), and models a `union` as a `Module` node. Method-level call
edges elsewhere are fine (`ProductNames.Normalize` returns its eight callers: the handlers, `Product` and the repository).

## Claude Code configuration (committed)

`.claude/settings.json` (project scope):

- **Permissions** (read-mostly, allowed without a prompt): `dotnet build|test|format|restore|tool restore`, `dotnet csharpier check`,
  `rg`, `git status|diff|log|show` and read-only `git branch` forms, `gh pr|issue|run|repo view/list/diff/checks`, and the read
  tools of the code-graph server. `git push`, network fetch tools and everything else still ask. These apply on your
  Windows host too; they are all read-only or local.
- **Skills:** the `mattpocock-skills` plugin from the `mattpocock/skills` GitHub repo, pinned to commit `84fdeffd12f2ee307994d1eb6feb48173b6e0502` (plugin version 1.2.3) with `extraKnownMarketplaces` +
  `enabledPlugins` (syntax from the [marketplaces docs](https://code.claude.com/docs/en/plugin-marketplaces)). The repo's own
  `.claude/skills/*` (`csharp-union`, ...) load automatically.
- **Hook:** `PostToolUse` on `Edit|Write` runs `.claude/hooks/format-cs.sh`, which formats the edited `.cs` file with
  `dotnet format whitespace --folder --include <file>` (about 1.3 s measured; it never blocks) and only when
  `DEVCONTAINER=true`. It formats whitespace only; `dotnet csharpier` still runs in the pre-commit hook.
- **MCP:** `.mcp.json` registers `codebase-memory-mcp` (enabled) and the two optional servers (disabled).

`--dangerously-skip-permissions` is not set anywhere. The firewall and the absence of host data are what make a
relaxed permission mode *reasonable* in this container, but turning one on (`--permission-mode ...`) is your deliberate
choice each time, not something the repo configures. Even then, the agent can still read whatever is in the container:
the repo, and the Claude and GitHub credentials you entered.

## Smoke test

`bash .devcontainer/smoke-test.sh` (inside the container; about 1.5 minutes warm) runs: SDK equals `global.json`;
`dotnet build` and `dotnet test`; `verify-isolation.sh`; headless LSP symbol queries against `csharp-ls`, `marksman`
and `bash-language-server`; and a code-graph `trace_path` asking who calls `ProductNames.Normalize`. It reports the
`union` symbol gap as a note, not a failure. Manual, because it needs your login: run `claude`, then `/mcp`
(`codebase-memory-mcp` connected) and `/plugin` (`mattpocock-skills`, `csharp-lsp` enabled).

## Updating pins

| What | Where to edit | Notes |
| --- | --- | --- |
| .NET SDK | `global.json`, then `features["...dotnet:2.5.0"].version` in `devcontainer.json` | Must be identical; `smoke-test.sh` checks. Also the SDK version in `.github/workflows/ci.yml` if the channel changes |
| Node | `version` option of the node Feature in `devcontainer.json` | |
| `gh` | `version` option of the github-cli Feature in `devcontainer.json` | Must exist in the cli.github.com apt repository |
| Published Features | The tag in `devcontainer.json`, then `devcontainer upgrade --workspace-folder .` | Dependabot (`devcontainers` ecosystem, monthly) opens these PRs and updates `devcontainer-lock.json`. Do not merge without a rebuild and the smoke test |
| Base image | The tag and digest in `devcontainer.json` `image` | `docker pull <tag>` and copy `RepoDigests`. Dependabot does not cover this |
| Claude Code, language servers, marksman, codebase-memory-mcp | Feature options in `features/agent-tools/devcontainer-feature.json` (defaults), or set the option in `devcontainer.json` | New SHA-256 with a new binary version (`marksmanSha256`, `codebaseMemorySha256`; codebase-memory publishes `checksums.txt` with the release). `csharpLsVersion` must match `.config/dotnet-tools.json` |
| mattpocock skills | `sha` in `.claude/settings.json` | |
| Allowed hosts | `features/egress-firewall/allowed-hosts.txt` | Rebuild |

After a bump, rebuild, run the smoke test, and re-run the `union` symbol check in `lsp-query.mjs`.

## Troubleshooting

- **Container exits at start / "init-firewall: FAILED":** read the container log. Usually GitHub's `meta` endpoint or a host in
  `features/egress-firewall/allowed-hosts.txt` did not resolve (no network, VPN, DNS). The container is meant to stop rather than run open.
- **A tool cannot reach a host:** it is not on the allow-list; see [Egress firewall](#egress-firewall). After a CDN
  rotation, retry after 15 seconds.
- **post-create fails on "workspace is on a host filesystem":** you opened a local folder. Use *Clone Repository in Container Volume*.
- **Slow or out of memory:** raise the WSL memory limit (Prerequisites). First `dotnet restore` fills the NuGet volume; later ones are fast.
- **Line endings:** shell scripts must be LF. `.gitattributes` forces `eol=lf` for `*.sh` and `.devcontainer/**`, and the volume clone is
  made on Linux, so this only bites if you edit these files with a CRLF-forcing tool on Windows.
- **Ports:** only `5233` is forwarded; run the API with `dotnet run --project src/MediatrUnionPoc.Api`.
- **Start over:** *Dev Containers: Rebuild Container* keeps the Claude and NuGet volumes; delete the volumes in Docker Desktop for a clean slate
  (that also logs Claude out).

## Safety model and limits

- A container is **not** a hard security boundary. It shares the WSL 2 VM's Linux kernel with every other container, so a kernel or
  runtime vulnerability could escape it. The measures here (no privileged mode, dropped capabilities, `no-new-privileges`, default
  seccomp, no socket or host mounts) make an escape harder; they do not make it impossible.
- **`NET_ADMIN` and `NET_RAW` stay in the container's capability bounding set** because PID 1 (root) must program the firewall. The user
  the tooling attaches as has no effective capabilities, no sudo and no setuid binaries, and `no-new-privileges` blocks regaining them, so
  the agent cannot edit the rules; but anything that becomes root inside the container (a kernel bug, or `docker exec -u root` from a
  host that already controls Docker) could. The alternative, dropping the capabilities after start, would need more capabilities to do it.
- The **firewall is best-effort**: it filters by IP resolved from names, so an allowed host that shares addresses with other tenants
  (a CDN, GitHub's ranges, Azure Front Door) also admits them; DNS to private-range resolvers is open, so DNS tunnelling is possible;
  and it stops accidents and casual exfiltration, not a determined actor. Allowed hosts are also exfiltration paths: the agent can
  `git push` your code to any GitHub repository your token can write to, which is why the token must be repository-scoped.
- The agent can read everything inside the container, including your Claude login and the GitHub token you entered there.
- Whatever is on the allowed hosts is trusted input: plugins from `mattpocock/skills` and `claude-plugins-official` are code and
  prompts fetched from GitHub. The skills marketplace is pinned to a commit; the official one is not.
- Not covered: the Dev Containers host-side features you may enable (extension forwarding, dotfiles), VS Code itself, and Docker Desktop.

## Verification status

See "As built" under Step 9 in [Hardening-Plan.md](Hardening-Plan.md) for exactly what was run and what was not.
