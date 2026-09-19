---
diagram: Swimlanes
slug: swimlanes
status: beta
mermaid_version_introduced: "v11.16.0"
mermaid_version_verified: "12.0.0"
keyword: swimlane-beta
source: https://mermaid.js.org/syntax/swimlanes.html
last_verified: 2026-09-19
plugin_required: false
---

# Swimlanes

> **Status:** Beta - introduced v11.16.0. Syntax may evolve; treat generated diagrams of this type as more likely to need adjustment than stable types.

## Overview
A swimlane diagram is a process flow where each top-level lane is explicitly owned by an actor, team, system, or phase, and nodes/edges show work moving within and across those lanes. It reuses flowchart node-shape and edge syntax internally, so if you already know flowchart notation there's almost nothing new to learn - the only new concept is that top-level `subgraph` blocks are the lanes themselves, rendered side-by-side (or stacked) rather than as nested boxes.

## Best-fit uses
- Showing a process where "who owns this step" matters as much as "what happens next"
- Approval flows, support/escalation processes, and delivery workflows that cross team or system boundaries
- Highlighting cross-lane handoffs (e.g. labeling the arrow where a document or decision passes from one owner to another)

## When NOT to use this
- Ownership/lane grouping isn't the point and you only need sequence or branching - a plain `flowchart.md` (optionally with subgraphs for light grouping) is simpler and more mature
- The focus is on timed messages passing between a fixed set of participants - use `sequence.md`
- The focus is how a single entity changes state over time rather than who does the work - use `state.md`

## Basic syntax
A diagram starts with the `swimlane-beta` keyword, optionally followed by a direction:
```
swimlane-beta LR
```
Supported directions: `TB` (default if omitted), `TD` (alias for `TB`), `BT`, `LR`, `RL`.

**Lanes** - declared with `subgraph`, closed with `end`. Every top-level `subgraph` becomes a rendered lane:
```
subgraph Sales
  lead[Qualify lead]
end
```
A lane can have a separate internal id and display label - useful when the label needs spaces or you want a stable id for styling/linking:
```
subgraph sales [Sales team]
  lead[Qualify lead]
end
```

**Nodes** - flowchart-style shape syntax, id first, label inside the shape delimiters:

| Syntax | Shape | Common use |
|---|---|---|
| `id[Text]` | Rectangle | Task or activity |
| `id(Text)` | Rounded rectangle | Step or event |
| `id([Text])` | Stadium | Start or end |
| `id{Text}` | Decision | Branching question |
| `id((Text))` | Circle | Connector or marker |

For the full shape catalog (icons, images, markdown strings, classes) see `flowchart.md`.

**Edges** - also flowchart-style, and can connect nodes within one lane or across lanes:

| Syntax | Meaning |
|---|---|
| `A --> B` | Arrow |
| `A --- B` | Line, no arrowhead |
| `A -->\|Label\| B` | Arrow with label |
| `A -.-> B` | Dotted arrow |
| `A ==> B` | Thick arrow |

**Accessibility** - `accTitle:` and `accDescr:` lines set an accessible title/description, same as other Mermaid diagram types.

`classDef`/`class` styling (flowchart-style) is also supported for highlighting individual nodes.

## Simple example
```mermaid
swimlane-beta LR
  subgraph Customer
    request[Request service]
    receive[Receive update]
  end

  subgraph Support
    triage[Triage request]
    answer[Send answer]
  end

  request --> triage
  triage -->|Known issue| answer
  answer --> receive
```
Two lanes, `Customer` and `Support`, with a labeled cross-lane arrow marking the handoff point where triage resolves the request.

## Complex example
```mermaid
swimlane-beta LR
  accTitle: Loan application flow
  accDescr: An applicant submits a request, a reviewer screens and decides, and the system creates the account on approval.

  subgraph Applicant
    apply[Submit application]
    sign[Sign agreement]
  end

  subgraph reviewer [Review team]
    screen[Screen application]
    decide{Approved?}
  end

  subgraph System
    create[Create account]
    notify[Send welcome email]
  end

  apply -->|Application received| screen
  screen --> decide
  decide -->|Approved| create --> notify --> sign
  decide -->|Needs changes| apply

  classDef attention fill:#fff2cc,stroke:#d6a500,color:#111;
  class decide attention;
```
Three lanes - one lane (`reviewer`) uses a separate internal id and display label, a decision node routes to two different outcomes with labeled edges, one outcome loops back to an earlier lane, and `classDef`/`class` highlight the decision node.

## Escaping & special characters
- Node and edge label quoting follows flowchart rules: wrap a label in double quotes if it contains characters like `[`, `]`, `(`, `)`, or `#` that would otherwise be parsed as shape/annotation syntax, e.g. `id["Cost: $500"]`.
- A lane's display label (the bracketed part of `subgraph id [Label]`) follows the same quoting needs as a flowchart node label when it contains reserved characters.
- Inside a ```` ```mermaid ```` fence in Markdown, keep the `subgraph ... end` block's indentation consistent - it's cosmetic (unlike TreeView/mindmap, structure comes from `subgraph`/`end` keywords, not indentation) but inconsistent indentation makes the source harder to review.
- **Tested (Mermaid 12.0.0 and 11.16.1):** Quote node labels (`a["text"]`); unquoted labels break on `" | ( ) [ ] { }`, and the only character that still needs an escape inside the quotes is `"`, written `#34;`.

## Common pitfalls
- [ ] Did you use `swimlane-beta` (singular "swimlane"), not "swimlanes-beta"?
- [ ] Is every lane a *top-level* `subgraph` - a `subgraph` nested inside another `subgraph` won't render as its own lane?
- [ ] Does every `subgraph` have a matching `end`?
- [ ] If a lane needs a label with spaces, did you give it an internal id (`subgraph id [Label with spaces]`) rather than putting spaces in the id itself?
- [ ] Are cross-lane handoff arrows labeled where the handoff depends on a decision, document, or condition - unlabeled arrows read as "just sequence"?
- [ ] Is a direction (`LR`/`TB`/etc.) chosen deliberately rather than left to default `TB`, since lane orientation strongly affects readability for wide processes?

## v11 fallback

This type was introduced in v11.16.0. Renderers older than that fail with `No diagram type detected` (see `general/renderers.md` for which markdown renderers those are). For such targets use a `flowchart.md` with one subgraph per lane.

- **Default appearance:** v12 draws this type with the `neo` look and the `redux-color` theme by default; v11 draws it with the `classic` look and the `default` theme. The diagram source is identical - only the rendering differs. `general/v11-compatibility.md` shows how to make v12 draw the v11 appearance.

## Beta/experimental caveats
Swimlanes is new as of v11.16.0; the source documentation itself carries an explicit warning that its syntax may evolve in future versions. Confirm the target Mermaid runtime is v11.16.0 or later before delivering a swimlane diagram - on older pinned versions this diagram type does not exist and will fail to parse. The doc's rendered examples use the "Neo" look and "Redux" theme, but that is cosmetic-only: swimlanes render fine under whatever look/theme is otherwise configured.

## Further reading
- https://mermaid.js.org/syntax/swimlanes.html
