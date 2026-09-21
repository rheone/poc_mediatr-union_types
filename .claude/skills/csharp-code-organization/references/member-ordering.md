# Member Ordering

## Config precedence

| Rule ID | Governs | If Discovery marked it **Explicit** | If **Unset** |
| ------- | ------- | ------------------------------------ | ------------ |
| SA1201  | Order of member *kinds* (fields before constructors before properties, etc.) | Follow whatever order the config enforces, even if it differs from the canonical order below | Apply the canonical order below |
| SA1202  | Order by accessibility (public before protected before internal before private) | Follow the config's position exactly - if suppressed, do not reintroduce accessibility grouping as a hard rule | Apply accessibility grouping below, advisory only |
| SA1203  | Constants before other fields | Follow the config | Constants before fields |
| SA1204  | Static members before instance members | Follow the config's position exactly - if suppressed, do not reintroduce static-before-instance as a hard rule | Apply static-before-instsance below, advisory only |
| SA1123/SA1124 | Whether `#region` is allowed at all | If regions are disallowed, skip the region-grouping rules in [file-and-type-structure.md](file-and-type-structure.md) entirely | Regions are permitted; use them per that reference |
| SA1214/SA1216 | Finer-grained static/instance and readonly ordering | Follow the config | No additional constraint beyond the rules below |

**"Explicit" includes explicit suppression.** A repo that has turned SA1202 or SA1204 *off* has taken a position - don't restate the rule as if the repo were silent. That suppression is local to that repo: a different repo may leave the same rule at its default or turn it on. Re-run Discovery per repo; never carry a prior repo's config forward. The rule this describes still exists as advisory guidance below - it moves from "gate the build" to "apply when a file is already being touched, don't go looking for violations elsewhere."

## Canonical order

Top to bottom, within a type (or within a partial file's own contribution to a type - see [file-and-type-structure.md](file-and-type-structure.md) for how the primary vs. implementation files divide this up):

1. Constants
2. Fields
3. Constructors
4. Properties
5. Indexers
6. Events
7. Methods

Within each of those groups, when SA1202/SA1204 are **Unset** for the target repo:

- Group by accessibility: `public` → `protected` → `internal` → `private`.
- Within an accessibility group, `static` before instance.

## Colocation exception

A `private` or `internal` member - static or instance - that has exactly one caller lives immediately next to that caller, not in its accessibility/kind group. A helper nobody else calls belongs beside the one place that calls it, not marooned in a "Methods" block three screens away. This exception overrides the grouping rule above; it does not override member-*kind* order (SA1201) - a single-use private method still comes after the constructors and properties that precede it in the canonical order, it just sits next to its caller within the methods section rather than at the bottom of a strict accessibility sort.

If a second caller appears later, move the helper back into its normal grouped position - colocation is for genuinely single-use members, not a permanent home.

## Overloads-together exception

Overloaded methods, constructors, operators, and indexers stay adjacent, ordered logically (fewest parameters to most, or simplest to most specific overload) rather than split apart by strict accessibility/static grouping. Do not let one overload's accessibility differ from its siblings in position within the file.

## Property-and-helper exception

When a property and a private helper used exclusively by that property would be harder to follow if separated by strict grouping, keep them adjacent instead. This is judgment, not a rule to invoke reflexively - it exists so "group like with like" doesn't force a property fifty lines away from the one method that only exists to serve it.

## Implementation-member / override grouping

Members that exist solely to satisfy an interface or override a base member are grouped together rather than interleaved into the accessibility/kind sort above - see [file-and-type-structure.md](file-and-type-structure.md) for whether that grouping happens via a `#region` in the same file or a separate partial file. Within that group, apply the canonical order and exceptions above as if the group were its own small file.
