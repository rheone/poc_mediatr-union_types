# C# Documentation Specialist Reference

This file is the authoritative standards reference for the `csharp-docs-and-comments` skill.
Sub-agents: read this file in full before editing any C# code.

---

## 1. Comment Philosophy

The single most important test for any comment:

> **Would removing it cause a future maintainer to ask a question that the code itself cannot answer?**

| Good Comment | Bad Comment |
| --- | --- |
| Explains **why** the code exists | Repeats **what** the code does |
| Explains intent and purpose | Narrates syntax |
| Explains constraints and invariants | Restates what identifiers already say |
| Preserves domain/institutional knowledge | Creates noise that trains readers to skip |
| Documents non-obvious consequences | Describes the obvious |

### The Seven Heuristics

A comment is probably worthwhile if removing it would make a future maintainer ask:

1. "Why is this done this way?"
2. "Can I simplify this without breaking something?"
3. "Is this intentional, or is it a bug?"
4. "What breaks if I change this?"
5. "What external rule, RFC, or business constraint does this satisfy?"
6. "Why not use the obvious/simpler approach?"
7. "What does this domain term mean?"

If the code already answers those questions clearly, the comment is probably unnecessary.

---

## 2. Audience Note

Documentation in this skill is written **for senior developer consumers**:

- They know C# — do not explain language mechanics
- They may not know the domain — **do** explain domain concepts, RFC references, business rules, and naming conventions that are specific to this codebase
- They are reading to understand intent, constraints, and safe usage — not to learn syntax

---

## 3. High-Value Comment Categories

| Category | Where | When | Why |
| --- | --- | --- | --- |
| Intent | Complex business logic | Purpose is non-obvious from identifiers | Explain *why* the code exists |
| Domain | Business/domain rules | Rules come from external specs, RFCs, processes | Preserve institutional knowledge |
| Constraint | Edge cases, invariants, protocol boundaries | Code must obey hidden constraints | Prevent accidental breakage |
| Algorithm | Complex algorithms, math, bit manipulation | Implementation is hard to reason about | Explain derivation or correctness |
| Architecture | Public APIs, boundaries, adapters | Design tradeoffs matter | Explain system structure |
| Temporal | Workarounds, version-specific hacks | Code depends on external bugs or version constraints | Explain removal conditions |
| Concurrency | Locking, shared state, volatile fields | Thread safety matters | Prevent race conditions |
| Performance | Optimized or hot-path code | Readability was sacrificed for speed | Justify complexity |
| Security | Auth, crypto, validation | Behavior has security implications | Prevent dangerous "cleanup" refactors |
| Interop | Serialization, native, wire protocol | Layout, order, or encoding matters | Preserve compatibility |
| Generated | Auto-generated or tool-managed files | Mark ownership boundaries | Prevent manual edits |
| Public API XML | Public/protected APIs | Consumers need IntelliSense and generated docs | Consumer usability |
| TODO/FIXME/HACK | Temporary technical debt | Tracked remediation is needed | Surface known deficiencies |

---

## 4. XML Documentation Standards

Apply all of the following. They are grouped by category.

### 4a. Structural Completeness

- All **public** types and members must have XML documentation.
- Non-public members should be documented when the purpose, behavior, or constraints are non-obvious.
- `<summary>` is required on every documented element.
- `<param>` is required for every parameter on documented methods, constructors, delegates, and indexers.
- `<returns>` is required on every documented method that returns a non-void value.
- `<typeparam>` is required for every generic type parameter on documented generic types and methods.
- `<value>` is required on every documented property and indexer.
- Every value in an `enum` must be documented.
- Partial class declarations: each partial file that declares new members must document those members. The primary partial file documents the type itself.
- `<exception>` should be present for every exception a method contractually throws under specific circumstances.

### 4b. Content Quality

- **Never** write a `<summary>` that restates the identifier name, paraphrases the signature, or narrates obvious code.
- **Never** leave default Visual Studio placeholder text (e.g., "TODO: Add summary here").
- **Never** copy and paste documentation verbatim from one member to another — if two members are similar, explain the *difference*.
- `<summary>` should describe purpose, not implementation. "What does this do for the caller?" not "How does it work internally?"
- If a method is one of several similar methods, explain what distinguishes it from the others.
- Call out unexpected behaviors: global state modifications, caching, deferred execution, non-obvious ordering requirements.
- Enumerate all side-effects.
- Document non-obvious postconditions that influence how callers deal with return values.

### 4c. `<summary>` Formatting Rules

