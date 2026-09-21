# Tables

GFM-spec extension (not CommonMark). Pipes `|` delimit cells, a hyphen row
separates the header from body rows. Tables render in comments, issues, PRs,
and wikis.

## Minimum viable table

A **blank line before the table** is required, or it may not render. Edge pipes
are optional. Each column needs **at least three hyphens** in the delimiter row.

```markdown
| Command      | Description                          |
| ------------ | ------------------------------------ |
| `git status` | List all new or modified files       |
| `git diff`   | Show changes that haven't been staged|
```

Cells do not need to align in the source; keep them aligned for readability.

## Alignment

Colons in the delimiter row set column alignment: `:---` left, `:---:` center,
`---:` right.

```markdown
| Left      | Center       | Right |
| :-------- | :----------: | ----: |
| value     | value        | value |
```

## Formatting inside cells

Inline Markdown works in cells: links, inline code, bold, italic, and
strikethrough.

```markdown
| Task | Status |
| ---- | ------ |
| Build **CI** | [Done](https://github.com/org/repo/actions) |
| Write `docs.md` | Pending |
```

Block elements do **not** work inside a single cell — no paragraphs, lists, or
headings. Use `<br>` for a forced second line:

```markdown
| Name | Notes        |
| ---- | ------------ |
| alpha| First line<br>Second line |
```

## Escaping the pipe

A literal `|` in a cell is the delimiter, so escape it with a backslash. Inside
inline code, the backslash approach also works:

```markdown
| Symbol | Name  |
| ------ | ----- |
| `\|`   | pipe  |
| `\|`   | escape|
```

Prefer the code form `\|` in cells meant to show syntax, since a bare `\|`
outside code renders just the pipe.

## What tables cannot do

- **No spanning cells** (rowspan/colspan). Workaround: merge content in one cell
  with `<br>`, or split into multiple tables.
- **No block content** in a cell (lists, code fences, blockquotes).
- **No nested task-list checkboxes** — a `[ ]` in a table cell renders as literal
  text, not an interactive box.
- **Header row is mandatory.** A delimiter row is required; without it, it is
  not a table.

## Multi-line / wrapping

Long cells wrap visually; you cannot put a literal newline inside one cell —
use `<br>` as above. For very wide tables, GitHub renders horizontally
scrollable in most surfaces.

## Pragmatic rules

1. Blank line before the table.
2. Escape `|` with `\|` (or in code).
3. Align delimiter row for readability; use colons only where alignment matters.
4. Keep cells single-line; use `<br>` for breaks.
5. Use inline formatting (`**bold**`, `` `code` ``, `[link](…)`) freely.