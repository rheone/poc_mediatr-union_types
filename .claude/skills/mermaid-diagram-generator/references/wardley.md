---
diagram: Wardley
slug: wardley
status: beta
mermaid_version_introduced: "v11.14.0"
mermaid_version_verified: "12.0.0"
keyword: wardley-beta
source: https://mermaid.js.org/syntax/wardley.html
last_verified: 2026-09-19
plugin_required: false
---

# Wardley

> **Status:** Beta - introduced v11.14.0. Syntax may evolve; treat generated diagrams of this type as more likely to need adjustment than stable types.

## Overview
A Wardley map plots components of a value chain on two axes - visibility to the user/customer (vertical) and evolutionary maturity from novel to commodity (horizontal) - and connects them to show dependency. Unlike most diagrams, position is the whole point: where a component sits tells you whether it's still being invented or is now a boring utility, which drives build/buy/outsource decisions.

## Best-fit uses
- Strategic mapping of a value chain to reason about what to build, buy, or outsource
- Visualizing how components are expected to evolve (or already have) from genesis toward commodity
- Communicating dependency + maturity together, not just one or the other

## When NOT to use this
- You just need a static two-axis scatter without an evolution/maturity narrative - `quadrant.md` is simpler
- You're mapping organizational or system dependencies without a maturity dimension - use `flowchart.md`
- Precise numeric plotting matters more than relative strategic positioning - a real chart/plot library is a better fit than Mermaid

## Basic syntax
Start with `wardley-beta`. Coordinates are given as `[visibility, evolution]` - note this is `[y, x]` order (visibility first), the reverse of typical `(x, y)` notation, and both values run 0.0–1.0.

- **Title/size:** `title <text>`, `size [<width>, <height>]`.
- **Component:** `component <Name> [<visibility>, <evolution>]` - name it with quotes (`component "Custom Service" [...]`) if it starts with a non-letter or contains characters the grammar doesn't otherwise accept; hyphenated bare names (`real-time processing`) don't need quoting.
- **Label offset:** append `label [<offsetX>, <offsetY>]` after the coordinates to nudge the text label away from its default position.
- **Anchor** (user/customer, rendered bold): `anchor <Name> [<visibility>, <evolution>]`.
- **Links:** `A -> B` for a plain dependency; `A +> B` for a flow; `A +<> B` for a bidirectional flow; `A +'label'> B` for a labeled flow.
- **Decorators** - appended after a component's coordinates: `(inertia)`, `(build)`, `(buy)`, `(outsource)`, `(market)`.
- **Evolve** (draws a movement indicator toward a target evolution stage): `evolve <ComponentName> <targetEvolution>`.
- **Pipeline** (a component broken into evolving sub-parts along the x-axis only): `pipeline <Parent> { component "<Name>" [<x>] ... }`.
- **Custom evolution stages:** `evolution Stage1 -> Stage2 -> Stage3 -> Stage4`, optionally with dual labels (`Genesis / Concept -> ...`) or custom stage-boundary widths (`Genesis@0.2 -> Custom@0.4 -> ...`).
- **Notes/annotations:** `note "<text>" [<x>, <y>]`; numbered annotations via `annotations [<x>, <y>]` (legend position) plus `annotation <n>,[<x>, <y>] "<text>"`.
- **Forces:** `accelerator "<text>" [<x>, <y>]`, `deaccelerator "<text>" [<x>, <y>]`.

## Simple example
```mermaid
wardley-beta
title Coffee Shop Value Chain

anchor Customer [0.90, 0.90]
component Cup of Coffee [0.75, 0.65]
component Beans [0.55, 0.40]
component Roaster [0.35, 0.20]

Customer -> Cup of Coffee
Cup of Coffee -> Beans
Beans -> Roaster
```
A customer depends on a cup of coffee, which depends on beans, which depend on a roaster - each positioned by how visible it is to the customer and how evolved/commoditized it is.

