# Renderers

Which Mermaid version each markdown renderer ships, so a diagram is written for what will actually draw it. Versions drift; the "as of" column is a date, not a guarantee. Consult this reference when the user names where the diagram will be displayed, or when choosing between a v12 diagram and its v11 fallback.

**Rule.** Gate by Mermaid version number, not by renderer name. Look up the renderer's version here, then compare it with the minimum version in the type's "v11 fallback" section. When the target is unnamed, write for Mermaid 12 and state the minimum version in the reply.

## Versions (as of 2026-09-19)

| Renderer | Mermaid version | Applies to | Source |
| --- | --- | --- | --- |
| GitLab | 11.16.1 | GitLab 19.4.0 (19.3 shipped 11.13.0) | `package.json` alias `mermaid-v11` in gitlab-org/gitlab at tag `v19.4.0-ee`; docs: "GitLab supports Mermaid version 11" (https://docs.gitlab.com/user/markdown/) |
| Obsidian | 11.13.0 | Desktop 1.13.x, unchanged through 1.14.2 early access | Changelog "Upgraded Mermaid to 11.13.0" (https://obsidian.md/changelog/2026-05-28-desktop-v1.13.0/) |
| VS Code built-in Markdown preview | 11.17.0 resolved (`^11.16.1` range) | Built-in since VS Code 1.121; figure from `main` | `extensions/mermaid-markdown-features` in microsoft/vscode |
| GitHub | unstated | live | Put `info` in a ```mermaid fence to print the version in use (GitHub docs, "Creating diagrams") |
| Azure DevOps wiki | unstated | docs updated 2026-06-15 | Documents 11 types only: sequence, Gantt, flowchart, class, state, user journey, pie, requirement, gitgraph, ER, timeline; "limited syntax support" (Microsoft Learn, wiki markdown guidance) |

None of these is known to ship Mermaid 12.

## What follows for each

- **GitLab (11.16.1).** `usecase-beta` and `agentflow-beta` fail. Railroad, swimlane, cynefin, treeView and the 11.16.0 syntax exist in 11.16.1 (GitLab's docs confirm `treeView-beta`; the rest follow from the version). Flowchart shapes `folder`, `bucket`, `console`, `browser`, `person`, collapsible subgraphs and XY legends (11.17.0) are not available.
- **Obsidian (11.13.0).** Also lacks everything after 11.13.0: `treeView-beta`, `wardley-beta` (11.14.0), `eventmodeling` (11.15.0), `cynefin-beta`, `swimlane-beta`, `railroad-*-beta` (11.16.0), the ER `?` attribute suffix, XY per-point labels. Venn and Ishikawa exist. Since Obsidian 1.13, Mermaid rendering is off until the user allows it per vault.
- **VS Code (11.17.0).** Has everything through 11.17.x; lacks the v12-only types and the v12 default look.
- **GitHub.** Unknown until probed. Do not assume v12 types or the neo/ELK defaults; ask the user to run `info`, or deliver the fallback.
- **Azure DevOps.** Treat only the 11 documented types as supported; every `-beta` type and every newer type as unsupported. Its docs list `flowchart` as unsupported syntax (use `graph`) and `---->` long arrows as unsupported.

## Renderers with no version to gate on

For GitHub and Azure DevOps (and any renderer not listed), the version is unstated. Render the diagram in the target itself and read the result, rather than gating: an unsupported type shows a parse error, not a blank. Mermaid Chart and Notion also state no version.

## Refreshing this table

Recheck each source before trusting a row older than a few weeks; the "as of" date is when it was read. Each row cites where the version came from, and the same source is the place to look for the next value. The full notes behind this table, with every source URL, are in `research/mermaid-markdown-renderers-research.md` at the skill root.
