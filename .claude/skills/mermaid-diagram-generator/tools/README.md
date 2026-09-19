# Validation Guide for mermaid-diagram-generator

`validate-mermaid.mjs` is a replayable validator for every Mermaid example in the skill and its README (111 blocks), pinned to **Mermaid v12.0.0**. It runs structural checks, keyword drift detection and version-pin verification, plus optional grammar and rendering validation against any Mermaid release. It can also check the diagram blocks in your own files.

## Quick start

Prerequisites: **Node.js 22.12 or later** (Mermaid 12 declares `engines.node >=22.12.0`; the script refuses to load Mermaid on an older Node, and `--mode none` works on any Node).

```bash
# Full validation: structure + keyword + version + render (Chromium)
node tools/validate-mermaid.mjs

# Grammar only (no browser)
node tools/validate-mermaid.mjs --mode parse

# Structural checks only (installs nothing)
node tools/validate-mermaid.mjs --mode none

# One diagram type
node tools/validate-mermaid.mjs --only architecture,treeview

# The same blocks against another Mermaid release
node tools/validate-mermaid.mjs --mode parse --mermaid-version 11.16.1

# Your own diagrams, against a target release
node tools/validate-mermaid.mjs --files docs/architecture.md,docs/flow.mmd --mode parse --mermaid-version 11.16.1

# Machine-readable output
node tools/validate-mermaid.mjs --json | jq .

# Remove the dependency cache for the selected release
node tools/validate-mermaid.mjs --clean
```

### Modes

| Mode                 | Checks                                    | Installs           | Time    | Best for                                       |
| -------------------- | ----------------------------------------- | ------------------ | ------- | ---------------------------------------------- |
| **render** (default) | Grammar + layout + icon resolution        | mermaid, puppeteer | ~15-25s | Full validation; catches the most bugs         |
| **parse**            | Grammar only                              | mermaid, jsdom     | ~2-5s   | CI and quick checks; no browser system libs    |
| **none**             | Structure, keywords, version pins         | nothing            | <1s     | Offline checks; pre-merge validation           |

### Reading the output

```text
summary mode=parse blocks=111 passed=111 skipped=0 version-gated=0 problems=0
OK - everything checks out
```

- **blocks** - diagram blocks discovered.
- **passed** - blocks that validated.
- **skipped** - blocks annotated `skip`. Currently none.
- **version-gated** - blocks whose `since=` / `until=` annotation excludes the release under test (see "Per-block annotations"). Against 11.16.1 the six v12-only use case and agentflow blocks are gated.
- **problems** - failures to fix.

A failure prints the file and line of the block and the parser's message:

```text
parse - 1 problem(s)
  FAIL references/architecture.md:60
        parse failed: Error text here
```

## Common scenarios

### Checking a Mermaid release other than the pin

`--mermaid-version X.Y.Z` installs that release into its own cache and checks every block against it. Run the two releases that matter for this skill:

```bash
node tools/validate-mermaid.mjs --mode parse                            # 12.0.0, the pin
node tools/validate-mermaid.mjs --mode parse --mermaid-version 11.16.1  # the v11 fallback
```

A block that must only run on some releases carries a `since` / `until` annotation instead of failing on the others.

### Bumping the pinned version

1. Set `metadata.mermaid_version` in `SKILL.md`.
2. Run `node tools/validate-mermaid.mjs --clean`, then `--mode parse`, then the default render run. Fix or annotate failures.
3. Set `mermaid_version_verified` and `last_verified` in each reference file you actually re-checked; do not bulk-update files you did not inspect. The validator requires every reference's `mermaid_version_verified` to equal the pin.
4. Check the fallback: `--mode parse --mermaid-version 11.16.1` (or the oldest release the skill still supports).

### Adding a diagram type

Add `references/<slug>.md` with the required frontmatter and sections (the validator lists what is missing), then update `EXPECTED_DIAGRAM_FILES` in the script and the tables in `SKILL.md`. A file with more than one starting keyword (Railroad, C4) annotates its blocks `keyword-exempt`.

### "One diagram type keeps failing. Do I delete it?"

