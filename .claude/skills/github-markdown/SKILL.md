---
name: github-markdown
description: >-
   Author rich GitHub Flavored Markdown — individual pages or interlinked
   collections — for READMEs, docs, wikis, issues, pull requests, and
   discussions. Covers the non-obvious GFM surface: tables, collapsed sections,
   code blocks and syntax highlighting, snippet permalinks, Mermaid/GeoJSON/STL
   diagrams, LaTeX math, autolinked references, task lists, alerts, footnotes,
   file attachments, and issue/PR closing keywords, while explicitly
   distinguishing plain CommonMark from GFM and GitHub-only extensions. Use
   when writing or improving a GitHub README, docs page, wiki, issue, or PR
   comment; when asked for "GFM", "GitHub markdown", or "advanced markdown"; or
   when building a set of interlinked markdown pages.
license: Apache-2.0
user-invocable: true
metadata:
   source: https://github.com/rheone/miscellaneous-agentic-tooling
   author: Robert Engelhardt <rheone@gmail.com>
   version: "1.0.0"
   info: >-
      Grounded in the GitHub writing docs and the GitHub Flavored Markdown
      Spec: <https://github.github.com/gfm/>. Math syntax follows the Wikibooks
      LaTeX/Mathematics guide: <https://en.wikibooks.org/wiki/LaTeX/Mathematics>.
---

# GitHub Markdown

Authors pages and collections in **GitHub Flavored Markdown (GFM)** — the dialect
GitHub renders in READMEs, wikis, issues, pull requests, and discussions.

Cardinal framing: three tiers, not one — **CommonMark** (standard Markdown),
**GFM** (the spec extensions), and **GitHub.com-only** features. Know which tier
each construct belongs to before you use it; the wrong tier breaks on other
renderers. Start with [references/common-vs-gfm.md](references/common-vs-gfm.md).

## Reference index

| You want to...                                                            | Read                                              |
| ------------------------------------------------------------------------- | ------------------------------------------------- |
| Know whether a feature is standard, GFM, or GitHub-only                   | `references/common-vs-gfm.md`                     |
| Build or align a table (incl. escaping pipes, alignment)                  | `references/tables.md`                            |
| Hide content behind an expandable toggle                                  | `references/collapsed-sections.md`                |
| Fenced code blocks, syntax highlighting, diff highlighting                | `references/code-blocks.md`                       |
| Link to a specific line/range of a file, or quote a snippet               | `references/permanent-links.md`                   |
| Render LaTeX math (`$…$`, `$$…$$`, `math` fence)                            | `references/math.md`                              |
| Refer to issues, PRs, SHAs, users; autolinking behavior                   | `references/autolinks.md`                         |
| Interactive checkboxes / task lists                                       | `references/tasklists.md`                         |
| Colored callout boxes (NOTE/TIP/IMPORTANT/WARNING/CAUTION)                | `references/alerts.md`                            |
| Upload and embed images, PDFs, video                                      | `references/attaching-files.md`                   |
| Auto-close issues from a PR ("Fixes #12")                                 | `references/issue-pr-keywords.md`                 |
| A Mermaid diagram                                                         | `references/diagrams/mermaid.md`                  |
| An interactive GeoJSON/TopoJSON map                                       | `references/diagrams/geojson-topojson.md`         |
| An interactive 3D model from ASCII STL                                    | `references/diagrams/stl-3d.md`                   |

**Do not teach basics.** CommonMark staples (headings, emphasis, links, lists,
blockquotes, inline code) are assumed known. Only GFM/GitHub-specific behavior is
spelled out, with the boundary in `common-vs-gfm.md`.

## Quick start

A minimal but representative README page — note the table, alert, code fence,
and a relative link to a sibling page:

````markdown
# acme-lib

[Install](docs/INSTALL.md) · [Contributing](docs/CONTRIBUTING.md)

> [!NOTE]
> Requires Node.js 20+.

| API            | Description                     |
| :------------- | :------------------------------ |
| `parse(src)`   | Parse GFM into an AST           |
| `render(ast)`  | Emit HTML from the AST          |
| `lint(ast)`    | Report style violations         |

## Usage

```ts
import { parse } from "acme-lib";
const ast = parse("# Hello, *world*");
```

## Roadmap

- [x] Parser
- [ ] Renderer
- [ ] Linter
````

## Workflows

### Single page

1. Identify the surface (README, wiki page, docs page, issue, PR, discussion).
2. Match the topics to the reference index; read the matching files.
3. Write with the target tier in mind — assume the page may also be read on
   other renderers where GFM/GitHub-only features degrade.
4. Self-check: `common-vs-gfm.md` boundary respected, every fence closed,
   every reference file's gotchas honored.

### Interlinked collection

1. Plan the page set and the navigation (relative links, section anchors).
2. Keep each page self-contained but cross-linked; put shared rules once.
3. Use relative links (`docs/x.md`, `../README.md`) so links survive clone/branch.
4. Verify every internal link resolves from its source file's location.

### Issue / PR comment

1. Favor brevity; use collapsed sections for debug dumps or long logs.
2. Reference issues/PRs with `#123`, SHAs, and `@mentions` — they unfurl.
3. Use task lists for multi-step work; use closing keywords in the PR body.
4. Note the surface limits: no autolinked refs in wikis/repo files, no
   interactive task lists outside issues/PRs.

## Checklist-driven execution

Create a `todowrite` checklist on invocation. Tick items off as you go; if
blocked, note the blocker and ask the user.

1. **Clarify target** — surface (README/wiki/issue/PR/discussion), single page
   vs collection, audience, whether other renderers matter.
2. **Map topics** — which reference index rows apply? Read those files.
3. **Read the boundary** — `common-vs-gfm.md` so common vs GFM vs GitHub-only
   stays explicit.
4. **Write** — compose the page(s) with correct tier-aware syntax and examples.
5. **Self-check** — fences closed, escaping correct, links resolve, alerts/tables
   follow their file's gotchas.
6. **Verify** — if Mermaid is used, run the `mermaid-diagram-generator` skill's
   validator when available.

## See also

- **`mermaid-diagram-generator`** — the full Mermaid catalog (25+ diagram types
  with per-type reference files and a validator). This skill covers Mermaid only
  as a high-level overview in `references/diagrams/mermaid.md`.
- **Advanced topics beyond this skill** — for content this skill does not cover
  (e.g. non-GitHub renderers, pandoc, or exotic diagram tooling), look to
  external tooling documentation; keep the output tier-aware so it still renders
  acceptably on GitHub.