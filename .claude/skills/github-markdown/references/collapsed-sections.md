# Collapsed sections

Hide content behind a click-to-expand toggle using the HTML `<details>` /
`<summary>` tags. Not a GFM-spec construct — GitHub renders these HTML tags in
every Markdown surface, and most other HTML-capable renderers will too.

## Basic pattern

A **blank line after `<summary>`** is required for the Markdown inside to
render; without it, content is treated as literal HTML text.

```markdown
<details>

<summary>Why does this table exist?</summary>

The table below was added to track migration status. It is updated weekly by
the release automation.

| Component | Status   |
| --------- | -------- |
| api       | migrated |
| worker    | pending  |

</details>
```

## Default-open variant

Add the `open` attribute to `<details>` to render expanded by default:

```html
<details open>
```

Readers can still collapse it manually.

## What can go inside

Almost any Markdown — headings, images, lists, code blocks, and nested
collapsibles. The collapsed content is simply not visible until expanded.

````markdown
<details>

<summary>Debug output</summary>

## Log tail

```
ERROR 12:34:01 connection refused
ERROR 12:34:05 retry failed
```

<details>

<summary>Full trace</summary>

```
Error: ECONNREFUSED 10.0.0.5:443
    at TCPConnectWrap.afterConnect [as oncomplete] (net.js:1304:16)
```

</details>

</details>
````

## Practical uses

- **Spoilers / optional detail** in issues and PRs — e.g. long logs, screenshots,
  reproduction dumps.
- **Table of contents** with per-section breakdowns in READMEs.
- **Changelog pruning** — old release notes collapsed under `<details>`.

## Rules of thumb

1. Always leave a blank line after `<summary>`.
2. Close both tags; nesting requires matching open/close pairs per level.
3. The `<summary>` label itself can contain inline Markdown/HTML but keep it
   short — it is the always-visible trigger line.
4. Do not rely on `open` for critical info; it only changes default state.