- First word starts with a **capital letter**.
- Text ends with a **period** (full stop).
- No blank lines inside the `<summary>` block.
- No tabs; no consecutive whitespace characters.
- No markdown syntax — use XML doc tags instead (see §5).
- Constructors: begin with `Initializes a new instance of the <see cref="TypeName"/> class.` (or struct/record as appropriate). Add more after this standard phrase if useful.
- Finalizers/destructors: begin with `Finalizes an instance of the <see cref="TypeName"/> class.`
- Properties: `<summary>` should reflect the accessible accessors. For a get-only property, say "Gets the...". For get+set, say "Gets or sets the...".

### 4d. Tag Correctness

- `<param name="x">` — the `name` attribute must **exactly** match the parameter name in the signature. No empty `name`, no missing `name`, no mismatched casing.
- `<typeparam name="T">` — same rule: exact match to the generic type parameter name.
- `<returns>` must **not** appear on `void` methods or constructors.
- `<value>` must **not** appear on methods.
- `<inheritdoc>` must only appear on members that actually inherit from a base class or implement an interface (see §8).
- The set of `<param>` tags must exactly match the parameter list — no extras, no missing.
- The set of `<typeparam>` tags must exactly match the generic parameter list.
- All `<exception cref="ExceptionType">` values must reference a real, resolvable exception type.
- No empty tags: every opening tag must have meaningful content.
- The XML inside documentation headers must be well-formed.

### 4e. Block-Level Element Wrapping

- `<remarks>` and `<note>` content must be wrapped in block-level elements. Use `<para>` for prose paragraphs.

  **Wrong:**
  ```xml
  <remarks>This method is thread-safe.</remarks>
  ```

  **Correct:**
  ```xml
  <remarks>
  <para>This method is thread-safe.</para>
  </remarks>
  ```

- Do not mix block-level and inline elements as siblings inside the same parent.

### 4f. Syntax Conversion (Markdown → XML Doc)

- Replace any Markdown formatting inside `///` comments with proper XML doc equivalents.
- `**bold**` → no direct equivalent; rephrase or use prose emphasis
- `` `TypeName` `` (inline code for a type/member) → `<see cref="TypeName"/>`
- `` `paramName` `` (inline code for a parameter) → `<paramref name="paramName"/>`
- `` `T` `` (inline code for a generic type param) → `<typeparamref name="T"/>`
- `` `codeSnippet` `` (inline code for a literal snippet) → `<c>codeSnippet</c>`
- Multi-line code blocks → `<code>` inside `<example>` or `<remarks>`
- Bulleted lists → `<list type="bullet">` with `<item><description>...</description></item>`
- Numbered lists → `<list type="number">`
- Tables → `<list type="table">` with `<item><term>...</term><description>...</description></item>`
- Empty `<para/>` or `<p/>` used as spacers → remove (use real paragraphs instead)
- HTML tags equivalent to known XML doc elements → replace with the XML doc form
- Unnecessary HTML character entities → remove or simplify

---

## 5. XML Tag Reference

