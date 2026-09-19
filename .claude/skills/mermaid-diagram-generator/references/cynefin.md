---
diagram: Cynefin
slug: cynefin
status: beta
mermaid_version_introduced: "v11.16.0"
mermaid_version_verified: "12.0.0"
keyword: cynefin-beta
source: https://mermaid.js.org/syntax/cynefin.html
last_verified: 2026-09-19
plugin_required: false
---

# Cynefin

> **Status:** Beta - introduced v11.16.0. Syntax may evolve; treat generated diagrams of this type as more likely to need adjustment than stable types.

## Overview
A Cynefin diagram renders the Cynefin sense-making framework: four fixed domains (Clear, Complicated, Complex, Chaotic) plus a center "Confusion" region for items that haven't been categorized yet. You drop labeled items into whichever domain describes how well understood the cause-and-effect relationship is, and can optionally draw transitions showing how a situation moved from one domain to another over time.

## Best-fit uses
- Classifying problems, decisions, or work items by how well-understood their cause-and-effect relationships are
- Teaching or facilitating a Cynefin workshop with a ready-made worksheet template
- Showing how a situation drifted between domains over time (e.g. complacency sliding "Clear" into "Chaotic")

## When NOT to use this
- You need a generic 2x2 with custom axis meanings rather than the fixed five Cynefin domains - use `quadrant.md`
- You're classifying by two independent numeric scores rather than a qualitative domain judgment - use `quadrant.md` or `radar.md`
- The relationships between items matter more than which fixed bucket they fall in - use `flowchart.md`

## Basic syntax
Start with `cynefin-beta`. Optional `title <text>` line.

- **Domains** are five fixed keywords - you cannot rename or add domains: `clear`, `complicated`, `complex`, `chaotic`, `confusion`. They render in fixed screen positions regardless of the order you declare them in.
- **Items:** quoted string lines indented under a domain keyword become labeled badges inside that domain, e.g. `"Restart service"` under `clear`.
- **Transitions:** `<domainA> --> <domainB>` connects two *different* domains with an arrow, optionally labeled: `<domainA> --> <domainB> : "<label>"`. Self-loop transitions (same domain on both sides) are ignored - transitions must connect two different domains.
- Domains can be declared with zero items (an "empty framework") purely to render the quadrant layout as a template.
- `confusion` is capped at showing 3 items visibly; additional items collapse into a `+N more` overflow badge - keep this domain's list short by design, since its purpose is surfacing unclassified items, not holding many.

## Simple example
```mermaid
cynefin-beta
  title Incident Triage

  clear
    "Restart the service"
    "Apply documented fix"

  complicated
    "Escalate to on-call expert"

  complex
    "Run a small experiment"

  chaotic
    "Stop the bleeding first"
```
Four incident-response actions are sorted into the four main domains by how well understood their cause-and-effect relationship is.

## Complex example
```mermaid
cynefin-beta
  title Product Strategy Over Time

  clear
    "Standard pricing tiers"
    "Documented onboarding flow"

  complicated
    "Competitive analysis"
    "Vendor contract negotiation"

  complex
    "New market experimentation"
    "Emerging user segment research"

  chaotic
    "PR crisis response"

  confusion
    "Undefined churn driver"

  complex --> complicated : "Pattern identified"
  complicated --> clear : "Best practice codified"
  clear --> chaotic : "Complacency"
  chaotic --> complex : "Stabilized"
  confusion --> complex : "Hypothesis formed"
```
Five domains are populated with strategy items, and five labeled transitions trace how situations are expected to move between domains as understanding improves or crises hit - including moving an item out of `confusion` once it's been given a working hypothesis.

## Escaping & special characters
- Item lines must be double-quoted strings - unquoted plain text under a domain is not valid syntax.
- Transition labels after `:` are also double-quoted strings, following the same quoting rules as items.
- A literal double quote inside an item or transition label needs escaping or the label needs rephrasing to avoid it.
- Inside a ```mermaid fence, no extra escaping is needed beyond avoiding literal triple backticks in a label.

## Common pitfalls
- [ ] Are you using only the five fixed domain keywords (`clear`, `complicated`, `complex`, `chaotic`, `confusion`), not invented domain names?
- [ ] Are item lines quoted strings, not bare text?
- [ ] Are transitions connecting two *different* domains - self-loops are silently ignored?
- [ ] Is the `confusion` domain kept short, given only 3 items display before collapsing into a `+N more` badge?
- [ ] Did you rely on declaration order to control domain layout - domains render in fixed positions regardless of the order you write them?
- [ ] If targeting handdrawn/sketch theme, note it's explicitly unsupported for this diagram type.

## v11 fallback

This type was introduced in v11.16.0. Renderers older than that fail with `No diagram type detected` (see `general/renderers.md` for which markdown renderers those are). For such targets use a `flowchart.md` with one subgraph per domain.

- No syntax differences. Every example in this file parses and renders on both Mermaid 12.0.0 and 11.16.1.

## Beta/experimental caveats
Cynefin diagrams are beta as of v11.16.0; domain layout, the `confusion` overflow behavior, and config option names (`showDomainDescriptions`, `boundaryAmplitude`, `seed`, etc.) may still change in minor releases. When delivering this diagram type, note it requires Mermaid v11.16.0 or later and that handdrawn mode is explicitly not supported.

## Further reading
- https://mermaid.js.org/syntax/cynefin.html
