# Code blocks

Fenced code blocks (GFM) with language-tagged syntax highlighting. Renders in
all Markdown surfaces.

## Fencing

Triple backticks open/close a block. Tilde fences (`~~~…~~~`) also work and
are equivalent per the GFM spec.

````markdown
```
function test() {
  console.log("notice the blank line before this function?");
}
```
````

Recommend a blank line before and after the fence in the source for legibility.

## Language identifier = syntax highlighting

Put the language on the opening fence. GitHub uses **Linguist** for detection;
the valid language names live in Linguist's `languages.yml`. Lower-case
identifiers are required for GitHub Pages rendering.

````markdown
```ruby
require 'redcarpet'
markdown = Redcarpet.new("Hello World!")
puts markdown.to_html
```
````

### Common identifiers

`bash`/`shell`/`console`, `c`, `cpp`, `csharp`/`cs`, `css`, `diff`, `go`,
`html`, `java`, `js`/`javascript`/`ts`/`typescript`, `json`, `jsx`, `kotlin`,
`markdown`/`md`, `php`, `powershell`/`ps1`, `python`/`py`, `rust`, `sql`,
`swift`, `tsx`, `xml`, `yaml`/`yml`.

Use `text` (or `plaintext`) to disable highlighting deliberately — e.g. for
ASCII art or pseudo-code.

## Diff highlighting

Prefix changed lines with `+`/`-` and tag the fence `diff`:

````diff
```diff
- console.log("old");
+ logger.info("new");
```
````

Same trick applies to per-line suggestions in review comments.

## Showing a fence inside a fence

A literal triple backtick inside a fenced block closes it early. Use a longer
outer fence — quadruple backticks around content that contains triple:

`````markdown
````
```
This is literal triple-backtick content.
```
````
`````

Rule: **outer fence must have more backticks than anything it contains.**

## Code blocks inside lists

Indented, non-fenced code inside a list item must be indented **eight spaces**
to stay inside the item. A fenced block is easier: indent the fence markers to
the list item's content level.

```markdown
1. First step:

   ```bash
   npm install
   ```

2. Second step: run tests
```

## Gotchas

1. **Blank lines matter** inside a list — the fence must not collide with the
   item text.
2. **Language spelling:** Linguist matches aliases, but an unknown tag just
   disables highlighting (falls back to `text`) rather than erroring.
3. **Math/diagram fences** are separate: `math`, `mermaid`, `geojson`,
   `topojson`, `stl` are *not* syntax-highlighting languages — they render
   content. See `math.md` and `references/diagrams/`.
4. Inline code (single backticks) is not highlighted; it renders monospace with
   a subtle background only.

## Pragmatic rules

1. Always tag the language when the content is code.
2. Keep fences on their own lines; no trailing content after the opening fence.
3. Use `text` for anything you do not want highlighted.
4. When the block itself must display backticks, bump the outer fence count.
5. In lists, indent the fence to the item's content level (or use 8-space
   indentation for raw indented blocks).