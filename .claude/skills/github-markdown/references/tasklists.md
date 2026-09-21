# Task lists

Interactive checkboxes in issues and PRs. The syntax is the GFM task-list-item
extension; the *interactivity* is GitHub.com-only.

## Syntax

Hyphen (or `*`), space, then `[ ]` (open) or `[x]` (done):

```markdown
- [x] #739
- [ ] https://github.com/octo-org/octo-repo/issues/740
- [ ] Add delight to the experience when all tasks are complete :tada:
```

Nesting works with indentation; items render on separate lines each with a
clickable box.

## Escape a leading parenthesis

If an item's description begins with `(`, escape it or the parser misreads the
task:

```markdown
- [ ] \(Optional) Open a follow-up issue
```

## Where interactivity works

- **Issues and PRs (bodies and comments):** clickable, with drag-to-reorder
  (grip dots appear on hover), and in issue bodies a **progress meter** shows
  completion (e.g. `2 of 5`).
- **`.md` files, wikis, discussions, releases:** render as static checkboxes
  (or plain text) — not clickable.
- **Tasklist blocks are retired** (Feb 2025); GitHub now recommends **sub-issues**
  for issue-to-issue tracking. Plain Markdown task lists still work everywhere.

## Issue-body superpowers

When the task list is in an **issue body**:

- The issue's list shows completion progress in repo issue views.
- A task referencing another issue **auto-checks** when that issue closes.
- Tasks can be **converted to issues** (hover → issue icon).
- Referenced issues show a **"Tracked by"** link back to the issue.

## Limitations

- **Cannot add task-list items to closed issues** or issues with linked PRs.
- **Not clickable in tables** — a `[ ]` inside a table cell is literal text.
- Not interactive in code blocks (obviously) or in rendered `.md` files.

## References inside task lists

Issue/PR references in a task item **unfurl** — the box label shows the issue
title and state, not just `#123`. URLs do the same.

## Pragmatic rules

1. Use `- [ ]` / `- [x]` with a space after the hyphen.
2. Prefer task lists in issue/PR bodies where they are interactive and counted.
3. Escape a leading `(` with `\`.
4. For cross-issue tracking, point to sub-issues or link tasks; rely on
   auto-check via referenced-issue closure.