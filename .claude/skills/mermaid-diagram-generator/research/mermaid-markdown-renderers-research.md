# Mermaid version shipped by each Markdown renderer

Compiled 2026-09-19 (Mermaid 12.0.0 released 2026-09-10). Complements, and does not repeat,
`mermaid-v12-research.md` (which covers what changed in v12). Primary sources only. "Unstated" means
no first-party source gives a version. Rows marked *(inference)* derive from the v12 report's
per-feature version table (`usecase-beta`/`agentflow-beta` = 12.0.0; `railroad-*-beta` and `treeView-beta` = 11.16.0),
not from a renderer source.

## Summary

| Renderer | Mermaid version | As-of (renderer version/date) | v12-ready? |
|---|---|---|---|
| GitHub.com | unstated (docs say run `info`) | live 2026-09-19 | Unknown; probe with `info` |
| GitLab | 11.16.1 (bundled `mermaid-v11` alias) | GitLab 19.4.0 (tag 2026-09-16); docs "Mermaid version 11" | No |
| Obsidian | 11.13.0 | 1.13.0 EA 2026-05-28; public 1.13.4 2026-07-30; not changed through 1.14.2 EA (2026-09-15) | No |
| Azure DevOps wiki | unstated (limited syntax subset) | docs page updated 2026-06-15 | No |
| VS Code built-in preview | `^11.16.1` range, lockfile 11.17.0 (extension `mermaid-markdown-features`) | `main` 2026-09-19; feature introduced VS Code 1.121 (2026-05-20); latest 1.138.0 | No |
| Bitbucket Cloud | no native Markdown Mermaid; `.mmd` file view only, version unstated | 2026-09-19 | n/a |
| Docusaurus | stable 3.10.2 (2026-07-10): user-supplied `mermaid >=11.6.0`; `main` (v4.0.0, unreleased): `^12.0.0` | as stated | Stable: only if you install 12; v4 main: yes |
| MkDocs Material | loads `https://unpkg.com/mermaid@11/dist/mermaid.min.js` (latest 11.x, currently 11.17.2 per prior report) | 9.7.7 (2026-07-17) | No |
| Notion | unstated | n/a | Unknown |
| Mermaid Live Editor | `^12.0.0` on `master` | master merge 2026-09-14 (app 2.0.67) | Yes (source); deployed build unverified |
| Mermaid Chart | unstated | n/a | Unknown |

## Per-renderer detail

### GitHub
- Docs do not state a version; they instruct users: "To ensure GitHub supports your Mermaid syntax, check the Mermaid version currently in use", by putting `info` inside a `mermaid` fence.
  Sources: https://docs.github.com/en/get-started/writing-on-github/working-with-advanced-formatting/creating-diagrams ; source of that page: https://github.com/github/docs/blob/main/content/get-started/writing-on-github/working-with-advanced-formatting/creating-diagrams.md ("Checking your version of Mermaid").