## Complex example
```mermaid
wardley-beta
title Online Retail Platform Strategy
size [1100, 800]

evolution Genesis@0.25 -> Custom@0.5 -> Product@0.75 -> Commodity@1.0

anchor Shopper [0.90, 0.95]

component "Mobile App" [0.80, 0.85] (build)
component "Web App" [0.75, 0.80] label [-60, 10] (build)
component "Checkout API" [0.65, 0.60] (buy)
component "Payment Provider" [0.55, 0.90] (market)
component "Recommendation Engine" [0.45, 0.35] (outsource) (inertia)
component "Product Catalog DB" [0.35, 0.55]

Shopper -> "Mobile App"
Shopper -> "Web App"
"Mobile App" -> "Checkout API"
"Web App" -> "Checkout API"
"Checkout API" +> "Payment Provider"
"Checkout API" -> "Product Catalog DB"
"Recommendation Engine" +'personalization'> "Product Catalog DB"

evolve "Checkout API" 0.80
evolve "Recommendation Engine" 0.55

accelerator "Cloud-native rollout" [0.20, 0.85]
deaccelerator "Legacy catalog schema" [0.45, 0.45]

note "Payment provider is a commodity utility" [0.50, 0.92]
```
Custom evolution stage boundaries are defined up front; components carry build/buy/outsource/market decorators plus an inertia marker; flow links (`+>`, `+'label'>`) distinguish data/value flow from plain dependency; `evolve` shows two components trending rightward; accelerator/deaccelerator and a note round out the strategic narrative.

## Escaping & special characters
- Component and anchor names with spaces must be quoted (`"Custom Service"`) when referenced later in links, decorators, or `evolve` - the quoted form must match exactly everywhere it's used.
- Hyphenated bare names (`real-time processing`) don't need quotes, but names starting with a digit or symbol do.
- Labeled flow links embed the label in single quotes inside the arrow token itself (`+'label'>`) - that's part of the arrow syntax, not a separate string argument.
- Note/annotation/accelerator text uses double quotes; escape a literal double quote inside the text or restructure the label to avoid it.
- Inside a ```mermaid fence, no extra escaping is needed beyond avoiding literal triple backticks in text.
- **Tested (Mermaid 12.0.0 and 11.16.1):** Component and anchor names accept only letters, digits and spaces unless quoted (`component "a: b" [0.5, 0.5]`; quote the name wherever it is referenced). In the title, a `<`...`>` pair is read as an HTML tag and silently dropped, so write both as `#60;` and `#62;`.

## Common pitfalls
- [ ] Are coordinates in `[visibility, evolution]` order, not `(x, y)` - visibility is Y, evolution is X?
- [ ] Are pipeline components given a single evolution value (`[0.5]`), not a `[visibility, evolution]` pair - pipelines only vary along the x-axis?
- [ ] Are quoted component names used consistently everywhere that name is referenced (links, `evolve`, decorators)?
- [ ] Are you distinguishing plain dependency (`->`) from flow (`+>`, `+<>`, `+'label'>`) intentionally, not interchangeably?
- [ ] If using custom evolution stages, do all component evolution values fall within 0.0–1.0 regardless of custom stage boundary widths?
- [ ] Are decorators placed after the coordinates on the same `component` line, not on a separate line?

## v11 fallback

This type was introduced in v11.14.0. Renderers older than that fail with `No diagram type detected` (see `general/renderers.md` for which markdown renderers those are). For such targets use a `quadrant.md` chart (value chain on one axis, evolution on the other).

- No syntax differences. Every example in this file parses and renders on both Mermaid 12.0.0 and 11.16.1.

## Beta/experimental caveats
Wardley diagrams are beta as of v11.14.0; component/link/decorator grammar and config option names may still change in minor releases. When delivering this diagram type, note it requires Mermaid v11.14.0 or later and that flow-link tokens (`+>`, `+<>`, `+'label'>`) in particular are newer, less-common syntax worth spot-checking against the target renderer.

## Further reading
- https://mermaid.js.org/syntax/wardley.html
