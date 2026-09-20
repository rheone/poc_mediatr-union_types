# Plan: splitting the root README into a documentation set

Status: **proposed, nothing moved yet.**

The root `README.md` is 2,906 lines: 90 sections, 148 in-page anchor links, 8 Mermaid diagrams and 2
footnotes. It is both the pitch, a tutorial, a reference for every endpoint, and the operations manual for
seven cross-cutting features. Nobody can find anything in it, and every change to it produces a huge diff.
This plan splits it into focused files without losing content and without breaking the links and tests that
depend on it.

## Goals and non-goals

- **Goal:** the root README becomes a short front door (about 150 to 200 lines) that says what the POC is,
  how to run it, and where each topic lives. Every topic gets one file, of a size a person reads in one sitting.
- **Goal:** no content is dropped or rewritten in the move. The split is mechanical; edits for tone or accuracy
  are separate, later commits so a reviewer can tell them apart.
- **Non-goal:** restructuring the per-project READMEs under `src/` and `tests/`, or the existing
  `docs/Features.md`, `docs/Hardening-Plan.md` and `docs/research/`. They stay and are linked from the new index.

## Proposed layout

Folders group by the question a reader has. Line ranges are in the current `README.md`.

| New file | Source sections (current lines) | Lines |
| --- | --- | ---: |
| `README.md` (root, slimmed) | intro and notes (1 to 26), Getting started and Project layout (80 to 135), Motivation (136 to 151), scope (152 to 218) condensed, plus a documentation map | ~180 |
| `docs/index.md` | table of contents for the whole set, grouped as below | ~60 |
| **`docs/pattern/`** | *the idea* | |
| `union-type.md` | The C# `union` type (225 to 397) and static abstract members (398 to 443) | 220 |
| `case-types.md` | Case types used here (444 to 497), Shared case types are meaning-free (498 to 632) | 190 |
| `no-exceptions.md` | No exceptions for expected outcomes (633 to 752) | 120 |
| `transactions.md` | Transactions (753 to 786), Unit of Work (787 to 808) | 55 |
| `value-objects.md` | Vogen (809 to 860) | 52 |
| **`docs/guides/`** | *how to work in it* | |
| `adding-a-command.md` | Adding a new command or query (861 to 956) | 96 |
| `worked-example-update.md` | Worked example: `UpdateProductCommand` (2529 to 2647) | 119 |
| `extending-search-index.md` | Extending the pattern: syncing a search index (2648 to 2708) and its footnote | 65 |
| `speculative-case-types.md` | Speculative shared case types (2709 to 2733) | 25 |
| `testing.md` | Testing (2734 to 2762) | 29 |
| **`docs/architecture/`** | | |
| `request-lifecycle.md` | Request lifecycle and Commit vs. rollback (957 to 1056) | 100 |
| **`docs/api/`** | *the HTTP surface* | |
| `http-contract.md` | The HTTP contract and API versioning (1057 to 1176) | 120 |
| `concurrency.md` | Optimistic concurrency (1279 to 1356) | 78 |
| `patch.md` | Partial updates: PATCH as JSON Merge Patch (1357 to 1420) | 64 |
| `listing.md` | Listing products (1421 to 1510) | 90 |
| **`docs/security/`** | | |
| `authorization.md` | Authorization, all sub-sections (1511 to 1850) | 340 |
| `impersonation.md` | Impersonation (1851 to 1972) | 122 |
| `audit.md` | Audit stream (1973 to 2090) | 118 |
| **`docs/operations/`** | *running it* | |
| `logging-and-errors.md` | Trace id and unhandled exceptions, Logging (1177 to 1253) | 77 |
| `health-and-options.md` | Health checks and options (1254 to 1278) | 25 |
| `cors.md` | CORS (2091 to 2177) | 87 |
| `rate-limiting.md` | Rate limiting (2178 to 2342) | 165 |
| `request-timeouts.md` | Request timeouts (2343 to 2479) | 137 |
| `openapi-contract.md` | OpenAPI contract check (2480 to 2528) | 49 |
| **`docs/reference/`** | | |
| `glossary.md` | Glossary (2814 to 2893) | 80 |
| `notes-and-gotchas.md` | Notes and gotchas (2763 to 2813) | 51 |

`authorization.md` is the largest at about 340 lines and is a candidate for a second-level split
(`authorization.md` overview plus `authorization-identity.md`, the 97-line "Where the identity comes from") but
that is deferred until the first pass is merged.

Existing files that are linked from the index and left alone: `docs/Features.md`, `docs/Hardening-Plan.md`
(rename to `docs/hardening-plan.md` only if the naming convention is standardized, in a separate commit),
`docs/research/*`, and each project's own README.

## What the slim root README keeps

Title and one-paragraph purpose; the "short answer"; the two IMPORTANT/NOTE callouts about C# 15 and the
"one opinionated take" disclaimer; Getting started (commands, persistence, the pinned SDK note); Project layout;
Motivation; the scope section reduced to a bullet list with links; a "Where to read next" table pointing at
`docs/index.md` sections; and the MediatR licence caveat (moved inline as a sentence, replacing its footnote, with
its link).

## Mechanics (do it with a script, not by hand)

