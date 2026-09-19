# Converting existing diagrams

Batch conversion of diagrams already written, between Mermaid 11 and Mermaid 12. Follow this only when the user asks to convert, upgrade or downgrade existing diagrams; it is never part of generating a new one. Do not sweep a repository on your own initiative.

## Steps

1. **Fix the scope and direction.** Scope is exactly the paths the user named: a file, a directory of `.md`, `.mmd` or `.mermaid` files, or a glob. Direction is *upgrade* (target Mermaid 12) or *downgrade* (target a named Mermaid 11 version or renderer; look the version up in `renderers.md`). If the direction or target is missing, ask.
2. **Collect the blocks.** Every fenced ```mermaid block in the named markdown files, and the whole body of standalone `.mmd` / `.mermaid` files. Record file and line for each. Nothing outside a fence is touched.
3. **Classify each block.** Identify its type from the first meaningful line (after any frontmatter and `%%` comments) and read that type's "v11 fallback" section, plus `v11-compatibility.md` for the cross-cutting rules.
4. **Convert, block by block.**
   - *Upgrade:* delete removed config keys (`flowchart.defaultRenderer`, `class.defaultRenderer`, `state.defaultRenderer`); leave `flowchart-elk` as is (still valid). Do not add new v12 features unless asked. If the user wants the old appearance kept, add the three-key restore block from `v11-compatibility.md` to the diagram's frontmatter, merging into an existing `config:` rather than duplicating it.
   - *Downgrade:* apply each type's fallback: replace v12-only types with the flowchart form (`usecase.md`, `agentflow.md`), replace shapes and features newer than the target, and drop options the target rejects. A feature that older renderers silently ignore (collapsible subgraphs, XY legends) may stay; say so in the report.
   - Preserve everything else byte for byte, including indentation (structural in Mindmap, Timeline, Kanban, TreeView, Ishikawa), quoting and line endings.
5. **Verify each converted block against the target version.** Run the validator on the edited files with the target release: `node tools/validate-mermaid.mjs --files <paths> --mode parse --mermaid-version <version>` (a block that fails is fixed or reverted, never delivered). Use `--mode render` too when layout or shapes changed. Outside this skill's repository the validator is unavailable; use the one-off parse recipe in `tools/README.md`.
6. **Leave lossy conversions alone.** When a downgrade would lose meaning (a use case diagram whose relationships have no flowchart equivalent, agentflow metadata such as `model` or `connectorRef`), do not convert that block; flag it.
7. **Report per block.** For every block: `converted`, `unchanged`, or `not converted` with the reason, listed by file and line. Give counts at the end. Edit in place only after the user has agreed to the scope; when asked for a preview, show the report without writing.

## Completion criterion

Every collected block has a status in the report, and every block marked `converted` passed a parse against the target version.