First check whether the example itself is wrong. Test candidate syntax directly against the target parser (see "Manual validation") and only annotate a block `skip` once no known-correct syntax parses. The reference files record the syntax that was confirmed by parsing, not only what the upstream prose says.

```markdown
<!-- mermaid-validate: skip reason="<diagram> has parser issues in vX.Y.Z" -->
```

## Manual validation

### mermaid.live

Paste the diagram into https://mermaid.live. It may run a different Mermaid version than the one you are checking.

### One-off parse check (no browser)

```bash
mkdir mermaid-check && cd mermaid-check && npm init -y
npm install mermaid@12.0.0 jsdom
```

```javascript
// check.mjs
import { JSDOM } from "jsdom";
const dom = new JSDOM("<!doctype html><body></body>", { pretendToBeVisual: true, url: "http://localhost/" });
for (const k of ["window", "document", "navigator", "HTMLElement", "SVGElement", "Element", "Node", "DOMParser"]) {
   globalThis[k] ??= dom.window[k];
}
const mermaid = (await import("mermaid")).default;
mermaid.initialize({ startOnLoad: false, securityLevel: "loose", suppressErrorRendering: true });
try {
   await mermaid.parse("flowchart TD\nA --> B");
   console.log("parse ok");
} catch (e) {
   console.log("parse failed:", e.message);
}
```

### Render locally

Install `puppeteer`, load `node_modules/mermaid/dist/mermaid.min.js` into a page, and call `mermaid.render(id, code)`; an error graphic or a thrown error means failure.

## Per-block annotations

An annotation is an HTML comment on the line above a fence:

```markdown
<!-- mermaid-validate: skip reason="requires a plugin the validator does not register" -->
<!-- mermaid-validate: parse-only reason="plugin is registered for --mode parse only" -->
<!-- mermaid-validate: keyword-exempt reason="railroad family: several starting keywords" -->
<!-- mermaid-validate: since="12.0.0" -->
<!-- mermaid-validate: until="12.0.0" -->
```

- **skip** - do not validate the block (a plugin-dependent type the validator has not wired up, or a parser issue confirmed by testing).
- **parse-only** - validate grammar, skip the render phase. ZenUML's blocks use it: the plugin is registered against jsdom for `--mode parse` but not against the browser bundle.
- **keyword-exempt** - skip the starting-keyword drift check, for files with several keywords or a fallback block in a different diagram family.
- **since="X.Y.Z"** - run only on releases at or above X.Y.Z. Marks syntax that older releases reject.
- **until="X.Y.Z"** - run only on releases below X.Y.Z.

`since` and `until` combine with a directive on the same comment.

## Troubleshooting

### "npm install failed in ..."

The validator installs mermaid, jsdom and puppeteer into a cache under the OS temp directory. Check that npm 7+ is installed, the network is reachable, and the temp directory is writable, then install by hand:

```bash
cd "$TMPDIR/mermaid-validate-12.0.0"          # Windows: %TEMP%\mermaid-validate-12.0.0
npm install mermaid@12.0.0 jsdom
```

### "puppeteer failed to launch"

Puppeteer needs Chromium (Windows and macOS usually just work) or system libraries on Linux. In a container without them, use `--mode parse`.

### "parse and render modes need Node >= 22.12.0"

Upgrade Node, or use `--mode none` for the structural checks.

## Cache

Dependencies live in `$TMPDIR/mermaid-validate-<version>/`, outside the repository, one directory per Mermaid release. The cache saves its packages to its own `package.json`, so installing one mode's packages never prunes another's. `--clean` removes the directory for the release selected by `--mermaid-version` (default: the pin); it never touches the repository.

## Integration

### Pre-commit hook

```bash
#!/bin/bash
cd "$(git rev-parse --show-toplevel)/.claude/skills/mermaid-diagram-generator"
node tools/validate-mermaid.mjs --mode parse || exit 1
```

### CI

```yaml
- name: Validate Mermaid examples
  run: |
     cd .claude/skills/mermaid-diagram-generator
     node tools/validate-mermaid.mjs --mode parse
     node tools/validate-mermaid.mjs --mode parse --mermaid-version 11.16.1
```

Run a full render pass at least once after a version bump; parse mode misses layout and icon-resolution failures.
