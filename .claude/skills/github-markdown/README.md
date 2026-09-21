# GitHub Markdown Skill

An agentic skill for authoring **GitHub Flavored Markdown (GFM)** — individual
pages or interlinked collections — for READMEs, docs, wikis, issues, pull
requests, and discussions.

## What it covers

The non-obvious GFM surface, with the common-vs-GFM boundary kept explicit:

- Tables, collapsed sections, code blocks & syntax highlighting, snippet
  permalinks
- Diagrams: Mermaid (overview — delegates to `mermaid-diagram-generator`),
  GeoJSON/TopoJSON maps, ASCII STL 3D models
- LaTeX math via MathJax, autolinked references, task lists, alerts, footnotes,
  file attachments, issue/PR closing keywords
- A single source of truth for what is **CommonMark** vs **GFM spec** vs
  **GitHub.com-only** ([references/common-vs-gfm.md](references/common-vs-gfm.md))

## Layout

- [SKILL.md](SKILL.md) — primary agent-facing reference
- [README.md](README.md) — user-facing documentation (this file)
- `references/`
  - [common-vs-gfm.md](references/common-vs-gfm.md) — CommonMark vs GFM vs GitHub.com boundary
  - [tables.md](references/tables.md) — tables
  - [collapsed-sections.md](references/collapsed-sections.md) — collapsed `<details>` sections
  - [code-blocks.md](references/code-blocks.md) — fenced code blocks & highlighting
  - [permanent-links.md](references/permanent-links.md) — snippet permalinks
  - [math.md](references/math.md) — LaTeX math
  - [autolinks.md](references/autolinks.md) — autolinked references & URLs
  - [tasklists.md](references/tasklists.md) — task lists
  - [alerts.md](references/alerts.md) — alerts (callouts)
  - [attaching-files.md](references/attaching-files.md) — attaching files
  - [issue-pr-keywords.md](references/issue-pr-keywords.md) — issue/PR closing keywords
  - `diagrams/`
    - [mermaid.md](references/diagrams/mermaid.md) — Mermaid diagrams
    - [geojson-topojson.md](references/diagrams/geojson-topojson.md) — GeoJSON/TopoJSON maps
    - [stl-3d.md](references/diagrams/stl-3d.md) — ASCII STL 3D models

## Sources

Grounded in the [GitHub writing & formatting docs](https://docs.github.com/en/get-started/writing-on-github)
and the [GitHub Flavored Markdown Spec](https://github.github.com/gfm/).
LaTeX math follows the [Wikibooks LaTeX/Mathematics guide](https://en.wikibooks.org/wiki/LaTeX/Mathematics).

## License

Apache-2.0