| Tag | Where | When | How | Notes |
| --- | --- | --- | --- | --- |
| `<summary>` | Any type or member | Always on documented elements | Short, purpose-focused prose | Most important tag; describes *what for*, not *how* |
| `<remarks>` | Types, methods, properties | Additional detail beyond summary | Longer prose; use `<para>` for paragraphs | Good for threading, lifecycle, protocol notes |
| `<value>` | Properties, indexers | Always on documented properties | Describe the meaning of the value | Pair with `<summary>` |
| `<returns>` | Non-void methods, delegates | Always on documented non-void methods | Describe meaning and conditions of return value | Omit on void; omit on constructors |
| `<param name="">` | Methods, constructors, delegates, indexers | One per parameter | Describe purpose and constraints | Name must match exactly |
| `<typeparam name="">` | Generic types and methods | One per type parameter | Describe what the type parameter represents; use a descriptive name | Name must match exactly |
| `<exception cref="">` | Methods, properties, constructors | For contractually thrown exceptions | Describe the circumstances that cause the exception | `cref` must resolve |
| `<example>` | Any member or type | To illustrate usage | Wrap code in `<code>` block | High-value for public APIs |
| `<code>` | Inside `<example>`, `<remarks>` | Multi-line code snippets | Preserve formatting and indentation | Rendered as a code block |
| `<c>` | Inline in prose | Short code fragments, keywords, values | Wrap the literal snippet | Do not use for type/member names — use `<see cref="">` |
| `<para>` | Inside text-containing tags | Multiple paragraphs | Separate logical sections | Required inside `<remarks>` and `<note>` |
| `<list type="">` | Inside remarks, examples | Structured lists | Types: `bullet`, `number`, `table` | Requires `<item>` children |
| `<item>` | Inside `<list>` | One entry per list item | Contains `<term>` and/or `<description>` | Structure varies by list type |
| `<term>` | Inside table/definition lists | Label or heading | Short text | Pair with `<description>` |
| `<description>` | Inside lists | Expanded explanation | Detailed prose | Pair with `<term>` |
| `<see cref="">` | Inline in prose | Cross-reference to a type, member, or field | Use fully-qualified or resolvable symbol | Compiler validates the reference |
| `<seealso cref="">` | Type or member level | Related symbols | Usually near the end of docs | Generates a "See Also" section |
| `<see href="">` | Inline external links | Link to a URL | Use an absolute URL | Supported in modern tooling |
| `<seealso href="">` | Type or member level | External related references | Use an absolute URL | Less common than `cref` form |
| `<paramref name="">` | Inside prose | Reference a parameter inline | Use exact parameter name | Renders consistently; preferred over `<c>` for params |
| `<typeparamref name="">` | Inside prose | Reference a generic type parameter inline | Use exact type param name | Generic equivalent of `<paramref>` |
| `<inheritdoc/>` | Types or members that inherit/implement | Inherit docs from base or interface | Usually standalone; see §8 | Must have something to inherit |
| `<inheritdoc cref="">` | Same as above | Inherit from a specific symbol | Use when the source is ambiguous | More precise than plain `<inheritdoc/>` |
| `<include>` | Any member | Reuse external XML doc file | References an external XML file and XPath | Useful for shared documentation |
| `<![CDATA[ ... ]]>` | Anywhere XML escaping is awkward | Raw embedded text with angle brackets | Wrap the unescaped content | Useful for complex code examples |

---

## 6. XML Documentation vs. Inline Comments

| Type | Audience | Purpose | Placement |
| --- | --- | --- | --- |
| XML docs (`///`) | API consumers | Usage contract, IntelliSense, generated docs | Before type/member declarations |
| Inline comments (`//`) | Maintainers | Internal reasoning, constraints, non-obvious decisions | Inside method bodies |
| Architecture docs | Teams | System-level tradeoffs and design decisions | External documents or ADRs |

XML docs and inline comments serve different audiences. A public method may need both: XML docs for callers (what, when, preconditions) and inline comments for maintainers (why this particular algorithm, what invariant is being maintained).

---

## 7. Scope-Specific Rules

### Constructors

- `<summary>` must begin: `Initializes a new instance of the <see cref="ClassName"/> class.`
  (Replace "class" with "struct" or "record" as appropriate.)
- Document all parameters.
- Document exceptions thrown by guard clauses.

### Properties

- `<summary>` must reflect the accessible accessors:
  - Get-only → `Gets the ...`
  - Get + set → `Gets or sets the ...`
  - Set-only → `Sets the ...`
  - Get + init → `Gets the ...`
- `<value>` is required: describe the meaning and valid range/states of the value.

### Enums

- The enum type itself requires a `<summary>` explaining what it represents.
- Every enum value requires a `<summary>` that includes the value's name in context and explains what that state or option means. Do not just restate the name.

### Generic Types and Members

- Every generic type parameter must have a `<typeparam>` tag.
- The `name` attribute must exactly match the declared type parameter.
- In prose, reference type parameters with `<typeparamref name="T"/>`.
- Type parameter names in `<typeparam>` content must be descriptive:

### Partial Classes

- The **primary partial file** (usually the one without an interface suffix) documents the type itself with `<summary>` and `<remarks>`.
- Other partial files document the members they declare.
- Members in other partial files that implement an interface should use `<inheritdoc/>` if the interface already has docs.
- Do not repeat the type-level summary across multiple partial files use `/// <content>` (not `/// <summary>`) on every non-root partial

---

## 8. `<inheritdoc/>` Rules

### When to use

Use `<inheritdoc/>` on a member when:

- The member **implements an interface member** — the interface already has the authoritative doc.
- The member **overrides a base class member** — the base class already has the authoritative doc.
- The docs would be identical to the base/interface and add no new information.

### When to use `<inheritdoc cref="..."/>`

Use the `cref` form when:

- The type implements multiple interfaces that both declare a member with the same name — specify which interface's docs to inherit.
- A partial class wants to inherit from a specific partial sibling.

### When NOT to use `<inheritdoc/>`

