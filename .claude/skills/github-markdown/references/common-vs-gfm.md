# CommonMark vs GFM vs GitHub.com

The single source of truth for **which tier a construct belongs to**. Three tiers,
not two. If a construct is GitHub.com-only and the page will also render
elsewhere (another forge, a local renderer, an npm README), expect it to degrade —
either silently or not at all.

## Tier map

| Construct                                              | Tier            | Notes                                                                  |
| ------------------------------------------------------ | --------------- | ---------------------------------------------------------------------- |
| Headings, paragraphs, thematic breaks                   | CommonMark      | `#`–`######`; blank line separates paragraphs                          |
| Emphasis (`*`/`_`), strong, inline code                 | CommonMark      | Nested `**bold _and italic_**` works                                    |
| Lists (ordered/unordered), nested lists                 | CommonMark      | Nest by aligning the marker under the parent's first text char          |
| Blockquotes                                            | CommonMark      | Alerts reuse blockquote syntax — see `alerts.md`                        |
| Links & images, reference links                         | CommonMark      |                                                                         |
| Indented code blocks (4 spaces)                         | CommonMark      | Legacy; prefer fenced blocks                                           |
| HTML blocks / raw inline HTML                          | CommonMark       | Some tags filtered — see "Tagfilter" below                              |
| Backslash escapes                                      | CommonMark/GFM   | CommonMark defines them; GFM tweaks which chars are escapable           |
| **Tables**                                             | **GFM spec**    | `references/tables.md`                                                  |
| **Task list items**                                    | **GFM spec**    | Interactive checkboxes only on GitHub.com — see `tasklists.md`          |
| **Strikethrough**                                      | **GFM spec**    | `~~text~~` (GFM) — also renders with `~text~` on GitHub.com             |
| **Literal autolinks**                                  | **GFM spec**    | Bare `www.example.com` / `http://…` become links — see `autolinks.md`   |
| **Tagfilter** (disallowed raw HTML)                    | **GFM spec**    | `` <title> ``, `<textarea>`, `<style>`, `<xmp>`, `<iframe>`,               |
|                                                        |                 | `<noembed>`, `<noframes>`, `<script>`, `<plaintext>` render as plain text |
| Footnotes `[^1]`                                       | GitHub.com-only | Not in the GFM spec; supported in issues/PRs/.md, **not wikis**          |
| `@mentions`, team mentions                             | GitHub.com-only | `@user`, `@org/team`; notifies + notifies on edit                      |
| Issue/PR/SHA references (`#12`, `user/repo#12`, SHAs)   | GitHub.com-only | Autolinked in conversations only, not wikis or repo files               |
| `:emoji:` shorthand                                    | GitHub.com-only | e.g. `:+1:`, `:shipit:`; not part of the GFM spec                       |
| Alerts (callouts)                                      | GitHub.com-only | `> [!NOTE]` etc. — see `alerts.md`                                      |
| Math (`$…$`, `$$…$$`, `math` fence)                     | GitHub.com-only | MathJax — see `math.md`                                                 |
| Diagrams (Mermaid/GeoJSON/TopoJSON/STL)                 | GitHub.com-only | Fenced-block syntax — see `references/diagrams/`                        |
| Collapsed sections (`<details>`)                       | HTML (renders)  | Plain HTML tag; GitHub renders it, most other renderers will too         |
| Color models in backticks (`#0969DA`, `rgb()`, `hsl()`) | GitHub.com-only | Renders a swatch in issues/PRs/discussions only                         |
| File attachments / anonymized upload URLs               | GitHub.com-only | `attaching-files.md`                                                    |
| Snippet permalinks (`#L12-L20`)                         | GitHub.com-only | Comment context only — see `permanent-links.md`                         |
| `?plain=1` on `.md` file URLs                           | GitHub.com-only | Shows markdown source with line anchors                                 |

## Lightweight GFM/GitHub features (no dedicated file)

These are small enough to live here rather than get their own reference.

### Strikethrough

GFM spec uses `~~double tilde~~`. GitHub.com additionally accepts single-tilde
`~text~`. Tilde inside a code span is literal, not strikethrough.

```markdown
~~This was a mistake~~ but ~this is also struck~ on GitHub.
```

### Emoji shorthand

`:` + code + `:` renders an emoji. The autocomplete list appears as you type
`:` in a comment field.

```markdown
:rocket: Released! :white_check_mark: :tada:
```

Codes live in the emoji-cheat-sheet; unknown codes stay literal text.

### Footnotes

`[^n]` marker in text, definition anywhere (position does not control where the
note renders — it always lands at the bottom).

```markdown
The parser is GFM-compliant[^1]. It is also fast[^2].

[^1]: Tested against the GFM spec test suite.
[^2]: Benchmarks on the repo wiki.  
    Note the two trailing spaces above to start this new line.
```

- Multiple lines in a definition need two trailing spaces for a soft line break.
- Footnotes are **not supported in wikis**.
- Numbering is automatic from the marker; use `[^1]`, `[^2]`, … or words like
  `[^note]`.

## Behavior differences worth knowing

### Soft line breaks

- **Issues/PRs/discussions:** a single newline renders as a line break.
- **`.md` files:** a single newline does **not** break the line — use two
  trailing spaces, a trailing `\`, or `<br/>`.

### Backslash escapes

Backslash escapes Markdown punctuation (``\ ` * _ {} [] () # + - . ! | ~``) so
it renders literally. GFM's table support changes the pipe: `\|` inside a table
cell is the way to show a literal `|` — see `tables.md`. Escape does **not**
work in the title of an issue or PR.

### HTML comments

`<!-- ... -->` hides content from rendered output everywhere — handy for
editor notes and scaffolding.

### Headings & anchors

GitHub auto-generates a table of contents ("Outline") from headings and an
anchor per heading: lowercase, spaces → `-`, punctuation stripped, formatting
removed, duplicates get `-1`, `-2`, … suffixes. Custom anchors
(`<a name="…"></a>`) are linkable but excluded from the outline.

### Relative links & images

Inside repo files, use relative paths (`docs/x.md`, `/assets/img.png`, `../`)
— GitHub rewrites them for the current branch, and clones work offline.
Leading `/` means repository root. Link text must be on a single line.