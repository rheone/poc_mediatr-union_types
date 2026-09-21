# Alerts

Alerts (a.k.a. callouts or admonitions) render colored, icon-bordered boxes.
They are a blockquote-based GitHub.com extension — **not** part of the GFM
spec. Renders in issues, PRs, discussions, wikis, and `.md` files.

## The five types

| Directive      | Tone                             |
| -------------- | -------------------------------- |
| `[!NOTE]`      | Useful info, safe to skim        |
| `[!TIP]`       | Helpful advice, better/easier way|
| `[!IMPORTANT]` | Key info needed to reach a goal  |
| `[!WARNING]`   | Urgent; act to avoid problems    |
| `[!CAUTION]`   | Risk / negative outcome warning  |

```markdown
> [!NOTE]
> Useful information that users should know, even when skimming content.

> [!TIP]
> Helpful advice for doing things better or more easily.

> [!IMPORTANT]
> Key information users need to know to achieve their goal.

> [!WARNING]
> Urgent info that needs immediate user attention to avoid problems.

> [!CAUTION]
> Advises about risks or negative outcomes of certain actions.
```

The type word is matched case-insensitively on github.com (`> [!note]` and
`> [!Note]` render too), but keep it uppercase to match GitHub's own docs and
grep-ability.

## Syntax rules

- The directive is the **first line of a blockquote**: `> [!NOTE]`.
- The body follows as standard blockquote lines; multiple paragraphs continue
  the blockquote.
- The body can contain **inline Markdown and code spans**; block elements such
  as nested blockquotes, lists, and code fences inside the alert body are
  unreliable — keep the body to prose + inline formatting.
- **Alerts cannot be nested inside other elements** (e.g. inside a list item or
  a table cell they will not render as alerts).

## Usage guidance

- **Use sparingly** — one or two per page; the visual weight devalues when
  overused.
- **Avoid consecutive alerts** (stacked boxes look like noise).
- Match severity to type: reserve `WARNING`/`CAUTION` for real risk, don't use
  them for gentle nudges.

## Multi-line example

```markdown
> [!WARNING]
> This migration deletes the old database.
>
> Back it up first, then run the script.
>
> Run: `npm run migrate` from the repo root.
```

## Portability caveat

Because alerts are GitHub-only, a page using them loses the boxes on other
renderers — the blockquote content still shows, but without color/icon. If the
target must render identically elsewhere, use plain blockquotes or bold text
instead of alerts.