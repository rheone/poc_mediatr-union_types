---
diagram: Event Modeling
slug: event-modeling
status: experimental
mermaid_version_introduced: "v11.15.0"
mermaid_version_verified: "12.0.0"
keyword: eventmodeling
source: https://mermaid.js.org/syntax/eventmodeling.html
last_verified: 2026-09-19
plugin_required: false
---

# Event Modeling

> **Status:** Experimental - introduced v11.15.0. Syntax and support may change; treat generated diagrams of this type as more likely to need adjustment than stable types.

## Overview
Event Modeling is a diagram type for narrating how information flows through a system over time - as a timeline of UI actions, commands, the events they produce, and the read models built from those events - rather than depicting static structure. Mermaid's implementation lays each timeline step out left-to-right in numbered "time frames," auto-inferring the relationship arrows between consecutive steps unless told otherwise. This is Mermaid's newest diagram type, added in v11.15.0.

## Best-fit uses
- Narrating a use case as a timeline of user action, command, resulting event(s), and the read model that surfaces the result
- Designing or documenting an event-sourced or CQRS-style workflow before implementation
- Showing how a UI screen, a backend command handler, and a projection/read model relate for one specific flow
- Facilitating an Event Modeling / Event Storming-style workshop output in a text-based, versionable format

## When NOT to use this
- General request/response or service-to-service call ordering without an event-sourcing angle - see `sequence.md` instead, which is purpose-built for that and far more stable.
- A static view of what data/entities exist rather than how they change over time - see `entity-relationship.md` or `class.md` instead.
- Any diagram where syntax stability matters more than expressiveness - this is Mermaid's newest and least battle-tested diagram type.

## Basic syntax
- Start keyword: `eventmodeling`.
- Each step is a time frame: `tf <number> <entity-type> <identifier>` (compact) or `timeframe <number> <entity-type> <identifier>` (relaxed) - both notations are interchangeable within the same diagram. **The `tf`/`timeframe` keyword is mandatory** - a bare `01 ui CartUI` line without it fails to parse (confirmed on Mermaid 11.16.1 and 12.0.0).
- Entity types, compact / relaxed pairs: `ui` (no relaxed alias) for a user-interface trigger, `pcr`/`processor` for an automated/background trigger, `cmd`/`command` for a command, `evt`/`event` for an event, `rmo`/`readmodel` for a read model.
- Time frame numbers must be unique per diagram and establish the left-to-right ordering; Mermaid infers a relationship arrow from each time frame to the next unless a reset frame intervenes.
- Reset frame: `rf <number> <entity-type> <identifier>` / `resetframe ...` breaks the automatic inference chain, letting a new, unrelated timeline segment start without an arrow from the previous step. **Unlike the mermaid.js.org prose, a bare `rf <number>` with no entity type fails to parse on 11.16.1 and 12.0.0** - the reset frame still declares an entity, identical in shape to `tf`, it just also suppresses the inferred arrow from the prior step.
- Inline data: append `{ ... }` on the same line as a time frame to attach a short payload description, e.g. `tf 02 cmd AddItem {item id}`. When combined with an explicit relation (see below), the `->>` relation tokens must come *before* the `{ ... }` data, e.g. `tf 03 rmo C ->> 01 {payload}` - the reverse order fails to parse.
- Data blocks: reference a block from a time frame with wiki-link syntax `[[identifier]]` and define its contents in a separate `data <identifier> { ... }` block after the frames (parses on 11.16.1 and 12.0.0). When the same entity repeats, give each block a numbered identifier (`AddItem01`, `AddItem02`).
- Namespaces: prefix an identifier with `Namespace.`, e.g. `Inventory.InventoryChanged` - each distinct Namespace + entity-type pair gets its own swimlane, letting you group related timelines visually.
- Multiple relations: when a step depends on more than one preceding step (e.g. a read model built from several events), chain the extra relations with `->> <frame-number>` tokens (referencing the *time frame number*, not the entity identifier) instead of relying on the default single-predecessor inference, e.g. `tf 03 rmo C ->> 01 ->> 02`.
- Typed data: prefix the braces with a backtick type hint, inline or in a data block - ``tf 01 rmo UserAdded `json`{ "name": "foo" }``. The types are `json`, `jsobj`, `figma`, `salt`, `uri`, `md`, `html` and `text`; the renderer gives them no special treatment.

