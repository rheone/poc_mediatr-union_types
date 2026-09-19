# Mermaid 11 compatibility

This skill writes for Mermaid 12. Consult this reference when the target renderer is on Mermaid 11 (`renderers.md` says which are), or when a diagram must work on both. Type-specific differences live in each `references/<type>.md` under "v11 fallback"; this file holds what cuts across types.

## What differs between 11 and 12

| Area | Mermaid 12 | Mermaid 11 |
| --- | --- | --- |
| Default look and theme | `neo` look and `redux-color` theme for flowchart, swimlane, class, ER, requirement, sequence, state, use case, Venn, agentflow; `default` and `classic` for every other type | `classic` and `default` for every type |
| Default layout | ELK, bundled, for flowchart, state, class, ER, requirement, use case, agentflow | dagre |
| Diagram types | adds `usecase-beta`, `agentflow-beta` | neither exists; `railroad-*-beta` exists from 11.16.0 |
| Config | `flowchart.defaultRenderer`, `class.defaultRenderer`, `state.defaultRenderer` removed and ignored; `elk.preset`; per-diagram-type `theme`/`look`/`layout` scoping | the `defaultRenderer` keys exist; per-type scoping is accepted |
| Config keys accepted by both | `theme`, `look`, `layout` (including `neo`, `redux-color`, `elk`) | the same keys parse and render on 11.16.1 |

Source syntax for every type that exists in both versions is the same unless its reference file says otherwise; the source-level exceptions are listed under "Minimum versions" below.

## One source, both appearances

The default look is a rendering choice, not a syntax one. To get the Mermaid 11 appearance under Mermaid 12, put this in the diagram's frontmatter; Mermaid 11 accepts it too, so the block is safe on either:

```yaml
---
config:
  theme: default
  look: classic
  layout: dagre
---
```

To ask a Mermaid 11 renderer for the v12 appearance, `theme: redux-color` with `look: neo` is accepted on 11.16.1; no claim is made that the result is identical to v12's own drawing. `theming.md` and `layout.md` cover the settings.

## Minimum versions

Features newer than the target renderer's version are replaced with the fallback in the type's reference file.

| Minimum | Feature | Behaviour on older versions | Reference |
| --- | --- | --- | --- |
| 12.0.0 | `usecase-beta`, `agentflow-beta` | `No diagram type detected` | `usecase.md`, `agentflow.md` |
| 11.17.0 | flowchart shapes `folder`/`directory`, `bucket`, `console`, `browser`, `person` | hard fail `No such shape` (tested on 11.16.1) | `flowchart.md` |
| 11.17.0 | flowchart collapsible subgraphs; XY chart legend | parsed and silently ignored (tested on 11.16.1) | `flowchart.md`, `xy-chart.md` |
| 11.16.0 | `railroad-*-beta`, `swimlane-beta`, `cynefin-beta` | `No diagram type detected` | `railroad.md`, `swimlanes.md`, `cynefin.md` |
| 11.16.0 | ER attribute `?` suffix; XY per-point line labels; TreeView bare labels and annotations; Architecture `align` | hard parse error | `entity-relationship.md`, `xy-chart.md`, `treeview.md`, `architecture.md` |
| 11.15.0 | `eventmodeling`; Sankey `labelStyle`/`nodeWidth`/`nodePadding`/`nodeColors` | type missing; options ignored or rejected per reference | `event-modeling.md`, `sankey.md` |
| 11.14.0 | `treeView-beta`, `wardley-beta` | `No diagram type detected` | `treeview.md`, `wardley.md` |

This table is an index into the type files, which hold the detail and are the source of truth for each row.

## Converting between versions

For a request to convert existing diagrams (upgrade to v12 or downgrade to v11), follow `converting-diagrams.md`.
