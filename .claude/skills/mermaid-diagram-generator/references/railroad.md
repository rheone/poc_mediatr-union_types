---
diagram: Railroad
slug: railroad
status: beta
mermaid_version_introduced: "v11.16.0"
mermaid_version_verified: "12.0.0"
keyword: railroad-ebnf-beta
source: https://mermaid.js.org/syntax/railroad.html
last_verified: 2026-09-19
plugin_required: false
---

# Railroad

> **Status:** Beta - introduced v11.16.0. Syntax may evolve; treat generated diagrams of this type as more likely to need adjustment than stable types.

## Overview
A railroad (syntax) diagram draws a context-free grammar as tracks: terminals, non-terminals, branches for choices and loops for repetition. Four keywords select the notation you write in; pick the one that matches the grammar you have: `railroad-ebnf-beta` (EBNF, W3C or ISO 14977 style), `railroad-abnf-beta` (RFC 5234), `railroad-peg-beta` (Parsing Expression Grammar), and `railroad-beta` (Mermaid's own intermediate representation, written as explicit constructors).

## Best-fit uses
- Documenting a language, file format or protocol grammar for readers who find BNF hard to scan
- Showing a single rule's alternatives, optional parts and repetition at a glance

## When NOT to use this
- The grammar is a hierarchy of things, not rules - use `mindmap.md` or `treeview.md`
- You need a process or state flow - use `flowchart.md` or `state.md`

## Basic syntax
The first line is the keyword. Optionally `title "text"` and `accTitle:` / `accDescr:`. Each rule is one statement ending in `;`.

- **EBNF** (`railroad-ebnf-beta`): `name = definition ;` (`::=` also accepted). Terminal `"text"` or `'text'`; non-terminal is an identifier. Choice `|`; W3C sequence `A B`, ISO sequence `A , B`; optional `A?` or `[ A ]`; repeat `A*` / `A+` or `{ A }`; group `( ... )`; comments `/* ... */` or `(* ... *)`; special sequence `? text ?`; exception `A - B`.
- **ABNF** (`railroad-abnf-beta`): `name = definition ;`. Choice `/`; repetition prefix `*A`, `1*A`, `2*4A`, `3A`; optional `[ A ]`; numeric terminals `%x41`, `%d65`, `%b1000001`, ranges `%x30-39`; comments start with `;` and run to end of line.
- **PEG** (`railroad-peg-beta`): `Name <- definition ;`. Ordered choice `/`; suffixes `?`, `*`, `+`; predicates `&A` and `!A`; `.` any character; comments start with `#`.
- **IR** (`railroad-beta`): `name = expression ;` with constructors `terminal("t")`, `nonterminal("n")`, `sequence(a, b)`, `choice(a, b)`, `optional(a)`, `zeroOrMore(a)`, `oneOrMore(a)`, `special("t")`.

## Simple example
<!-- mermaid-validate: keyword-exempt reason="railroad family: railroad-ebnf-beta, railroad-abnf-beta, railroad-peg-beta, railroad-beta" -->
```mermaid
railroad-ebnf-beta
title "Optional Sign"

sign = "+" | "-" ;
number = sign? digit+ ;
digit = "0" | "1" | "2" | "3" | "4" | "5" | "6" | "7" | "8" | "9" ;
```
Three EBNF rules; `number` shows an optional element and a one-or-more repetition.

## Complex example
<!-- mermaid-validate: keyword-exempt reason="railroad family: railroad-ebnf-beta, railroad-abnf-beta, railroad-peg-beta, railroad-beta" -->
```mermaid
railroad-peg-beta
title "Calculator Grammar"

Expression <- Term (("+" / "-") Term)* ;
Term <- Factor (("*" / "/") Factor)* ;
Factor <- Number / "(" Expression ")" ;
Number <- Digit+ ;
Digit <- "0" / "1" / "2" / "3" / "4" / "5" / "6" / "7" / "8" / "9" ;
```
A PEG expression grammar: ordered choice, grouped repetition and mutually recursive rules.

## Escaping & special characters
- Terminals are quoted strings; use the other quote style to embed a quote character (`'"'` or `"'"`).
- Every rule ends in `;` in all four notations. In ABNF `;` is also the comment marker: a `;` followed by text on the same line starts a comment and swallows the terminator, so end each rule with a bare `;` and put comments on their own line.
- Comment markers differ by notation: `/* */` or `(* *)` (EBNF), `;` (ABNF), `#` (PEG).
- Inside a ```mermaid fence, avoid a literal triple backtick in a terminal.
- **Tested (Mermaid 12.0.0 and 11.16.1):** Terminals take the other quote style to embed a quote (`'"'` or `"'"`); a `<`...`>` pair is read as an HTML tag and silently dropped, so write both as `#60;` and `#62;`.

## Common pitfalls
- [ ] Does the keyword match the notation used in the rules? Each keyword parses only its own notation (`<-` under `railroad-ebnf-beta`, or `|` under `railroad-peg-beta`, is a lexer error), and a bare `railroad` keyword is not recognised.
- [ ] In ABNF, does every rule end with a bare `;` and every comment sit on its own line?
- [ ] Does every rule end with `;`?
- [ ] Assignment operator matches the notation: `=` (EBNF, ABNF, IR) versus `<-` (PEG)?
- [ ] Choice operator matches the notation: `|` (EBNF) versus `/` (ABNF, PEG)?
- [ ] Repetition is a prefix in ABNF (`1*A`) but a suffix in EBNF and PEG (`A+`)?
- [ ] Is `look: handDrawn` avoided? It is not supported for railroad diagrams.

## v11 fallback
Introduced in v11.16.0, so it parses on every renderer at or above that (GitLab 19.4 and VS Code's preview; see `general/renderers.md`). Older renderers, including Obsidian's Mermaid 11.13.0, fail with `No diagram type detected`; for those, deliver the grammar as text in a fenced code block. No syntax differs between 11.16.1 and 12.0.0.

## Beta/experimental caveats
Railroad diagrams are beta as of v11.16.0. All four keywords carry the `-beta` suffix, and there is no bare `railroad` keyword. The docs state the hand-drawn look is not supported.

## Further reading
- https://mermaid.js.org/syntax/railroad.html
- https://datatracker.ietf.org/doc/html/rfc5234 (ABNF)
- https://www.cl.cam.ac.uk/~mgk25/iso-14977.pdf (ISO 14977 EBNF)