- On a type or member that neither inherits from a base class nor implements any interface.
- When the overriding or implementing member has meaningfully different behavior, preconditions, or exceptions — in that case, write explicit docs (or use `<inheritdoc/>` plus additional `<remarks>`).

### Extending inherited docs

If the overriding member has additional behavior, exceptions, or constraints not present on the base:

```xml
/// <inheritdoc/>
/// <exception cref="InvalidOperationException">
/// Thrown if the connection has already been disposed.
/// (The base interface does not throw this.)
/// </exception>
```

---

## 9. Test Method Rules

Tests are unique because well-named test methods with clear Arrange/Act/Assert structure are self-documenting.

### Skip Additional XML docs when

The test method name and test body together make the intent completely clear:

- Name follows a convention like `MethodName_Condition_ExpectedResult`
- The Arrange, Act, and Assert sections are obvious from reading the code
- No domain-specific preconditions or constraints require explanation

### Add XML docs when

- The test name is ambiguous or abbreviated
- A non-obvious precondition exists (e.g., a specific OS, network state, or clock dependency)
- A domain concept in the test body would be confusing to a reader unfamiliar with the domain
- The test exists to catch a regression from a specific bug — link to the bug/issue

### Do NOT Remove existing docs if they are quality

- Even is comments and documentation is already present on a test, do not remove the comments unless they are incorrect or invalid.

### Always fix malformed existing comments

Even in test projects, fix:
- Empty `<summary>` tags
- Tags with incorrect parameter names
- Badly-formed XML
- `<inheritdoc/>` misuse

---

## 10. Public Facing API Controller Methods

Public facing controller methods xml comments are handled different as the documentation is user facing and read by consumers and OpenAPI documentation.
Avoid over technical terms and abbreviations
Include specific GET/POST/PUT/DELETE/PATCH information when relevant to an API user
Explain what each public facing argument does in a manner that is clear to a junior developer.
Always fix malformed existing comments
If it is known and obvious add information stating if the endpoint requires special permissions

---

## 11. Generated File Patterns

Skip documentation work entirely on files matching these patterns. These files are owned by tooling and edits will be overwritten:

**By filename:**
- `*.g.cs` — source generators
- `*.Designer.cs` — Windows Forms / XAML designers
- `AssemblyInfo.cs` — assembly metadata
- `GlobalUsings.cs`, `GlobalUsings.g.cs` — implicit usings

**By path:**
- Any file inside `obj/` or `bin/` directories

**By content:**
- Files whose first 5 lines contain `<auto-generated>` or `// This code was auto-generated`

---

## 12. Quality Checklist

The agent runs this checklist against its own edits before reporting completion.

### Structural Completeness

- [ ] Every public type has a non-empty `<summary>` explaining its purpose
- [ ] Every public method documents all parameters (`<param>`), return value (`<returns>` if non-void), and contractually thrown exceptions (`<exception>`)
- [ ] Every public property has `<summary>` and `<value>`
- [ ] Every enum value has a `<summary>`
- [ ] Every generic type parameter has a `<typeparam>` with descriptive content

### Content Quality

- [ ] No comment restates what the identifier or signature already says
- [ ] No default placeholder text (Visual Studio generated summaries)
- [ ] No copied/pasted duplicate documentation across members

### `<inheritdoc/>` Correctness

- [ ] Every member that implements an interface or overrides a base class member uses `<inheritdoc/>` (or `<inheritdoc cref="..."/>` when the source is ambiguous)
- [ ] No `<inheritdoc/>` appears on a member that has nothing to inherit from

### Tag Correctness

- [ ] No `<returns>` on void methods or constructors
- [ ] All `<param>` names exactly match the actual parameter names
- [ ] All `<typeparam>` names exactly match the generic type parameter names
- [ ] `<remarks>` and `<note>` content is wrapped in `<para>` block-level elements
- [ ] Partial class primary file documents the type; other partials document their own members

### Syntax and Formatting

- [ ] No markdown syntax inside XML doc comments (converted to XML doc tags)
- [ ] All `` `TypeName` `` inline code for types/members converted to `<see cref="TypeName"/>`
- [ ] All `` `paramName` `` inline code for params converted to `<paramref name="paramName"/>`
- [ ] Summary text: starts with capital letter, ends with period, no tabs, no duplicate whitespace, no blank lines
- [ ] Spelling verified against project `*.dict` files for domain-specific terms

### Test-Specific

- [ ] Test methods documented only when the method name and AAA structure do not fully convey intent
- [ ] All malformed existing comments in test files are fixed regardless of documentation requirements
