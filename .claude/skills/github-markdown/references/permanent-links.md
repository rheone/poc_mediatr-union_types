# Permanent links to code snippets

Permanent links pin a specific **line or line range** of a file so the link stays
valid as the file evolves. GitHub renders the referenced lines as an inline code
snippet when pasted into a **comment**.

## Anatomy

A permalink is a file URL plus a fragment: `#L<start>` for one line,
`#L<start>-L<end>` for a range.

```text
https://github.com/org/repo/blob/main/src/index.ts#L12-L20
```

## How it renders

- **In the originating repo's comment field** (issue, PR, discussion): the
  permalink renders as a **code snippet** with a file-name + line-range header.
- **Anywhere else** (other repos, `.md` files, wikis): it renders as a plain
  URL, not a snippet.
- **`.md` files:** snippet rendering does not apply — use `?plain=1` to point at
  the markdown *source* with line anchors (below).

## Creating one

1. Open the file (or a PR's **Files changed** → **View** on a file).
2. Single line: click its line number.
3. Range: click the first line number, then **Shift**+click the last.
4. Open the line-options (kebab) menu and choose **Copy permalink**.
5. Paste into the comment.

The `y` keyboard shortcut copies the permalink for the current selection.

## Linking into Markdown files

Rendered Markdown has no line anchors, so load the source with `?plain=1`,
then append `#L…`:

```text
https://github.com/org/repo/blob/main/README.md?plain=1#L14
```

## Permanent vs "current branch" links

The permalink produced by **Copy permalink** is pinned to the **commit SHA** —
that is what makes it permanent. A link built by hand against `main` is only
stable until the file changes on that branch.

## Quoting a snippet without a permalink

To embed a snippet inline in a comment, blockquote the code instead:

```markdown
> ```ts
> const x = await client.fetch(id);
> ```
```

Renders as a quoted code block inside the comment — good when the lines are
short or the target file may not exist in the reader's context.

## Pragmatic rules

1. Use the **Copy permalink** flow (SHA-pinned) for anything you want to last.
2. Prefer line ranges for anything longer than one or two lines.
3. Use `?plain=1` when linking Markdown files.
4. Remember the snippet view only appears in comments in the originating repo.