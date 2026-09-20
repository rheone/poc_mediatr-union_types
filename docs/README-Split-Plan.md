# Plan: splitting the root README into a documentation set

Status: **implemented.** The root `README.md` is a 132-line front door and the rest of the former 2,906-line
README lives in flat pages under `docs/`, indexed by [index.md](index.md).

The old README was the pitch, a tutorial, a reference for every endpoint and the operations manual for seven
cross-cutting features in one file: 90 sections, 148 in-page anchor links, 8 Mermaid diagrams and 2 footnotes.
Nobody could find anything in it, and every change to it produced a huge diff. The split moved the text
mechanically, without rewriting it.

## Goals and non-goals

- **Goal:** the root README is a short front door that says what the POC is, how to run it and where each topic
  lives. Every topic has one file, of a size a person reads in one sitting.
- **Goal:** no content is dropped or rewritten in the move (only the old table of contents is replaced). Edits for tone or accuracy are separate, later commits so a reviewer can tell them apart.
- **Non-goal:** restructuring the per-project READMEs under `src/` and `tests/`, or `docs/Features.md`,
  `docs/Hardening-Plan.md` and `docs/research/`. They stay where they are and are linked from the index.

## Decisions

1. **Flat layout.** Every page is `docs/<name>.md`; there are no sub-folders (the existing `Features.md`,
   `Hardening-Plan.md` and `research/` stay where they were).
2. **One operations page.** Health checks and options, CORS, rate limiting, request timeouts and the OpenAPI
   contract check are one page, [operations.md](operations.md); each stays a top-level (`#`) section in it.
3. **No "moved" table.** Old anchors are repointed to their new locations everywhere in the repository instead of
   being listed in a table; external links to old README anchors land at the top of the slim README.

## Layout

In a page, the source section's main heading becomes `#` and its sub-headings shift up by the same amount; each
page starts with `Part of the [documentation](index.md).`, and pages over about 120 lines (and the operations
page) get a contents list.

| Page | Source sections in the old README | Lines |
| --- | --- | ---: |
| `README.md` (root, slimmed) | intro and callouts, Getting started, Project layout, Motivation, condensed scope bullets linking to `scope.md`, a documentation table, the MediatR license footnote | 132 |
| `docs/index.md` | the "Core concepts" introduction plus a grouped table of every page and project README | 84 |
| `scope.md` | What this pattern provides, and its actual scope (incl. Union types as responses); What this POC demonstrates, and what it leaves out | 68 |
| `union-type.md` | The C# `union` type; Static abstract interface members | 227 |
| `case-types.md` | Case types used here; Shared case types are meaning-free | 196 |
| `no-exceptions.md` | No exceptions for expected outcomes | 128 |
| `request-lifecycle.md` | Request lifecycle, Commit vs. rollback | 101 |
| `transactions.md` | Transactions; Unit of Work | 57 |
| `value-objects.md` | Vogen | 53 |
| `adding-a-command.md` | Adding a new command or query | 97 |
| `worked-example-update.md` | Worked example: `UpdateProductCommand` | 120 |
| `extending-search-index.md` | Extending the pattern: syncing a search index, and its footnote | 69 |
| `speculative-case-types.md` | Speculative shared case types | 26 |
| `testing.md` | Testing | 30 |
| `http-contract.md` | The HTTP contract; API versioning | 126 |
| `concurrency.md` | Optimistic concurrency | 79 |
| `patch.md` | Partial updates: PATCH as JSON Merge Patch | 65 |
| `listing.md` | Listing products | 91 |
| `authorization.md` | Authorization, all sub-sections | 354 |
| `impersonation.md` | Impersonation | 132 |
| `audit.md` | Audit stream | 119 |
| `logging-and-errors.md` | Trace id and unhandled exceptions; Logging | 78 |
| `operations.md` | Health checks and options; CORS; Rate limiting; Request timeouts; OpenAPI contract check | 494 |
| `notes-and-gotchas.md` | Notes and gotchas | 52 |
| `glossary.md` | Glossary | 81 |

`authorization.md` is a candidate for a second-level split (the 97-line "Where the identity comes from" section),
and `operations.md` for one page per feature if it grows; neither is done yet.

## How it was done

A throwaway script (not kept in the repository) cut the old README by heading text, never by line number, and
wrote each target file. It recorded where every heading went, computed GitHub's anchor for each heading in its
new file (lower-case, punctuation removed, spaces to hyphens, duplicates suffixed `-1`, `-2` per file) and rewrote
every link: a same-file anchor stays `#anchor`, an anchor in another page becomes `other.md#anchor`, and a
relative path is re-based for the page's directory. An anchor with no target failed the run. The same
map rewrote the links to old README anchors in `CLAUDE.md`, the project and test READMEs and `docs/`.

## Things that had to change with it

- The three integration tests that read the README (`CorsOptionsTests`, `RateLimitingOptionsTests`,
  `RequestTimeoutOptionsTests`) read `docs/operations.md` through `OperationsDocumentation.Section`, which returns
  one top-level section so each test checks only its own feature's table.
- `CLAUDE.md`, `docs/Features.md`, `docs/Hardening-Plan.md`, the project and test READMEs and two XML doc/message
  strings name the new pages instead of README sections.
- `MediatrUnionPoc.slnx` lists the docs as a `/docs/` solution folder.
- Footnotes are per file: `eventual-consistency` lives with the search-index page and `mediatr-license` stays in
  the root README.

## As built

- **Line counts:** root README 132; pages as in the table above; `operations.md` is the largest at 494 lines.
- **Links:** of the 148 in-page anchor links, 92 were rewritten in place; the other 56 sat in the old table of
  contents (50, replaced by [index.md](index.md)) and in the scope sections (6, which moved to `scope.md` with their links rewritten, so 98 anchor links now exist across pages). 58
  relative file links were re-based, and 23 links in the project and test READMEs were repointed. Four old
  headings carried duplicate suffixes; in the new files only three headings in `operations.md` do
  (`options-1`, `options-2`, `where-it-sits-in-the-pipeline-1`).
- **Content preservation:** a check compared every non-heading, non-blank line of the old README (links reduced to
  their text) with the new files: each line appears exactly once, in its original order within its section, and
  nothing was invented. The only exempt content is the old table of contents (replaced by the index). The two scope
  sections are complete in `scope.md`; the root README keeps condensed bullets that link to them. The "Core concepts" introduction moved to the index. All 8 Mermaid diagrams are byte-identical.
- **Link checker:** `DocumentationLinkTests` in `tests/MediatrUnionPoc.ArchitectureTests` checks every relative
  link, `#fragment`, reference link and footnote in every `*.md` file (except `.claude/skills`, `docs/research`,
  build output and the ignored `.scratch` folder). It sits with the architecture tests because it enforces a rule
  over the repository, like they do, and that project has no runtime dependencies to slow it. The baseline run on
  the old tree found one broken link: the old table of contents linked `#development-environment`, a heading that
  never existed; the table of contents is gone. `MarkdownSlugTests` covers the slug function against headings
  with backticks, generics, colons, quotes and duplicates.
- **Deviations from the original plan:** flat layout, one operations page and no "moved" table (the decisions
  above); the root README is 132 lines rather than 150 to 200 because the scope sections are condensed to bullets (with the full text in `scope.md`);
  the MediatR license footnote stays a footnote rather than becoming an inline sentence.