- No changelog or blog entry giving a bundled version was found (only https://github.blog/changelog/2022-02-28-gists-now-support-mermaid-diagrams/ and the 2022 intro blog, which predate this). GitHub's renderer is closed source; no lockfile available.
- v12 expectation: **unknown**. Do not assume `usecase-beta`/`agentflow-beta`/neo/ELK defaults until `info` shows 12.x.

### GitLab
- Docs: "GitLab supports Mermaid version 11." and "Support for `treeView-beta` diagrams introduced in GitLab 19.4." Source: https://docs.gitlab.com/user/markdown/ (source file: https://gitlab.com/gitlab-org/gitlab/-/raw/master/doc/user/markdown.md, lines ~1623, 1632).
- `gitlab-org/gitlab` `package.json`: `"mermaid-v11": "npm:mermaid@11.16.1"` plus `"@mermaid-js/layout-elk": "0.2.2"`.
  - `master` (VERSION file `19.5.0-pre`): 11.16.1. https://gitlab.com/gitlab-org/gitlab/-/raw/master/package.json
  - tag `v19.4.0-ee` (tag commit 2026-09-16): 11.16.1. https://gitlab.com/gitlab-org/gitlab/-/raw/v19.4.0-ee/package.json
  - tag `v19.3.0-ee` (2026-08-20): 11.13.0. https://gitlab.com/gitlab-org/gitlab/-/raw/v19.3.0-ee/package.json
  - Commit "Update Mermaid to 11.16.1" dated 2026-08-20; "Register ELK layout loader for Mermaid" 2026-07-02 (from `repository/commits?path=package.json` API on gitlab.com).
- The package key is named `mermaid-v11` (an alias), so a future v12 would likely be added as a separate key; nothing in package.json indicates v12 work.
- v12: not expected. `usecase-beta`, `agentflow-beta` fail (types do not exist before 12.0.0). *(inference)* `railroad-*-beta` and `treeView-beta` exist in 11.16.x (GitLab 19.4 docs confirm treeView-beta).
- Note: the earlier skill claim "GitLab is a Mermaid v10 pin" is contradicted by these sources.

### Obsidian
- First-party changelog (https://obsidian.md/changelog.xml) says "Upgraded Mermaid to 11.13.0." in Desktop 1.13.0 Early access (https://obsidian.md/changelog/2026-05-28-desktop-v1.13.0/) and repeats it in the public 1.13 release (https://obsidian.md/changelog/2026-07-30-desktop-v1.13.4/). Previous upgrade: "Upgraded Mermaid to 11.3.0." in 1.8.0 (https://obsidian.md/changelog/2024-12-18-desktop-v1.8.0/).
- No later "Upgraded Mermaid" entry through 1.14.2 Early access (https://obsidian.md/changelog/2026-09-15-desktop-v1.14.2/), the newest entry in the feed. Help site (https://obsidian.md/help/syntax) states no version.
- Side note from the same feed: 1.13 made Mermaid rendering off by default, with a one-time banner asking to allow it per vault.
- v12: not expected. Lacks everything added after 11.13.0: `usecase-beta`, `agentflow-beta` (12.0.0), `railroad-*-beta` and `treeView-beta` *(inference, 11.16.0)*. Has Venn and Ishikawa (11.13.0, per https://mermaid.ai/blog/posts/mermaid-v11-13-0-two-new-diagram-types-and-our-most-polished-release-yet).

### Azure DevOps wiki / Markdown
- Docs page (ms.date 2026-06-03, updated 2026-06-15) states no Mermaid version. It lists supported types only: sequence, Gantt, flowchart, class, state, user journey, pie, requirement, gitgraph, ER, timeline; says "Azure DevOps provides limited syntax support ... Unsupported syntax includes most HTML tags, Font Awesome, `flowchart` syntax (use `graph` element instead), LongArrow `---->`, and more." Source: https://learn.microsoft.com/en-us/azure/devops/project/wiki/markdown-guidance?view=azure-devops (section "Work with Mermaid diagrams"; repo copy https://github.com/MicrosoftDocs/azure-devops-docs-pr/blob/live/docs/project/wiki/markdown-guidance.md). Supports `::: mermaid` containers and fenced blocks in wikis.
- v12: no. Beyond the 11 listed types, nothing is documented: mindmap, sankey, block, xychart, architecture, kanban, radar, venn, ishikawa, treemap, all `*-beta` types are not listed, so treat as unsupported.

### VS Code built-in Markdown preview
- Renders Mermaid natively since VS Code 1.121 (2026-05-20): built-in extension `mermaid-markdown-features` (merged from bierner's extension), covering the Markdown preview, notebook Markdown cells and chat. Sources: https://code.visualstudio.com/updates/v1_121 ; tracking issue https://github.com/microsoft/vscode/issues/293028 ; coverage https://visualstudiomagazine.com/articles/2026/05/20/vs-code-1-121-adds-remote-agents-built-in-html-and-mermaid-previews.aspx (third-party).
- `microsoft/vscode` `main` `extensions/mermaid-markdown-features/package.json`: dependencies `"mermaid": "^11.16.1"`, `"@mermaid-js/layout-elk": "^0.2.0"`, `"@mermaid-js/layout-tidy-tree": "^0.2.0"`, `"@mermaid-js/mermaid-zenuml": "^0.2.0"`. Its `package-lock.json` on `main` resolves `node_modules/mermaid` to **11.17.0**. https://github.com/microsoft/vscode/blob/main/extensions/mermaid-markdown-features/package.json
- `extensions/markdown-language-features/package.json` also carries `"mermaid": "^11.15.0"` (older dependency).
- Latest tag 1.138.0. The 1.138 release notes fetched contain no Mermaid entry. I did not verify the exact range in the 1.138.0 tag.
- Caret ranges cap at 11.x, so a v12 bump needs a code change (the plugin peers already allow 12: ZenUML 1.0.1, tidy-tree 1.0.1 per the v12 report).
- v12: not expected. `usecase-beta`/`agentflow-beta` fail. Extension `bierner.markdown-mermaid` (community) is also 11.x per the v12 report.

### Bitbucket
- Cloud: no native Mermaid in Markdown (Cloud Markdown is CommonMark). Standalone `.mmd` files render in the source view. Version unstated. Requests open/closed: https://jira.atlassian.com/browse/BCLOUD-21675 ("Implement Mermaid diagrams as part of Markdown"), https://jira.atlassian.com/browse/BCLOUD-18559 ; community thread https://community.atlassian.com/forums/Bitbucket-questions/Mermaid-support-for-bitbucket-markdown/qaq-p/2264240 . I could not retrieve a first-party Atlassian doc page stating this positively (support.atlassian.com markdown page returned 404 via fetch), so "no native Markdown support" rests on the Jira tickets and community thread.
- Data Center: Jira ticket https://jira.atlassian.com/browse/BSERV-12548 ("Mermaid extension for Markdown Renderer"); status not confirmed here.
- v12: n/a; no v12 beta type will render inside Markdown.

### Docusaurus (`@docusaurus/theme-mermaid`)
- Stable `latest` 3.10.2 (published 2026-07-10): `dependencies.mermaid: ">=11.6.0"`, peer `@mermaid-js/layout-elk ^0.1.9`, `@docusaurus/*` 3.10.2. The installed Mermaid is whatever the site's package manager resolves (likely 12.0.0 on a fresh install, but see the ESM/`layout-elk` caveats in the v12 report). https://registry.npmjs.org/@docusaurus/theme-mermaid
- `main` (v4.0.0, not published: npm dist-tags `canary: 4.0.0-canary-6822`): `"mermaid": "^12.0.0"`, Node `>=24.21`. Commit "break(theme-mermaid): upgrade to Mermaid 12 (#12454)" 2026-09-17. https://github.com/facebook/docusaurus/blob/main/packages/docusaurus-theme-mermaid/package.json
- v12: 3.x - only with a v12 install and ELK-peer handling; 4.0 (unreleased) - yes. Beta types render only where the resolved mermaid is >= the version that introduced them.

### MkDocs Material
- Mermaid is loaded client-side from a CDN: `watchScript("https://unpkg.com/mermaid@11/dist/mermaid.min.js")` in `src/templates/assets/javascripts/components/content/mermaid/index.ts` (`master`, 9.7.7 published 2026-07-17). unpkg resolves `@11` to the newest 11.x, so v12 will not be picked up without a code change (or `extra_javascript` override). https://github.com/squidfunk/mkdocs-material/blob/master/src/templates/assets/javascripts/components/content/mermaid/index.ts ; docs https://squidfunk.github.io/mkdocs-material/reference/diagrams/ (source `docs/reference/diagrams.md`, native support since 8.2.0). Docs: only flowcharts, sequence, class, state and ER diagrams get font/colour adjustment; other types "should still work as advertised by Mermaid.js".
- v12: no (pinned to 11 by URL). `usecase-beta`/`agentflow-beta` fail.

### Notion
- Help center says Mermaid is supported in code blocks with Preview/Split modes (https://www.notion.com/help/code-blocks); introduced December 2021 (https://www.notion.com/releases/2021-12-23). No version stated anywhere found. Enhanced Markdown guide (https://developers.notion.com/guides/data-apis/enhanced-markdown) surfaced in search but was not checked for a version.
- v12: unknown.

### Mermaid Live Editor (mermaid.live)
- `mermaid-js/mermaid-live-editor` `master` `package.json` (app 2.0.67): `"mermaid": "^12.0.0"`, `"@mermaid-js/layout-tidy-tree": "^1.0.0"`, `"@mermaid-js/mermaid-zenuml": "^1.0.0"`. `master` was updated by merge of "sidv/update-mermaid-12" (PR #2029) then "release-promotion" (#2030) on 2026-09-14. `develop` moved to `^1.0.1` companion packages on 2026-09-18. https://github.com/mermaid-js/mermaid-live-editor/blob/master/package.json
- Deploy workflow (`.github/workflows/deploy.yml`) triggers on push to `master`. I could not confirm a completed deploy run, and the site itself displays no version (fetched mermaid.live). A newer "Release live editor" PR (#2040) is open.
- v12: yes per source; expect `usecase-beta`, `agentflow-beta`, `railroad-*-beta`, neo/redux-color and ELK.

### Mermaid Chart (mermaid.ai)
- No first-party statement of the Mermaid version. The blog index (https://mermaid.ai/blog/posts/page/1) has no v12 post as of the fetch; latest Mermaid release post is v11.13.0 (2026-04-20), newest entry "Mermaid Flow Is in Public Beta" (2026-09-03). The VS Code extension `package.json` lists no direct mermaid dependency (v12 report). The Mermaid Chart connector in this session needs OAuth and could not be inspected.
- v12: unknown.

## Beta types known not to render (summary)

| Type | Fails in (evidence) |
|---|---|
| `usecase-beta`, `agentflow-beta` (12.0.0) | GitLab (11.16.1), Obsidian (11.13.0), VS Code preview (11.17.0), MkDocs Material (unpkg `@11`), Docusaurus 3.x on Mermaid 11, Azure DevOps, Bitbucket. Version arithmetic: these types do not exist before 12.0.0 (v12 report). |
| `railroad-*-beta` (11.16.0), `treeView-beta` | Obsidian 11.13.0 *(inference)*, Azure DevOps (not in supported list). Should render in GitLab 19.4 / VS Code (>= 11.16) *(inference; GitLab docs confirm treeView-beta)*. |
| Any `*-beta` | Bitbucket Cloud Markdown (no Mermaid), Azure DevOps (only the 11 listed types). |
| Unknown | GitHub, Notion, Mermaid Chart (versions unstated; probe with `info` on GitHub). |

## Caveats
- GitLab 19.4 date is the tag commit date from the GitLab API, not a release-announcement date.
- VS Code: version at the 1.138.0 tag not separately verified; `main` used.
- Live Editor: source version verified, deployed bundle not verified.
- Azure DevOps versions are simply not documented; a "limited syntax support" statement is the only signal.
