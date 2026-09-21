# Mathematical expressions

GitHub renders LaTeX math via **MathJax** in issues, PRs, discussions, wikis,
and `.md` files. For the full LaTeX math command catalog (symbols, fractions,
sums, matrices, spacing), see the **Wikibooks LaTeX/Mathematics** guide —
<https://en.wikibooks.org/wiki/LaTeX/Mathematics>. This file covers only the
GitHub-specific delimiters and pitfalls.

## Inline math

Two delimiter options:

```markdown
The ratio is $\frac{a}{b}$.

The ratio is $`\frac{a}{b}`$.
```

- `$…$` — the common form.
- `` $`…`$ `` — backtick-delimited; safer when the expression contains
  characters that collide with Markdown syntax (e.g. `*`, `_`, `^` inside
  exponents or subscripts).

## Display math (block)

Two forms:

```markdown
The Cauchy–Schwarz inequality:

$$\left( \sum_{k=1}^n a_k b_k \right)^2 \leq \left( \sum_{k=1}^n a_k^2 \right) \left( \sum_{k=1}^n b_k^2 \right)$$
```

Or a `math` fenced block — no `$$` delimiters needed inside:

````markdown
```math
\left( \sum_{k=1}^n a_k b_k \right)^2 \leq \left( \sum_{k=1}^n a_k^2 \right) \left( \sum_{k=1}^n b_k^2 \right)
```
````

**Line-break gotcha:** in `.md` files a single newline does not break a line, so
a `$$` block placed right after a paragraph can end up inline. End the previous
line with a trailing backslash (`\`) to force the break before the block:

```markdown
The inequality:\
$$\sum_{k=1}^n a_k^2 \geq 0$$
```

## Literal dollar signs

A `$` you do *not* intend as math must be escaped — differently depending on
where it sits.

- **Inside a math expression:** prefix with a backslash — `$\sqrt{\$4}$`.
- **On the same line but outside math:** wrap in a span — `<span>$</span>100`.

```markdown
To split <span>$</span>100 in half, we compute $100/2$.
```

## Scope notes

- MathJax is a wide LaTeX superset, but not everything compiles: environment
  support and some macros differ from a full TeX install. Test complex
  expressions before relying on them.
- Math is not rendered in code blocks tagged as code (use `math`), in HTML
  comments, or in raw source view.
- Accessibility: MathJax exposes readable text; keep expressions inside math
  delimiters rather than pasting ASCII math so it parses.

## Pragmatic rules

1. Inline → `$…$`; switch to `` $`…`$ `` when the expression has Markdown-special
   characters.
2. Display → `$$…$$`, or ```math for big multi-line expressions.
3. In `.md`, terminate the preceding line with `\` before a `$$` block.
4. Escape stray `$` (`\$` inside math, `<span>$</span>` outside).
5. Verify complex expressions render; consult the Wikibooks guide for symbols
   and macros.