1. **Freeze first.** No README edits by anyone while the split is in flight; do it in one short-lived branch.
2. **Section map as data.** Write a throwaway script (kept out of the repo) that reads a small mapping file:
   heading text to target file. It cuts each range by heading (not by line number, which is stale the moment
   anyone edits), writes the target with a one-line "Part of the documentation set" header and a
   back-link to `docs/index.md`, and records where every heading and anchor went.
3. **Anchor rewrite.** Build the old-anchor to new-`file#anchor` table from the map. Rewrite the 148 in-page
   `](#anchor)` links: same file stays `#anchor`; a different file becomes a relative link
   (`../security/authorization.md#anchor`). Any anchor with no target fails the script, so nothing is silently
   left dangling. GitHub anchor rules (lowercase, punctuation stripped, duplicates get `-1`) must be
   reproduced exactly; verify against the rendered pages for the headings that contain backticks, colons
   and `<T>`.
4. **Footnotes.** GFM footnotes are per file. `eventual-consistency` goes with the search-index guide and its
   Glossary link is rewritten; `mediatr-license` becomes an inline sentence in the root README. Check that
   each footnote reference and definition end up in the same file.
5. **Mermaid diagrams.** All 8 stay with the section that explains them. Re-render every one (the
   `mermaid-diagram-generator` skill has a validator) to confirm none depended on surrounding markup.
6. **Per-file table of contents.** Each new file over about 120 lines gets a short contents list at the top;
   the big TOC in the root README is replaced by `docs/index.md`.
7. **Prettier/CSharpier and line endings.** The new files pass `dotnet csharpier check .` and are LF
   (`.gitattributes` already normalizes text).

## Things that break unless updated

- **Three integration tests read `README.md` and assert on its content:** `CorsOptionsTests`,
  `RateLimitingOptionsTests` and `RequestTimeoutOptionsTests` (they check that the documented defaults
  match the options classes). Point each at its new file (`docs/operations/cors.md`,
  `rate-limiting.md`, `request-timeouts.md`) and keep the assertions. Grep `tests/` for any other
  `ReadAllText` of a markdown file before starting, and add the same guard to any other page that documents defaults.
- **`CLAUDE.md`** cites `README.md`'s "No exceptions for expected outcomes" and "The HTTP contract" sections by
  name; update those to the new paths. CLAUDE.md itself stays a single file.
- **`MediatrUnionPoc.slnx`** lists `README.md` as a solution file; add the new folders' files as a solution
  folder (or drop the entry) so Visual Studio still shows the docs.
- **`docs/Features.md`** links `../README.md` for the "full write-up"; relink to `docs/index.md`. Its "Where"
  column and the Hardening Plan reference README section names in prose; sweep both.
- **Code XML docs and other READMEs** that say "see the README's ..." (search `README` in `src/` and `tests/`
  including `.md` and `.cs`).
- **External links** to old anchors on GitHub (issues, PRs, the previous commit messages) will land at the
  top of the slim README. Accept that, or add a small "moved" table in the root README for the ten most-shared
  sections for one release.

## Verification (acceptance criteria)

- A link checker passes over every `.md` file: all relative links resolve and all `#anchor` fragments exist in
  the target file. Add it as a CI step (a small script or `lychee` in offline mode) so the set cannot rot.
- A content-preservation check: concatenating the new files' section bodies in the original order gives the
  original text, modulo the added headers, rewritten links and the slim root. The script emits a diff for review;
  it should show only link and header changes.
- `dotnet build`, `dotnet test` (including the three README-reading tests), `dotnet format whitespace
  --verify-no-changes` and `dotnet csharpier check .` pass; CI is green.
- The root README is under 200 lines and every new file is under about 350.
- The 8 diagrams render; the footnotes resolve.

## Order of work (each step is one reviewable commit)

1. Add the link checker and run it against the current tree (baseline; fix any existing broken links first).
2. Extract the leaf topics with no inbound cross-links from other sections: `operations/*`, `security/impersonation.md`,
   `security/audit.md` and their three README-reading tests. Smallest, safest, and the tests prove the pattern.
3. Extract `api/*` and `security/authorization.md`.
4. Extract `pattern/*`, `guides/*`, `architecture/*`, `reference/*`.
5. Slim the root README, write `docs/index.md`, update `CLAUDE.md`, `Features.md`, the `.slnx` and stray references.
6. Content-preservation diff and the final link-check run; delete the mapping script.

## Risks

- **Dangling anchors** are the main risk (148 of them, several to headings with backticks or generics); the
  script must fail on any unmapped anchor rather than guess.
- **Duplicate headings across files** ("Options", "Where it sits in the pipeline", "How it works" each occur
  several times) are harmless once split, but the current README disambiguates them with `-1`, `-2` anchor
  suffixes that the old links rely on; the anchor table must account for that.
- **Reviewers cannot read a 2,900-line move.** Mitigate with the mechanical script, one commit per step,
  and `git diff --color-moved` guidance in the PR description.
- **Drift during the split** if someone else edits the README; hence the freeze.

## Open questions for the owner

1. Folder names: is `docs/{pattern,guides,architecture,api,security,operations,reference}` acceptable, or would you rather
   keep it flat (`docs/*.md`)? A flat layout is simpler to link but harder to browse.
2. Should the per-feature operational pages (CORS, rate limiting, timeouts, health) be one `operations.md`
   instead of four to six files? They are each short and closely related.
3. Is a "moved" table in the root README wanted for external inbound links, or is breaking old anchors fine?
