# Autolinked references and URLs

GitHub converts URLs, issue/PR references, commit SHAs, users, and teams into
links automatically. The behavior differs by surface — the key rule is that
**issue/PR/SHA references autolink only in conversations** (issues, PRs,
discussions, comments), **not in wikis or repository files**.

## Plain URLs

Bare URLs become links without Markdown syntax:

```markdown
Visit https://github.com — no brackets needed.
```

Standard Markdown `[text](url)` still works and gives you control over the
displayed text.

## Issues and pull requests

| Raw reference                                    | Short link                      |
| ------------------------------------------------ | ------------------------------- |
| `#26`                                            | `#26`                           |
| `GH-26`                                          | `GH-26`                         |
| `jlord/sheetsee.js#26`                           | `jlord/sheetsee.js#26`          |
| `github-linguist/linguist#4039`                  | `github-linguist/linguist#4039` |
| Full URL `https://github.com/…/issues/26`        | `#26`                           |

Inside a **task list**, an issue/PR reference unfurls to show its title and
state — see `tasklists.md`.

## Commit SHAs

Any full 40-char SHA becomes a shortened commit link:

| Raw                                        | Short       |
| ------------------------------------------ | ----------- |
| `a5c3785ed8d6a35868bc169f07e40e889087fd2e` | `a5c3785`   |
| `jlord@a5c3785ed8d6a35868bc169f07e40e889087fd2e` | `jlord@a5c3785` |
| `jlord/sheetsee.js@<SHA>`                  | `jlord/sheetsee.js@a5c3785` |

Short SHAs (7–40 chars) also autolink when unambiguous.

## Users and teams

`@username` and `@org/team` mention the person/team and fire notifications
(read access to the repo required to receive them). Mentions trigger on edit
too, and typing `@` shows an autocomplete filtered to collaborators and thread
participants.

## Labels

Pasting a **label URL** (e.g. `https://github.com/github/docs/labels/enhancement`)
renders the label chip — but only for labels in the **same repository**. A label
name containing a period (`.`) will not render from its URL.

## Custom autolinks

Repos can configure custom autolinks (JIRA, Zendesk, etc.) so patterns like
`JIRA-123` shorten into links. Availability is repo-specific.

## Suppressing autolinks

- Wrap in **inline code** (`` `#26` ``) to render literally.
- Escape with a backslash: `\#26`.

## Backlinks and `redirect.github.com`

Referencing an issue from a PR generates a **backlink** from the issue to the
PR. To suppress the backlink, build the link with `redirect.github.com`
instead of `github.com` — the pop-up preview is also disabled. (Not supported on
GitHub Enterprise Cloud with Data Residency.)

```markdown
[#26](https://redirect.github.com/org/repo/issues/26)
```

## Pragmatic rules

1. Rely on `#N`, `user/repo#N`, SHAs, and `@mentions` in conversation text.
2. Remember: no issue/PR/SHA autolinks in **wikis or repo files** — write full
   Markdown links there.
3. Use inline code or `\` to show a literal `#N` without a link.
4. Use `redirect.github.com` only when you must suppress backlinks.