## Simple example

```mermaid
eventmodeling
tf 01 ui CartUI {select item}
tf 02 cmd AddItem {item id, quantity}
tf 03 evt ItemAdded {item id, quantity, cart id}
```
A user interaction (`ui`) triggers a command (`cmd`), which produces an event (`evt`); Mermaid infers the two connecting arrows automatically because the time frame numbers run consecutively with no reset frame between them.

## Complex example

```mermaid
eventmodeling
tf 01 ui CartUI {customer selects item}
tf 02 cmd AddItem {item id, quantity}
tf 03 evt ItemAdded {item id, quantity, cart id}
tf 04 rmo CartSummary {running total, item count}

rf 05 ui CheckoutUI {customer confirms order}
tf 06 cmd PlaceOrder {cart id}
tf 07 evt OrderPlaced {order id, cart id, total}

tf 08 pcr Inventory.ReserveStock {triggered by OrderPlaced}
tf 09 evt Inventory.StockReserved {order id, reserved items}
tf 10 rmo OrderConfirmation ->> 07 ->> 09 {order id, total, reserved items}
```
`rf 05 ui CheckoutUI` is both the reset frame *and* the entity for that frame - reset frames declare an entity the same way `tf` does, they additionally just suppress the arrow that would otherwise be inferred from step 04. The `Inventory.` namespace prefix on steps 08-09 places the background reservation processor and its event in their own swimlane, separate from the checkout flow. `OrderConfirmation` is a read model built from two prior steps, so both relations are stated explicitly with `->> 07 ->> 09` (referencing frame numbers, and placed before the inline data) rather than relying on single-predecessor inference.

## Escaping & special characters
- Inline payload descriptions in `{ ... }` are free text; keep them short and avoid embedding raw `{`/`}` characters, since the parser treats the first matching pair as the payload boundary.
- Identifiers used as namespace-qualified names (`Namespace.Entity`) use a literal period as the separator - avoid periods inside an identifier for any other purpose.
- Data-block references use double-square-bracket wiki-link syntax (`[[identifier]]`); keep the identifier alphanumeric to avoid ambiguity with Markdown's own link syntax when this diagram is embedded in a larger document.
- Inside a fenced ```mermaid block in Markdown, blank lines between time frame groups are cosmetic and safe - they do not need escaping and can be used freely to visually separate timeline segments.
- **Tested (Mermaid 12.0.0 and 11.16.1):** The identifier cannot contain `:` (a lexer error). The `{ data }` part accepts any character.

## Common pitfalls
- Relying on default inference across a `rf`/`resetframe` boundary - inference is intentionally cut there, so a missing explicit relation will leave a step disconnected.
- Reusing a time frame number - numbers must be unique per diagram since they double as the ordering and the target of `->>` relations.
- Building a read model or event from multiple predecessors without adding the extra `->>` relations - the default inference only connects consecutive single-predecessor steps.
- Mixing compact and relaxed keyword forms inconsistently in a way that hurts readability - both parse fine, but pick one convention per diagram for a team's sanity.
- Treating this like a general sequence diagram - it models information flow over named time frames and swimlanes, not arbitrary message passing, so shoehorning unrelated interaction patterns into it produces an awkward result.

## v11 fallback

This type was introduced in v11.15.0. Renderers older than that fail with `No diagram type detected` (see `general/renderers.md` for which markdown renderers those are). For such targets use a `sequence.md` diagram, one participant per swimlane.

- No syntax differences. Every example in this file parses and renders on both Mermaid 12.0.0 and 11.16.1.

## Beta/experimental caveats
This is Mermaid's newest diagram type (v11.15.0+) and its docs note that the underlying grammar is developed in a separate external DSL project intended to eventually support multiple output targets (e.g. IDE tooling), which signals the syntax is still actively evolving outside the main Mermaid release cadence. Always tell the user this diagram type requires Mermaid v11.15.0 or later, and that both the compact/relaxed keyword set and the inference/namespace rules are more likely than stable diagram types to change in a future Mermaid release.

## Further reading
- https://mermaid.js.org/syntax/eventmodeling.html
