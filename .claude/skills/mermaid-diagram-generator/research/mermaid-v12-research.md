# Mermaid v12 — research notes

Compiled 2026-09-19 to inform an update of the local skill `.claude/skills/mermaid-diagram-generator`
(currently pinned to Mermaid **11.16.1**). Every claim is cited to a primary source (mermaid-js GitHub
releases, the repo's docs/schema/changeset files, npm registry metadata, first-party vendor docs).
Where a source is not first-party it is flagged. Release-note quotes come from
`gh release view <tag> -R mermaid-js/mermaid`; docs quotes from the repo's `packages/mermaid/src/docs/`
tree (the source of https://mermaid.ai/open-source/), read on `develop`, which equals 12.0.0 plus
unreleased changesets (see §6).

Short labels used below: **REL12** = https://github.com/mermaid-js/mermaid/releases/tag/mermaid%4012.0.0 ;
**TINY12** = https://github.com/mermaid-js/mermaid/releases/tag/%40mermaid-js/tiny%4012.0.0 (its notes
carry the full 12.0.0 changelog, including entries that the REL12 body omits — see §6);
**DOCS** = https://github.com/mermaid-js/mermaid/tree/develop/packages/mermaid/src/docs .

---

## 1. Version facts

| Fact | Value | Source |
|---|---|---|
| Mermaid 12.0.0 published | 2026-09-10T07:33:03Z (npm `latest` = 12.0.0; no 12.0.x/12.1 exists yet) | REL12; `npm view mermaid dist-tags` -> `latest: 12.0.0` (https://www.npmjs.com/package/mermaid) |
| Only v12.x release of `mermaid` | 12.0.0 (there are no other v12.x notes to read) | https://github.com/mermaid-js/mermaid/releases |
| Companion v12-line packages, same day (2026-09-10) | `@mermaid-js/parser@2.0.0`, `@mermaid-js/layout-elk@1.0.0`, `@mermaid-js/layout-tidy-tree@1.0.0`, `@mermaid-js/mermaid-zenuml@1.0.0`, `@mermaid-js/tiny@12.0.0`, `@mermaid-js/examples@2.0.0` | https://github.com/mermaid-js/mermaid/releases (tags `@mermaid-js/*`) |
| Follow-up patches, 2026-09-18 | `@mermaid-js/layout-tidy-tree@1.0.1` ("Support Mermaid 12 in the tidy-tree layout package while retaining Mermaid 11 compatibility") and `@mermaid-js/mermaid-zenuml@1.0.1` ("Support Mermaid 12 in the ZenUML plugin while retaining Mermaid 10 and 11 compatibility") | https://github.com/mermaid-js/mermaid/releases/tag/%40mermaid-js/mermaid-zenuml%401.0.1 and .../%40mermaid-js/layout-tidy-tree%401.0.1 |
| v11.x newer than the skill (11.16.1, 2026-08-04) | 11.17.0 (2026-08-19), 11.17.1 (2026-08-24), 11.17.2 (2026-08-25) | https://github.com/mermaid-js/mermaid/releases/tag/mermaid%4011.17.0 , `%4011.17.1`, `%4011.17.2` |
| Skill target version | **11.16.1** everywhere |  see quotes below |

Where the skill states its target (all local files):

- `SKILL.md` frontmatter: `mermaid_version: 11.16.1`; body line 14: "Generates any Mermaid diagram type against **Mermaid v11.16.1** syntax."
- `README.md` line 9: "targeting Mermaid v11.16.1"; line 72: "v11.16.1 - structure checks, grammar validation, and full rendering."
- `tools/README.md` line 3: "pinned to **Mermaid v11.16.1**"; `tools/validate-mermaid.mjs` reads the pin from `SKILL.md` (`pinned` regexp, ~line 551) and installs `mermaid@<pin>` (line 591).
- Every `references/<slug>.md` carries `mermaid_version_verified`, which `versionChecks()` (validator ~line 310) requires to equal the SKILL.md pin — bumping the pin means editing all 30 reference files.

---

## 2. Breaking changes in 12.0.0

Source for all rows unless noted: REL12 "Major Changes" (https://github.com/mermaid-js/mermaid/releases/tag/mermaid%4012.0.0).

| Area | Change | Detail / source |
|---|---|---|
| Runtime floor | ES2024, Safari 17.4+, Node 22.12+ | PR #8213: "Mermaid is now built to target Safari 17.4+ and ES2024. If you need to support older browsers, you may need to polyfill or transpile mermaid." Node 22.12+ is "declared as requirement in our `package.json` files". Confirmed by `npm view mermaid@12.0.0 engines` -> `node >=22.12.0`, `type: module`; and https://github.com/mermaid-js/mermaid/blob/develop/packages/mermaid/src/docs/config/usage.md ("Node.js >= 22.12.0"; linting for "Chromium 121 and Firefox 123" but "we don't commit to supporting these outdated versions"). |
| Packaging | No ESM-only announcement in the notes. `package.json` on develop is `"type": "module"`, `exports["."]` has only `import`/`default` -> `./dist/mermaid.core.mjs`. Whether v11 differed is **not stated** (see §6). Published dist files at 12.0.0: `mermaid.core.mjs`, `mermaid.esm.mjs`, `mermaid.esm.min.mjs`, `mermaid.js`, `mermaid.min.js` (jsDelivr file listing https://data.jsdelivr.com/v1/packages/npm/mermaid@12.0.0?structure=flat). | https://github.com/mermaid-js/mermaid/blob/develop/packages/mermaid/package.json |
| Packaging gotcha | `dist/mermaid.esm.min.mjs` now contains syntax that `es-module-lexer` (Vite) rejects ("content contains invalid JS syntax"); import the `mermaid` package specifier (core build) instead. The single-file IIFE `mermaid.min.js` inlines ELK and "grows by roughly 500 kB gzipped". | REL12 (PR #8155) |
| Default layout | **ELK is bundled and is the default layout** (was dagre). Flowchart, state, class, ER, requirement, use-case (and agentflow) re-lay out. Restore old: `layout: dagre` in frontmatter `config:` or `mermaid.initialize({ layout: 'dagre' })`. Mindmap unchanged (cose-bilkent). | REL12 (PR #8155); https://github.com/mermaid-js/mermaid/blob/develop/packages/mermaid/src/docs/config/layouts.md ("`elk` is the new default layout for these diagrams: Flowchart, State, Class, Entity relationship, Requirement, Use case, Agentflow"); schema `layout: default: 'elk'` (https://github.com/mermaid-js/mermaid/blob/develop/packages/mermaid/src/schemas/config.schema.yaml) |
| Default look/theme | **`redux-color` theme + `neo` look** are now the defaults for ten diagram types: flowchart, swimlane, class, ER, requirement, sequence, state, use case, Venn, agentflow. All other types keep `default` + `classic`. Restore: `mermaid.initialize({ layout: 'dagre', theme: 'default', look: 'classic' })` or the same three keys in frontmatter (frontmatter always wins). | REL12 closing section "Keeping the previous defaults in your integration"; TINY12 entry PR #8148 (`8603bdd`); https://github.com/mermaid-js/mermaid/blob/develop/packages/mermaid/src/docs/config/theming.md#per-diagram-defaults |
| Config key removed | `flowchart.defaultRenderer`, `class.defaultRenderer`, `state.defaultRenderer` **removed**. Now ignored ("ignored rather than rejected, so nothing throws"). Use top-level `layout`. Legacy diagram ids `flowchart`, `class`, `state` gone; `detectType` returns `flowchart-v2`, `classDiagram`, `stateDiagram`. `flowchart-elk` keyword still works. | REL12 (PR #8211); flowchart docs https://github.com/mermaid-js/mermaid/blob/develop/packages/mermaid/src/docs/syntax/flowchart.md ("was removed in v12") |
| Public API removed | `clearLayoutRenderState`, `createCommonLayoutRenderer`, `defaultMeasureLayout`, `paintLayoutData`, `CommonLayout*` types removed from the public API. | REL12 (PR #8223). NB: a later docs commit says they were instead moved to `__internalsDoNotUse` (outside SemVer) — see §6. |
| Theme name resolution | Unrecognised `theme` name now resolves to `default` in name as well as variables; `theme: 'null'` unaffected. `neo` paints node strokes with a gradient when the theme sets `useGradient` (which `base` does); a custom `nodeBorder` on `base` turns it off. | REL12 (PR #8155/#8148 text) |
| Visual output changes (not API, but affects any golden/screenshot tests) | ELK cluster labels measured unwrapped (subgraph sizes change); `elk.preset: default` now = balanced Brandes-Koepf + depth-first cycle breaking ("Some diagrams may become wider or taller; `depthFirst` retains the previous default layout"); `intersectPolygon` fix moves attachments up to 1px in every layout incl. dagre; line hops dropped near bends (also affects swimlane); hand-drawn hachure fill tightened; circle/double-circle/Delay/Display sized from label height; stadium/circle/diamond/etc. no longer wrap at `flowchart.wrappingWidth`. | REL12 patch changes (#8073, #8231, #8152, #8211, #8232, #8227) |
| `mermaid.parse` / `mermaid.render` API | **No signature change announced in the 12.0.0 notes.** usage.md documents `mermaid.parse(text, parseOptions)` -> `{ diagramType }` or `false` with `suppressErrors`, `mermaid.render(id, definition)` -> `{ svg, bindFunctions }`, `mermaid.detectType`. Related earlier deprecation: `mermaidAPI.setConfig()` deprecated in 11.16.1 ("Calling this function has no observable effect, as the next time a `render()` or `parse()` is called, the `currentConfig` is cleared"). | https://github.com/mermaid-js/mermaid/blob/develop/packages/mermaid/src/docs/config/usage.md ; https://github.com/mermaid-js/mermaid/releases/tag/mermaid%4011.16.1 |
| Parser package | `@mermaid-js/parser` 1.2.1 -> **2.0.0** (major only for the ES2024/Node 22.12 floor) | https://github.com/mermaid-js/mermaid/releases/tag/%40mermaid-js/parser%402.0.0 |
| mermaid-cli | Latest `@mermaid-js/mermaid-cli` is **11.17.0** (2026-09-02), depends on `mermaid ^11.14.0`, `@mermaid-js/layout-elk ^0.1.5 \|\| ^0.2.0`, `puppeteer ^25`, Node `>=22.13.0`. So it **renders with Mermaid 11.x, not 12**, and cannot render `usecase-beta`/`agentflow-beta`. PR "Upgrade to Mermaid v12.0.0" (#1182) is **open** as of 2026-09-18; dependabot PRs for layout-elk 1.0.0 (#1178) and layout-tidy-tree 1.0.1 (#1175) also open. The docs page for mermaid CLI now just says it "has been moved to mermaid-cli". | https://github.com/mermaid-js/mermaid-cli/releases/tag/11.17.0 ; https://github.com/mermaid-js/mermaid-cli/blob/master/package.json ; https://github.com/mermaid-js/mermaid-cli/pull/1182 ; https://github.com/mermaid-js/mermaid/blob/develop/packages/mermaid/src/docs/config/mermaidCLI.md |
| Dev-workflow gotcha | mermaid-cli README: "the NodeJS API is not covered by semver, as `mermaid-cli` follows `mermaid`'s versioning". | https://github.com/mermaid-js/mermaid-cli/blob/master/README.md |
| Plugins | ZenUML plugin **1.0.1** is the first with an explicit Mermaid-12 statement; its peer range is `mermaid ^10 \|\| ^11 \|\| ^12` (`npm view @mermaid-js/mermaid-zenuml@1.0.1 peerDependencies`), Node `>=22.12.0`. tidy-tree 1.0.1 likewise. The old `mermaid.registerExternalDiagrams` plugin flow is not mentioned as changed. | ZenUML/tidy-tree release links in §1 |

### Implications for the skill's own tooling (local files)

- `tools/validate-mermaid.mjs` `pickMermaidEsm()` candidates (`dist/mermaid.core.mjs`, ...) and `pickMermaidUmd()` (`dist/mermaid.min.js`) all still exist in 12.0.0 (jsDelivr listing above), so the file lookups keep working.
- The validator hard-codes `@mermaid-js/mermaid-zenuml@0.2.3` (line 591); 12.0.0 needs 1.0.1 per the release note above.
- `EXPECTED_DIAGRAM_FILES = 30` (line 29) will fail as soon as reference files for new types are added.
- `tools/README.md` line 9 says "Node.js 18+"; mermaid 12 declares `engines.node >=22.12.0` (only an npm warning unless `engine-strict`, but the doc is stale).
- `SKILL.md` self-check item 8 says GitLab is a "Mermaid v10 pin". GitLab's current docs say "GitLab supports Mermaid version 11" (https://docs.gitlab.com/user/markdown/), so this claim looks stale (see §5/§6).
- `references/general/layout.md` (lines 9, 18, 37) says dagre is the default and that ELK is a separate package that must be registered; both are now wrong for v12 (§3/§4).

---

## 3. New diagram types and new syntax/config, mapped to the skill

### 3a. Diagram types absent from the skill's list

The skill claims 30 types (SKILL.md description + `EXPECTED_DIAGRAM_FILES = 30`). Three are missing:

| New/missing type | Keyword | Since | Status | Source | Skill file that should exist |
|---|---|---|---|---|---|
| **Use case** (UML: actors, use cases, system boundaries, `include`/`extend`, stereotypes, notes, business variants) | `usecase-beta` (docs: "Mermaid's exact syntax keyword is the single token `usecase-beta`") | 12.0.0 | beta | REL12 (PR #8048); https://github.com/mermaid-js/mermaid/blob/develop/packages/mermaid/src/docs/syntax/usecase.md | `references/usecase.md` (missing). Skill only mentions "use case" in the Event Modeling row. |
| **Agentflow** (agents, `flow ... end` containers, `task`/`tool`/`decision`/`input`/`refdoc`/`connector`/`action` shapes, `@{ ... }` metadata) | `agentflow-beta` | 12.0.0 | beta ("syntax may still change in a backwards-incompatible way") | TINY12 entry PR #8073/`0cf3797`; https://github.com/mermaid-js/mermaid/blob/develop/packages/mermaid/src/docs/syntax/agentflow.md | `references/agentflow.md` (missing) |
| **Railroad / syntax diagrams** | `railroad-beta` (IR), `railroad-ebnf-beta`, `railroad-abnf-beta`, `railroad-peg-beta` | **11.16.0** (already older than the skill's 11.16.1 pin — the skill missed it) | beta | https://github.com/mermaid-js/mermaid/releases/tag/mermaid%4011.16.0 (PR #7251); https://github.com/mermaid-js/mermaid/blob/develop/packages/mermaid/src/docs/syntax/railroad.md | `references/railroad.md` (missing) |

Note REL12's intro says "two new diagram types" but links only use case; the second is agentflow (§6).
The DOCS `syntax/` folder now lists 33 syntax pages: agentflow, architecture, block, c4, classDiagram, cynefin, entityRelationshipDiagram, eventmodeling, flowchart, gantt, gitgraph, ishikawa, kanban, mindmap, packet, pie, quadrantChart, radar, railroad, requirementDiagram, sankey, sequenceDiagram, stateDiagram, swimlanes, timeline, treeView, treemap, usecase, userJourney, venn, wardley, xyChart, zenuml (https://github.com/mermaid-js/mermaid/tree/develop/packages/mermaid/src/docs/syntax). Everything else in the skill's list still has a docs page.

### 3b. New syntax/config per existing type (11.17.x and 12.0.0; 11.16.0 items shown only where the skill lacks them)

| Skill reference file | New / changed | Version | Source |
|---|---|---|---|
| `flowchart.md` | Collapsible subgraphs: `subgraphId@{ view: collapsed }` | 11.17.0 | PR #7785 (REL 11.17.0); docs flowchart.md "Collapsible subgraphs (v11.17.0+)" |
| `flowchart.md` | New shapes `folder`, `bucket`, `console` (terminal window), `browser`, and `person` (`A@{ shape: person }`) | 11.17.0 | PRs #7970, #7842 (REL 11.17.0). The DOCS `flowchart.md` has **no** section for these shapes (rg found none) — release-note only. |
| `flowchart.md` | `flowchart`/`state`/`usecase`/`agentflow` gain `wrappingWidth` (default 120) and `minNodeWidth` (default 120) config; stadium/circle/diamond/double-circle/Display/Delay exempt from wrapping | 12.0.0 | TINY12 entry PR #8211/`a31ecb7`; REL12 PR #8227 |
| `flowchart.md`, `class.md`, `state.md`, `entity-relationship.md`, `requirement.md` | Default `theme`/`look`/`layout` changed (see §4); `defaultRenderer` removed; ELK per-container `@{ algorithm: elk.box }` on subgraphs | 12.0.0 | REL12 |
| `class.md` | `classDiagram` routed to the unified renderer by default (11.17.0); relation markers no longer scale with stroke width (11.17.1); per-class palette colour under redux-color; the legacy `class: { defaultRenderer: 'dagre-d3' }` opt-out mentioned in 11.17.0 was then removed in 12.0.0 | 11.17.0, 11.17.1, 12.0.0 | RELs 11.17.0, 11.17.1, 12.0.0 |
| `state.md` | Composite-state palette colours under redux-color; handDrawn concurrency regions now solid | 12.0.0 | REL12 (PR #8191) |
| `entity-relationship.md` | Subgraph support in ER diagrams (11.17.0); `?` optional attribute types and backtick-escaped special characters in names/types were 11.16.0 (skill already documents `?`) | 11.17.0 | REL 11.17.0 (PR #7792); docs ER "Subgraphs (v11.17.0+)" |
| `xy-chart.md` | Legends for named line/bar series (`legend`) | 11.17.0 | PR #7724 (REL 11.17.0); docs xyChart.md "Legend (v11.17.0+)". Per-point labels and rotate-x-labels were 11.16.0 (skill has the first). |
| `c4.md` | C4 elements rendered via the unified shape system (`person` shape); wrapping now gated on `c4.wrap` (fix in 11.17.1); boundaries usable as `Rel` endpoints; `$tags`/`$sprite` positional-slot fixes | 11.17.0-12.0.0 | RELs 11.17.0/11.17.1/12.0.0 (PRs #7842, #8092, #7874, #8100) |
| `swimlanes.md` | `layout` (e.g. `swimlane: { layout: ... }` or frontmatter `layout`) now honoured; per-lane palette under redux; line hops dropped near bends; cycle removal fix | 12.0.0 | REL12 (PRs #8193, #8176, #8152, #8225) |
| `sequence.md` | Default theme/look changed (redux-color + neo); layout of actors under neo/redux normalised; `</br>` now accepted as line break in labels (all types) | 12.0.0 | https://github.com/mermaid-js/mermaid/blob/develop/packages/mermaid/src/docs/syntax/sequenceDiagram.md ; REL12 (PRs #8185, #8048) |
| `venn.md` | Default theme/look changed; venn circles follow redux palettes | 12.0.0 | docs venn.md; REL12 (PR #8189) |
| `pie.md`, `architecture.md`, `treeview.md`, `cynefin.md`, `gantt.md`, `swimlanes.md` | All 11.16.0 features (donut/legend position/highlight slice; `align row\|column`; treeView box-drawing input; cynefin; multiple `excludes`; swimlane type) — the skill already covers these; no v12-specific change found | 11.16.0 | REL 11.16.0 |
| `treeview.md` | icons no longer vanish after strict sanitisation | 11.17.0 | PR #7924 |
| `architecture.md` | non-ASCII / punctuation now allowed in unquoted titles (parser 1.2.1) | 11.17.0 | https://github.com/mermaid-js/mermaid/releases/tag/%40mermaid-js/parser%401.2.1 |
| `packet.md` | `bitOrder: ascending\|descending` config — **unreleased** (pending changeset on `develop`, not in 12.0.0) | after 12.0.0 | https://github.com/mermaid-js/mermaid/blob/develop/.changeset/packet-bit-order.md ; schema `bitOrder` |
| `sankey.md`, `treemap.md`, `block.md`, `kanban.md`, `mindmap.md`, `timeline.md`, `user-journey.md`, `quadrant.md`, `radar.md`, `ishikawa.md`, `wardley.md`, `event-modeling.md`, `requirement.md`, `gitgraph.md`, `zenuml.md` | No new syntax found in the 11.17/12.0.0 notes. Bug fixes only (e.g. block: sibling overlap, classDef text colour, gradient borders under `look: neo`, palette on composite blocks). Mindmap: default layout unchanged (cose-bilkent); `layout: tidy-tree` from `@mermaid-js/layout-tidy-tree` (docs: "primarily supported for mindmap"). | 11.17/12.0.0 | RELs; https://github.com/mermaid-js/mermaid/blob/develop/packages/mermaid/src/docs/config/tidy-tree.md |
| `zenuml.md` | Plugin 1.0.0/1.0.1 for Mermaid 12 (see §1/§2) | 12 | ZenUML releases |

Also new on the config side (all 12.0.0): `elk.preset` = `default | legacy | modelOrder | depthFirst`; `elk.nodePlacementAlignment` and `elk.keepEntryNodeOnTop` (11.17.0); ELK variants `elk.stress`, `elk.force`, `elk.mrtree`, `elk.sporeOverlap`, `elk.box`, `elk.rectpacking`; usecase theme variables `usecaseActorBkg/Border`, `usecaseBkg/Border`, `usecaseBoundaryBkg/Border`, `usecaseIncludeLine`, `usecaseExtendLine`, and `usecase.colorScheme: 'rotate'` (REL12 PR #8178; TINY12 for `elk.preset` and #8152; schema config.schema.yaml `elk:`).

---

## 4. Deprecations, removals, renamed themes/config, layout engine and look

- **Themes.** Skill's `references/general/theming.md` line 7 says "five named themes: default, neutral, dark, forest, base". The v12 schema enum is `default, base, dark, forest, neutral, neo, neo-dark, redux, redux-dark, redux-color, redux-dark-color, 'null'` (https://github.com/mermaid-js/mermaid/blob/develop/packages/mermaid/src/schemas/config.schema.yaml, `BaseDiagramConfig.properties.theme`; docs list 11 named themes: https://github.com/mermaid-js/mermaid/blob/develop/packages/mermaid/src/docs/config/theming.md). `redux-color` is the per-diagram default for ten types. `base` remains the only customisable theme. No theme was renamed or removed; `redux*`/`neo*` themes existed before 12 (11.17.1 notes already reference `redux`, `neo`, `neo-dark` strokeWidth 2) — they became defaults in 12.
- **Look.** Enum is `classic | handDrawn | neo` (schema). Global schema default remains `look: classic`, but ten types default to `neo` (§2). Skill mentions `handDrawn` only in `cynefin.md`; `look` is not documented in `references/general/`.
- **Per-diagram scoping (new).** `theme`, `look` and `layout` can be set per diagram type: `mermaid.initialize({ look: 'classic', flowchart: { look: 'handDrawn' }, er: { theme: 'neutral' } })` or the same under `config:` frontmatter. Resolution, highest first: frontmatter/directive, `initialize()`, diagram-type default, global default; a diagram-scoped value beats a global one in the same layer. Config keys are not always the diagram keyword: `agentflow`, `flowchart`, `swimlane`, `class`, `er`, `requirement`, `sequence`, `state`, `usecase`, `venn` (REL12 PR #8193; theming.md "Per-diagram defaults").
- **Layout.** Available: `elk` (default, bundled), `dagre`, `cose-bilkent`, `tidy-tree` (separate package), plus the ELK variants above (layouts.md). Unregistered layout now always falls back to `dagre` with a console warning instead of throwing (REL12 PR #8193). Tiny build omits ELK and falls back to dagre; `@mermaid-js/layout-elk@1.0.0` is "only needed for Mermaid builds that ship without ELK", and existing `mermaid.registerLayoutLoaders(elkLayouts)` calls "keep working and can be removed" (REL12; TINY12 `810893c`). ELK also gets new ID support for per-container `@{ algorithm: ... }`.
- **Removed:** `defaultRenderer` (flowchart/class/state), legacy diagram ids `flowchart`/`class`/`state`, layout-internal public exports (§2). **Deprecated (earlier):** `mermaidAPI.setConfig()` in 11.16.1. **Fixed alias:** `flowchart-elk` "still works, but is no longer needed" (flowchart.md).
- **Pre-existing skill text that is now wrong:** `references/general/layout.md` (dagre = long-standing default; ELK requires separate package + registration; "four algorithms"); `references/general/theming.md` ("five named themes"); `SKILL.md` "Choosing a diagram type" (no use case / agentflow / railroad rows), `SKILL.md` line 3 description, and Beta/Experimental badges (see below).
- **Status badges to re-verify.** DOCS mark `usecase-beta`, `agentflow-beta` and all `railroad-*-beta` as beta. The skill lists Entity Relationship, C4 and Event Modeling as "experimental"; the current docs give ER, C4 and Event Modeling no such label in the sources I opened, and 11.17.0 moved C4 onto the unified renderer — worth re-checking each page's banner (not verified in this pass; see §6).

---

## 5. Ecosystem (from https://mermaid.ai/open-source/ecosystem/integrations-community.html)

Fetched live (https://mermaid.ai/open-source/ecosystem/integrations-community.html) and cross-checked against the source file https://github.com/mermaid-js/mermaid/blob/develop/packages/mermaid/src/docs/ecosystem/integrations-community.md . The page groups entries under Productivity tools, LLM integrations, CRM/ERP, Blogging, CMS/ECM, Communication, Wikis, Editor Plugins, Document Generation, Browser Extensions, Other. A check mark means "native Mermaid support on the platform". The page itself states no Mermaid versions. First-party is exactly one entry: **Mermaid Chart** ("built by the team behind Mermaid JS"; https://mermaid.ai). Everything else is community/third-party, including native platform support.

Relevance to a diagram-authoring/validation skill, with version evidence gathered from each project's own repo/registry:

| Tool | Party | Role | Mermaid version it ships / v12 support | Source |
|---|---|---|---|---|
| **mermaid-cli** (`mmdc`, `@mermaid-js/mermaid-cli`) | first-party (mermaid-js org; the integrations page just points to it) | CLI render/validate | 11.17.0 uses `mermaid ^11.14.0` — **no v12 yet**; upgrade PR #1182 open | https://github.com/mermaid-js/mermaid-cli (package.json, PR #1182) |
| **Mermaid Live Editor** | first-party (mermaid-js org) | Browser editor/validator | `package.json`: `mermaid ^12.0.0`, `@mermaid-js/layout-tidy-tree ^1.0.1`, `mermaid-zenuml ^1.0.1`, `examples ^2.0.0` — **v12** | https://github.com/mermaid-js/mermaid-live-editor (package.json) |
| **Mermaid Chart** VS Code extension / platform | first-party (Mermaid Chart org) | Editor + MCP-era tooling | extension `2.7.8`; its `package.json` lists no direct `mermaid` dependency (bundled version not determinable) | https://github.com/Mermaid-Chart/vscode-mermaid-chart |
| **GitHub** (native ✅) | community/platform | renders ```mermaid in Markdown | GitHub docs do not state the version; tell users to run `info` in a mermaid block to see it | https://docs.github.com/en/get-started/writing-on-github/working-with-advanced-formatting/creating-diagrams |
| **GitLab** (native ✅) | platform | renders ```mermaid | "GitLab supports Mermaid version 11"; `treeView-beta` support added in GitLab 19.4; **not v12** | https://docs.gitlab.com/user/markdown/ |
| **Obsidian** (native ✅) | platform | renders ```mermaid | Obsidian's syntax help states no Mermaid version | https://obsidian.md/help/syntax |
| VS Code "Markdown Preview Mermaid Support" (`bierner.markdown-mermaid`, repo mjbvz/vscode-markdown-mermaid) | community | preview | v1.32.1 depends on `mermaid ^11.12.2` — no v12 | https://github.com/mjbvz/vscode-markdown-mermaid (package.json) |
| **MCP Server Mermaid** (`hustcc/mcp-mermaid`) | community | LLM tool: generate + render | 0.4.1 (2026-02-12) renders through `mermaid-isomorphic ^3.0.4` | https://github.com/hustcc/mcp-mermaid |
| `mermaid-isomorphic` / `rehype-mermaid` / `remark-mermaidjs` (remcohaszing) | community | Node-side rendering via Playwright | `mermaid-isomorphic` 3.1.0 has peer `mermaid ^11.0.0` — not declared for 12 | https://github.com/remcohaszing/mermaid-isomorphic (package.json) |
| Mermaid Studio (JetBrains plugin + MCP) | community (listed under both editors and LLM integrations) | code intelligence + generation | not checked | https://mermaidstudio.dev ; https://plugins.jetbrains.com/plugin/29870-mermaid-studio |
| Docusaurus, Quarto, Typora, Notion, Azure DevOps, Gitea/Forgejo, Slidev, Joplin (all ✅), mkdocs-material, mdBook, Sphinx, VitePress plugin etc. | community/platform | doc-site rendering | per-project; not surveyed individually | the integrations page |
| Mermaid Chart MCP server | first-party (claude.ai connector "Mermaid Chart") | — | this session's MCP server needs OAuth and is unauthorised, so it could not be inspected | — |

Practical read: as of 2026-09-19, **only the Live Editor (and mermaid.js.org/mermaid.ai docs) run v12**. mermaid-cli, GitLab, and the popular VS Code preview all render **11.x**, so `usecase-beta`/`agentflow-beta` will fail to parse in them, and 12's redux/neo defaults will not appear in them. GitHub's version is unknown until probed with an `info` block. Anything a skill emits for v12-only syntax should carry an explicit version gate like the skill already does for 11.16.0 features.

---

## 6. Open questions and source conflicts

1. **"Two new diagram types" vs one linked.** REL12's intro says "two new diagram types: [UML use case diagrams](...)" and links only use case. The changelog (TINY12 PR #8073) and docs (`agentflow.md`, "Agentflow (v12.0.0+)") add `agentflow-beta`. Assumed the second type is agentflow. Also the REL12 body itself omits the PR #8148 (redux-color/neo default) and PR #8073 (agentflow) entries that TINY12 contains, though its footer describes them; treat TINY12 as the fuller changelog.
2. **Global default layout.** `theming.md` (Per-diagram defaults) says "The global default (`theme: default`, `look: classic`, `layout: dagre`)" and "`elk` ships as a separate package you register yourself" — both contradict `layouts.md`, the release notes, and the schema (`layout: default: 'elk'`). Likely stale prose; the schema/release notes are authoritative. Same page says "the nine types above" but tabulates ten.
3. **Layout internal exports.** REL12 says the `createCommonLayoutRenderer` etc. exports were removed; a later docs commit (2026-09-07, "docs: cover ELK bundling and tiny, and move layout internals behind `__internalsDoNotUse`") says they moved to `__internalsDoNotUse`, outside SemVer. Which is in the published 12.0.0 tarball was not verified.
4. **ESM-only.** No source calls out "ESM-only" as new in v12. `type: module`, and `exports` import-only, are visible in 12.0.0 package metadata, but I did not diff against 11.x, so I cannot say whether that is new.
5. **`mermaid.parse`/`render` semantic changes.** None announced; only the 11.16.1 `setConfig` deprecation. The validator's use of `mermaid.parse` under jsdom with ELK now lazily bundled was not run against 12.0.0 in this pass (no Node execution of the skill's validator).
6. **New flowchart shapes documentation.** `folder`/`bucket`/`console`/`browser`/`person` exist per 11.17.0 notes but are absent from the develop docs page; exact accepted short names/aliases unverified.
7. **Status of ER / C4 / Event Modeling.** Skill labels them experimental; whether that still matches the current docs banners was not checked.
8. **GitLab/GitHub/Obsidian versions.** GitLab: "version 11" (no minor). GitHub and Obsidian: unstated. `SKILL.md` claims GitLab is v10-pinned — contradicts GitLab's own doc. Not verified whether the skill's claim came from an older GitLab release.
9. **Unreleased changes on `develop`.** Pending changesets (`packet-bit-order` minor, plus sequence hyphenated-actor/config-whitespace and gantt warn patches) are not in 12.0.0 — docs on `develop` may describe `bitOrder` before a release exists. Source: https://github.com/mermaid-js/mermaid/tree/develop/.changeset
10. **ZenUML plugin 1.0.0 vs 1.0.1.** 1.0.1's note says "Support Mermaid 12 in the ZenUML plugin", implying 1.0.0 did not fully; recommend 1.0.1 (not tested).
11. **Mermaid Chart MCP / connector.** Unauthorised in this environment, so first-party tool capabilities and version were not checked.
