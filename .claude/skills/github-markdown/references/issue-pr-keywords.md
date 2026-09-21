# Issue & PR keywords

Use keywords in a pull request to **link it to an issue** and **auto-close the
issue when the PR merges**. Also used to mark an issue or PR as a duplicate.

## Closing keywords

One keyword + an issue reference closes the issue on merge:

```text
Closes #10
Fixes octo-org/octo-repo#100
```

Valid keywords (case-insensitive):

- `close`, `closes`, `closed`
- `fix`, `fixes`, `fixed`
- `resolve`, `resolves`, `resolved`

Reference the issue by number (`#10`) or cross-repo (`owner/repo#100`).

## Where the keyword must appear

- In the **PR description**, or
- In a **commit message** — but only for commits merged into the default branch
  do they take effect at merge time.

The keyword line can be anywhere in the description; it does not need to be at
the top.

## Multiple issues

Close several at once by repeating the pattern on separate lines (or in a
list):

```markdown
Fixes #10
Fixes #11
Closes #12
```

## Behavior

- The linked issue appears in the PR's **Development** sidebar.
- The issue closes **only when the PR merges** (not on open).
- The PR then appears on the closed issue as the fix reference.

## Marking a duplicate

In a comment on the issue or PR, write:

```text
Duplicate of #123
```

GitHub marks the target as a duplicate and links to it.

## Pragmatic rules

1. Prefer the PR **description** for keywords; commit-message keywords only
   work on the default branch.
2. Use `fixes`/`closes`/`resolves` families interchangeably — all are valid.
3. List every issue the PR resolves; don't rely on one line for many.
4. `Duplicate of` is a separate feature — don't use closing keywords for it.