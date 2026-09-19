#!/usr/bin/env node
/**
 * validate-mermaid.mjs - replayable verification for the mermaid-diagram-generator skill.
 *
 * Zero dependencies are committed to this repository. Anything the chosen mode needs is
 * installed on demand into a cache directory under the OS temp dir, outside the repo.
 *
 *   node tools/validate-mermaid.mjs                 structure + keyword + version + render
 *   node tools/validate-mermaid.mjs --mode parse    no browser; grammar only
 *   node tools/validate-mermaid.mjs --mode none     structural checks only; installs nothing
 *   node tools/validate-mermaid.mjs --only architecture,treeview
 *   node tools/validate-mermaid.mjs --mermaid-version 11.16.1   check the same blocks against another release
 *   node tools/validate-mermaid.mjs --json
 *   node tools/validate-mermaid.mjs --escaping                    replay the escaping matrix; report drift from the baseline
 *   node tools/validate-mermaid.mjs --clean         delete the temp dependency cache
 *
 * Exit code 0 when everything passed, 1 otherwise.
 */

import { spawnSync } from 'node:child_process';
import crypto from 'node:crypto';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const SKILL_DIR = path.resolve(HERE, '..');
const REFS_DIR = path.join(SKILL_DIR, 'references');
const GENERAL_DIR = path.join(REFS_DIR, 'general');

const EXPECTED_DIAGRAM_FILES = 33;
const EXPECTED_GENERAL_FILES = 10;

// mermaid 12 declares engines.node >=22.12.0; the parse and render modes load it directly.
const MIN_NODE = [22, 12, 0];

const REQUIRED_FRONTMATTER = [
  'diagram',
  'slug',
  'status',
  'mermaid_version_introduced',
  'mermaid_version_verified',
  'keyword',
  'source',
  'last_verified',
  'plugin_required',
];

// Prefix match - "## Escaping & special characters" is matched by "## Escaping".
const REQUIRED_SECTIONS = [
  '## Overview',
  '## Best-fit uses',
  '## When NOT to use this',
  '## Basic syntax',
  '## Simple example',
  '## Complex example',
  '## Escaping',
  '## Common pitfalls',
  '## v11 fallback',
  '## Beta/experimental caveats',
  '## Further reading',
];

// ---------------------------------------------------------------------------
// CLI
// ---------------------------------------------------------------------------

function parseArgs(argv) {
  const opts = { mode: 'render', only: null, json: false, clean: false, verbose: false, mermaidVersion: null, files: [], escaping: false, updateBaseline: false };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === '--mode') opts.mode = argv[++i];
    else if (a.startsWith('--mode=')) opts.mode = a.slice(7);
    else if (a === '--only') opts.only = argv[++i];
    else if (a.startsWith('--only=')) opts.only = a.slice(7);
    else if (a === '--mermaid-version') opts.mermaidVersion = argv[++i];
    else if (a.startsWith('--mermaid-version=')) opts.mermaidVersion = a.slice(18);
    else if (a === '--files') opts.files.push(...argv[++i].split(',').map((f) => f.trim()).filter(Boolean));
    else if (a.startsWith('--files=')) opts.files.push(...a.slice(8).split(',').map((f) => f.trim()).filter(Boolean));
    else if (a === '--escaping') opts.escaping = true;
    else if (a === '--update-baseline') opts.updateBaseline = true;
    else if (a === '--json') opts.json = true;
    else if (a === '--clean') opts.clean = true;
    else if (a === '--verbose' || a === '-v') opts.verbose = true;
    else if (a === '--help' || a === '-h') opts.help = true;
    else die(`unknown argument: ${a} (try --help)`);
  }
  if (!['render', 'parse', 'none'].includes(opts.mode)) {
    die(`--mode must be one of render|parse|none (got "${opts.mode}")`);
  }
  if (opts.mermaidVersion && !/^\d+\.\d+\.\d+$/.test(opts.mermaidVersion)) {
    die(`--mermaid-version must look like 12.0.0 (got "${opts.mermaidVersion}")`);
  }
  if (opts.only) opts.only = opts.only.split(',').map((s) => s.trim()).filter(Boolean);
  return opts;
}

function die(msg) {
  process.stderr.write(`validate-mermaid: ${msg}\n`);
  process.exit(2);
}

const HELP = `validate-mermaid.mjs - verify every Mermaid example in this skill

  --mode render   (default) headless Chromium; parse + render each block
  --mode parse    jsdom; grammar check only, no browser
  --mode none     structure/keyword/version checks only; installs nothing
  --only a,b      restrict block checks to these file slugs
  --mermaid-version X.Y.Z  install and check against this release instead of the SKILL.md pin
                  (blocks annotated since=/until= run only on the releases they name)
  --files a.md,b.mmd  check the diagram blocks in these files (paths relative to the current directory)
                  instead of the skill's own references; structure/keyword checks are skipped
  --escaping      replay the escaping matrix (every special character in every label position, raw and
                  escaped) on the selected release and report drift from tools/escaping-baseline.json
  --update-baseline  with --escaping: record this release's outcomes as the new baseline
  --json          machine-readable report on stdout
  --clean         remove the temp dependency cache and exit
  --verbose       print PASS lines too, and the npm install output
`;

// ---------------------------------------------------------------------------
// Small helpers
// ---------------------------------------------------------------------------

const C = process.stdout.isTTY
  ? { red: (s) => `\x1b[31m${s}\x1b[0m`, green: (s) => `\x1b[32m${s}\x1b[0m`, yellow: (s) => `\x1b[33m${s}\x1b[0m`, dim: (s) => `\x1b[2m${s}\x1b[0m`, bold: (s) => `\x1b[1m${s}\x1b[0m` }
  : { red: (s) => s, green: (s) => s, yellow: (s) => s, dim: (s) => s, bold: (s) => s };

const rel = (p) => path.relative(SKILL_DIR, p).split(path.sep).join('/');

function readFrontmatter(text) {
  // Flat scalar YAML only - sufficient for these files, and avoids a yaml dependency.
  const lines = text.split(/\r?\n/);
  if (lines[0].trim() !== '---') return null;
  const fm = {};
  for (let i = 1; i < lines.length; i++) {
    if (lines[i].trim() === '---') return fm;
    const m = /^([A-Za-z0-9_-]+):\s*(.*)$/.exec(lines[i]);
    if (m) fm[m[1]] = m[2].trim().replace(/^["']|["']$/g, '');
  }
  return null; // unterminated
}

/**
 * Extract fenced ```mermaid blocks, tracking nested/longer fences so a ```mermaid
 * shown *inside* a ````markdown demonstration fence is not treated as a real block.
 */
function extractBlocks(file, text) {
  const lines = text.split(/\r?\n/);
  const blocks = [];
  let open = null; // { marker, info, startLine, body[] }
  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];
    const fence = /^(\s*)(`{3,}|~{3,})(.*)$/.exec(line);
    if (fence) {
      const marker = fence[2];
      const info = fence[3].trim();
      if (!open) {
        open = { marker, info, startLine: i + 1, body: [] };
        continue;
      }
      // A closing fence must use the same char and be at least as long, with no info string.
      if (marker[0] === open.marker[0] && marker.length >= open.marker.length && info === '') {
        if (open.info.toLowerCase() === 'mermaid') {
          blocks.push({
            file,
            line: open.startLine,
            code: open.body.join('\n'),
            annotation: annotationAbove(lines, open.startLine - 1),
          });
        }
        open = null;
        continue;
      }
    }
    if (open) open.body.push(line);
  }
  return blocks;
}

/**
 * Look upward from a fence for `<!-- mermaid-validate: ... -->`, skipping blank lines.
 * Grammar: an optional bare directive (skip | parse-only | keyword-exempt) followed by
 * key="value" attributes: reason, since (run only on releases >= value), until (run only on
 * releases < value).
 */
function annotationAbove(lines, fenceIdx) {
  for (let i = fenceIdx - 1; i >= 0; i--) {
    const t = lines[i].trim();
    if (t === '') continue;
    const m = /^<!--\s*mermaid-validate:\s*(.*?)\s*-->$/.exec(t);
    if (!m) return null;
    const body = m[1];
    const first = body.split(/\s+/)[0];
    const directive = first.includes('=') ? null : first;
    const attr = (name) => new RegExp(`${name}="([^"]*)"`).exec(body)?.[1] ?? null;
    return { directive, reason: attr('reason'), since: attr('since'), until: attr('until'), raw: t };
  }
  return null;
}

/** Compare dotted numeric versions: negative, zero or positive like a - b. */
function compareVersions(a, b) {
  const pa = a.split('.').map(Number);
  const pb = b.split('.').map(Number);
  for (let i = 0; i < 3; i++) {
    const d = (pa[i] ?? 0) - (pb[i] ?? 0);
    if (d !== 0) return d;
  }
  return 0;
}

/** True when a block's since/until annotation admits the given mermaid release. */
function appliesToVersion(block, version) {
  const a = block.annotation;
  if (a?.since && compareVersions(version, a.since) < 0) return false;
  if (a?.until && compareVersions(version, a.until) >= 0) return false;
  return true;
}

function assertNodeVersion() {
  const have = process.versions.node.split('.').map(Number);
  for (let i = 0; i < 3; i++) {
    if (have[i] > MIN_NODE[i]) return;
    if (have[i] < MIN_NODE[i]) die(`parse and render modes need Node >= ${MIN_NODE.join('.')} (running ${process.versions.node}); --mode none works on any Node`);
  }
}

/** First meaningful line of a diagram: skips config frontmatter, %% comments and %%{init}%%. */
function firstMeaningfulLine(code) {
  const lines = code.split('\n');
  let i = 0;
  while (i < lines.length && lines[i].trim() === '') i++;
  if (lines[i]?.trim() === '---') {
    i++;
    while (i < lines.length && lines[i].trim() !== '---') i++;
    i++; // past the closing ---
  }
  for (; i < lines.length; i++) {
    const t = lines[i].trim();
    if (t === '' || t.startsWith('%%')) continue;
    return t;
  }
  return '';
}

// ---------------------------------------------------------------------------
// Dependency bootstrap (temp dir, outside the repo)
// ---------------------------------------------------------------------------

function cacheDir(pinned) {
  return path.join(os.tmpdir(), `mermaid-validate-${pinned}`);
}

function ensureDeps(pinned, packages, { verbose }) {
  const dir = cacheDir(pinned);
  const missing = packages.filter((p) => {
    const name = p.replace(/@[^@/]+$/, '') || p;
    return !fs.existsSync(path.join(dir, 'node_modules', ...name.split('/'), 'package.json'));
  });
  if (missing.length === 0) return dir;

  fs.mkdirSync(dir, { recursive: true });
  process.stderr.write(
    `${C.dim('installing')} ${missing.join(' ')}\n${C.dim('into')}       ${dir}\n${C.dim('(temp cache outside the repo; remove with --clean)')}\n\n`
  );
  // Create minimal package.json and use npm install in that directory
  const pkgJsonPath = path.join(dir, 'package.json');
  if (!fs.existsSync(pkgJsonPath)) {
    fs.writeFileSync(pkgJsonPath, '{"name":"mermaid-validate","version":"1.0.0","private":true}', 'utf8');
  }
  // Saved (not --no-save): npm prunes anything absent from package.json, so a later install of
  // one mode's packages would otherwise delete the other mode's. The cache is disposable.
  const res = spawnSync('npm', ['install', '--no-audit', '--no-fund', ...missing], {
    cwd: dir,
    stdio: verbose ? 'inherit' : ['ignore', 'ignore', 'inherit'],
    shell: process.platform === 'win32',
  });
  if (res.status !== 0) die(`npm install failed in ${dir}; maybe try: cd "${dir}" && npm install ${missing.join(' ')}`);
  return dir;
}

function resolveInCache(dir, ...segments) {
  const p = path.join(dir, 'node_modules', ...segments);
  if (false && !fs.existsSync(p)) console.error(`resolveInCache: not found: ${p}`);
  return fs.existsSync(p) ? p : null;
}

// ---------------------------------------------------------------------------
// Static checks (no dependencies)
// ---------------------------------------------------------------------------

function structureChecks(files, pinnedFromSkill) {
  const problems = [];

  const diagramFiles = files.filter((f) => f.kind === 'diagram');
  const generalFiles = files.filter((f) => f.kind === 'general');

  if (diagramFiles.length !== EXPECTED_DIAGRAM_FILES) {
    problems.push({
      check: 'structure',
      file: 'references/',
      message: `expected ${EXPECTED_DIAGRAM_FILES} diagram reference files, found ${diagramFiles.length}`,
    });
  }
  if (generalFiles.length !== EXPECTED_GENERAL_FILES) {
    problems.push({
      check: 'structure',
      file: 'references/general/',
      message: `expected ${EXPECTED_GENERAL_FILES} general reference files, found ${generalFiles.length}`,
    });
  }

  for (const f of diagramFiles) {
    if (!f.frontmatter) {
      problems.push({ check: 'structure', file: f.rel, message: 'missing or unterminated YAML frontmatter' });
      continue;
    }
    for (const field of REQUIRED_FRONTMATTER) {
      if (!(field in f.frontmatter)) {
        problems.push({ check: 'structure', file: f.rel, message: `frontmatter missing "${field}"` });
      }
    }
    for (const heading of REQUIRED_SECTIONS) {
      const found = f.text.split(/\r?\n/).some((l) => l.startsWith(heading));
      if (!found) problems.push({ check: 'structure', file: f.rel, message: `missing section "${heading}"` });
    }
    if (f.frontmatter.slug && f.frontmatter.slug !== path.basename(f.path, '.md')) {
      problems.push({
        check: 'structure',
        file: f.rel,
        message: `frontmatter slug "${f.frontmatter.slug}" does not match filename`,
      });
    }
  }

  if (!pinnedFromSkill) {
    problems.push({ check: 'structure', file: 'SKILL.md', message: 'could not read metadata.mermaid_version from frontmatter' });
  }

  return problems;
}

function keywordChecks(files) {
  const problems = [];
  for (const f of files) {
    if (f.kind !== 'diagram' || !f.frontmatter?.keyword) continue;
    const expected = f.frontmatter.keyword;
    for (const b of f.blocks) {
      if (b.annotation?.directive === 'keyword-exempt' || b.annotation?.directive === 'skip') continue;
      const first = firstMeaningfulLine(b.code);
      const token = first.split(/[\s:]/)[0];
      if (token !== expected) {
        problems.push({
          check: 'keyword',
          file: f.rel,
          line: b.line,
          message: `block starts with "${token}" but frontmatter declares keyword "${expected}"`,
          hint: 'fix the example, correct the frontmatter, or annotate the block with <!-- mermaid-validate: keyword-exempt -->',
        });
      }
    }
  }
  return problems;
}

function versionChecks(files, pinnedFromSkill, installedVersion, target) {
  const problems = [];
  for (const f of files) {
    if (f.kind !== 'diagram') continue;
    const v = f.frontmatter?.mermaid_version_verified;
    if (v && pinnedFromSkill && v !== pinnedFromSkill) {
      problems.push({
        check: 'version',
        file: f.rel,
        message: `mermaid_version_verified "${v}" != SKILL.md metadata.mermaid_version "${pinnedFromSkill}"`,
      });
    }
  }
  if (installedVersion && target && installedVersion !== target) {
    problems.push({
      check: 'version',
      file: 'tools/validate-mermaid.mjs',
      message: `installed mermaid ${installedVersion} != requested ${target} - run --clean and re-run`,
    });
  }
  return problems;
}

// ---------------------------------------------------------------------------
// Block validation - parse mode (jsdom) and render mode (puppeteer)
// ---------------------------------------------------------------------------

const ERROR_SVG_MARKERS = [
  'aria-roledescription="error"',
  'Syntax error in text',
  'mermaid version',
];

async function validateParse(blocks, dir) {
  const jsdomPath = resolveInCache(dir, 'jsdom');
  const mermaidDir = resolveInCache(dir, 'mermaid');
  if (!jsdomPath || !mermaidDir) die('parse mode: jsdom/mermaid not present in the dependency cache');

  const { JSDOM } = await import(pathToFileURL(path.join(jsdomPath, 'lib/api.js')).href);
  const dom = new JSDOM('<!doctype html><html><body></body></html>', {
    pretendToBeVisual: true,
    url: 'http://localhost/',
  });
  // location/localStorage/sessionStorage are additionally required by the ZenUML plugin's
  // internal renderer (@zenuml/core) - core mermaid diagram types don't touch them, so this
  // list stays a superset of what any single diagram type needs.
  for (const k of ['window', 'document', 'navigator', 'location', 'localStorage', 'sessionStorage', 'HTMLElement', 'SVGElement', 'Element', 'Node', 'DOMParser', 'MutationObserver', 'getComputedStyle', 'requestAnimationFrame', 'cancelAnimationFrame']) {
    if (globalThis[k] === undefined && dom.window[k] !== undefined) {
      globalThis[k] = typeof dom.window[k] === 'function' && /^(getComputedStyle|requestAnimationFrame|cancelAnimationFrame)$/.test(k)
        ? dom.window[k].bind(dom.window)
        : dom.window[k];
    }
  }

  const entry = pickMermaidEsm(mermaidDir);
  const mod = await import(pathToFileURL(entry).href);
  const mermaid = mod.default ?? mod;
  mermaid.initialize({ startOnLoad: false, securityLevel: 'loose', suppressErrorRendering: true });
  await registerZenUmlIfPresent(mermaid, dir);

  const results = [];
  for (const b of blocks) {
    try {
      await mermaid.parse(b.code);
      results.push({ ...b, status: 'pass' });
    } catch (err) {
      results.push({ ...b, status: 'fail', error: cleanError(err) });
    }
  }
  return results;
}

function pickMermaidEsm(mermaidDir) {
  const candidates = [
    'dist/mermaid.core.mjs',
    'dist/mermaid.esm.mjs',
    'dist/mermaid.esm.min.mjs',
    'dist/mermaid.js',
  ];
  for (const c of candidates) {
    const p = path.join(mermaidDir, c);
    if (fs.existsSync(p)) return p;
  }
  die(`could not find a mermaid ESM entry point under ${mermaidDir}/dist`);
}

const ZENUML_PACKAGE = '@mermaid-js/mermaid-zenuml';

/** ZenUML 1.x is the line that declares Mermaid 12 support; 0.2.x covers Mermaid 11. */
const zenumlFor = (mermaidVersion) => `${ZENUML_PACKAGE}@${compareVersions(mermaidVersion, '12.0.0') >= 0 ? '1.0.1' : '0.2.3'}`;

/**
 * ZenUML blocks don't parse against core mermaid at all until the plugin is registered
 * (mermaid.registerExternalDiagrams). No-ops if the package isn't in the cache (e.g. it
 * wasn't requested for the current mode/packages list).
 */
async function registerZenUmlIfPresent(mermaid, dir) {
  const pkgDir = resolveInCache(dir, ...ZENUML_PACKAGE.split('/'));
  if (!pkgDir) return false;
  const candidates = ['dist/mermaid-zenuml.core.mjs', 'dist/mermaid-zenuml.esm.mjs', 'dist/mermaid-zenuml.esm.min.mjs'];
  const entry = candidates.map((c) => path.join(pkgDir, c)).find((p) => fs.existsSync(p));
  if (!entry) die(`found ${ZENUML_PACKAGE} in the cache but no known ESM entry under its dist/`);
  const zenumlMod = await import(pathToFileURL(entry).href);
  await mermaid.registerExternalDiagrams([zenumlMod.default ?? zenumlMod]);
  return true;
}

function pickMermaidUmd(mermaidDir) {
  const candidates = ['dist/mermaid.min.js', 'dist/mermaid.js'];
  for (const c of candidates) {
    const p = path.join(mermaidDir, c);
    if (fs.existsSync(p)) return p;
  }
  die(`could not find a browser-loadable mermaid bundle under ${mermaidDir}/dist`);
}

async function validateRender(blocks, dir, { verbose }) {
  const puppeteerDir = resolveInCache(dir, 'puppeteer');
  const mermaidDir = resolveInCache(dir, 'mermaid');
  if (!puppeteerDir || !mermaidDir) die('render mode: puppeteer/mermaid not present in the dependency cache');

  const puppeteerEntry = path.join(puppeteerDir, 'lib', 'puppeteer', 'puppeteer.js');
  if (!fs.existsSync(puppeteerEntry)) die(`puppeteer entry not found at ${puppeteerEntry}`);
  const puppeteer = (await import(pathToFileURL(puppeteerEntry).href)).default;
  const bundle = pickMermaidUmd(mermaidDir);

  const browser = await puppeteer.launch({
    headless: true,
    args: ['--no-sandbox', '--disable-dev-shm-usage', '--allow-file-access-from-files'],
  });
  try {
    const page = await browser.newPage();
    const consoleErrors = [];
    page.on('console', (msg) => {
      if (msg.type() === 'error') consoleErrors.push(msg.text());
    });
    page.on('pageerror', (err) => consoleErrors.push(String(err)));

    await page.setContent('<!doctype html><html><body><div id="container"></div></body></html>');
    await page.addScriptTag({ path: bundle });
    const hasMermaid = await page.evaluate(() => typeof window.mermaid !== 'undefined');
    if (!hasMermaid) die(`loaded ${bundle} but window.mermaid is undefined`);

    await page.evaluate(() => {
      window.mermaid.initialize({ startOnLoad: false, securityLevel: 'loose', suppressErrorRendering: true });
    });

    const results = [];
    for (let i = 0; i < blocks.length; i++) {
      const b = blocks[i];
      consoleErrors.length = 0;
      const out = await page.evaluate(async (code, id) => {
        try {
          await window.mermaid.parse(code);
        } catch (e) {
          return { ok: false, phase: 'parse', error: String(e?.message ?? e) };
        }
        try {
          const { svg } = await window.mermaid.render(id, code);
          return { ok: true, svg: svg.slice(0, 4000) };
        } catch (e) {
          return { ok: false, phase: 'render', error: String(e?.message ?? e) };
        }
      }, b.code, `mv-${i}`);

      if (!out.ok) {
        results.push({ ...b, status: 'fail', phase: out.phase, error: out.error });
        continue;
      }
      const marker = ERROR_SVG_MARKERS.find((m) => out.svg.includes(m));
      if (marker) {
        results.push({ ...b, status: 'fail', phase: 'render', error: `mermaid emitted an error graphic (matched "${marker}")` });
        continue;
      }
      if (consoleErrors.length) {
        results.push({ ...b, status: 'fail', phase: 'render', error: `console error: ${consoleErrors.join(' | ').slice(0, 500)}` });
        continue;
      }
      results.push({ ...b, status: 'pass' });
      if (verbose) process.stderr.write(C.dim(`  rendered ${rel(b.file)}:${b.line}\n`));
    }
    return results;
  } finally {
    await browser.close();
  }
}

function cleanError(err) {
  const msg = String(err?.message ?? err);
  return msg.split('\n').slice(0, 12).join('\n');
}

// ---------------------------------------------------------------------------
// Main
// ---------------------------------------------------------------------------

/** Blocks from user-named files (`--files`): fences in .md, the whole body of .mmd/.mermaid. */
function collectUserFiles(paths) {
  return paths.map((given) => {
    const p = path.resolve(given);
    if (!fs.existsSync(p)) die(`--files: no such file: ${given}`);
    const text = fs.readFileSync(p, 'utf8');
    const display = path.relative(process.cwd(), p).split(path.sep).join('/');
    const standalone = /\.(mmd|mermaid)$/.test(p);
    return {
      path: p,
      rel: display,
      kind: 'user',
      text,
      frontmatter: null,
      blocks: standalone ? [{ file: p, line: 1, code: text, annotation: null }] : extractBlocks(p, text),
    };
  });
}

function collectFiles() {
  const files = [];
  const push = (p, kind) => {
    const text = fs.readFileSync(p, 'utf8');
    files.push({
      path: p,
      rel: rel(p),
      kind,
      text,
      frontmatter: readFrontmatter(text),
      blocks: extractBlocks(p, text),
    });
  };

  push(path.join(SKILL_DIR, 'SKILL.md'), 'skill');
  push(path.join(SKILL_DIR, 'README.md'), 'readme'); // its glossary blocks are copies; keep them honest
  for (const name of fs.readdirSync(REFS_DIR).sort()) {
    const p = path.join(REFS_DIR, name);
    if (fs.statSync(p).isFile() && name.endsWith('.md')) push(p, 'diagram');
  }
  if (fs.existsSync(GENERAL_DIR)) {
    for (const name of fs.readdirSync(GENERAL_DIR).sort()) {
      if (name.endsWith('.md')) push(path.join(GENERAL_DIR, name), 'general');
    }
  }
  // Standalone diagram files, if the skill ever ships any.
  const walk = (d) => {
    for (const name of fs.readdirSync(d)) {
      const p = path.join(d, name);
      const st = fs.statSync(p);
      if (st.isDirectory()) walk(p);
      else if (/\.(mmd|mermaid)$/.test(name)) {
        files.push({ path: p, rel: rel(p), kind: 'standalone', text: '', frontmatter: null, blocks: [{ file: p, line: 1, code: fs.readFileSync(p, 'utf8'), annotation: null }] });
      }
    }
  };
  walk(SKILL_DIR);

  return files;
}

// ---------------------------------------------------------------------------
// Escaping matrix (--escaping)
// ---------------------------------------------------------------------------

const BASELINE_PATH = path.join(HERE, 'escaping-baseline.json');

/**
 * Renders every case from escaping-cases.mjs in headless Chromium and records one outcome per case:
 * P = parsed, rendered and the expected label text is in the SVG; F = parse or render error;
 * M = rendered, but the label text is missing or altered (the silent failures - "no error" is not proof
 * the text survived). Outcomes are compared with the committed baseline for the release's major version.
 */
async function runEscaping(target, opts) {
  assertNodeVersion();
  const { buildCases } = await import(pathToFileURL(path.join(HERE, 'escaping-cases.mjs')).href);
  const cases = buildCases();
  const idsHash = crypto.createHash('sha1').update(cases.map((c) => c[0]).join('\n')).digest('hex').slice(0, 12);
  const major = target.split('.')[0];

  const dir = ensureDeps(target, [`mermaid@${target}`, 'puppeteer'], opts);
  const puppeteerDir = resolveInCache(dir, 'puppeteer');
  const mermaidDir = resolveInCache(dir, 'mermaid');
  if (!puppeteerDir || !mermaidDir) die('escaping: puppeteer/mermaid not present in the dependency cache');
  const puppeteer = (await import(pathToFileURL(path.join(puppeteerDir, 'lib', 'puppeteer', 'puppeteer.js')).href)).default;
  const bundle = pickMermaidUmd(mermaidDir);

  process.stderr.write(`${C.bold(`replaying ${cases.length} escaping cases`)} against mermaid ${target}\n\n`);
  const browser = await puppeteer.launch({ headless: true, args: ['--no-sandbox', '--disable-dev-shm-usage'] });
  let outcomes = '';
  const detail = [];
  try {
    const page = await browser.newPage();
    await page.setContent('<!doctype html><html><body></body></html>');
    await page.addScriptTag({ path: bundle });
    await page.evaluate(() => window.mermaid.initialize({ startOnLoad: false, securityLevel: 'loose', suppressErrorRendering: true }));
    for (let i = 0; i < cases.length; i++) {
      const [, code, expect] = cases[i];
      const r = await page.evaluate(async (src, id, want) => {
        try { await window.mermaid.parse(src); } catch (e) { return 'F'; }
        try {
          const { svg } = await window.mermaid.render(id, src);
          if (svg.includes('aria-roledescription="error"')) return 'F';
          if (want == null) return 'P';
          const text = new DOMParser().parseFromString(svg, 'image/svg+xml').documentElement.textContent.replace(/\s+/g, ' ');
          return text.includes(want.replace(/\s+/g, ' ')) ? 'P' : 'M';
        } catch (e) { return 'F'; }
      }, code, `esc-${i}`, expect);
      outcomes += r;
    }
  } finally {
    await browser.close();
  }

  let baseline = fs.existsSync(BASELINE_PATH) ? JSON.parse(fs.readFileSync(BASELINE_PATH, 'utf8')) : null;
  const count = (ch) => [...outcomes].filter((o) => o === ch).length;
  const summary = `mermaid ${target}: cases=${cases.length} pass=${count('P')} fail=${count('F')} altered-text=${count('M')}`;

  if (opts.updateBaseline) {
    const keep = baseline && baseline.ids === idsHash ? baseline.results : {};
    const next = { ids: idsHash, count: cases.length, results: { ...keep, [major]: outcomes } };
    fs.writeFileSync(BASELINE_PATH, JSON.stringify(next, null, 2) + '\n', 'utf8');
    process.stdout.write(`${summary}\nbaseline for Mermaid ${major}.x written to ${rel(BASELINE_PATH)}\n`);
    return 0;
  }

  const problems = [];
  if (!baseline) problems.push('no baseline yet - run with --update-baseline');
  else if (baseline.ids !== idsHash) problems.push('the case list changed since the baseline was recorded - run with --update-baseline');
  else if (!baseline.results[major]) problems.push(`no baseline for Mermaid ${major}.x - run with --update-baseline`);
  else {
    const want = baseline.results[major];
    const label = { P: 'pass', F: 'fail', M: 'altered text' };
    for (let i = 0; i < cases.length; i++) {
      if (want[i] !== outcomes[i]) problems.push(`${cases[i][0]}: baseline ${label[want[i]]}, now ${label[outcomes[i]]}`);
    }
  }

  if (opts.json) {
    process.stdout.write(JSON.stringify({ mode: 'escaping', target, cases: cases.length, drift: problems }, null, 2) + '\n');
    return problems.length ? 1 : 0;
  }
  process.stdout.write(`${summary}\n`);
  if (problems.length) {
    process.stdout.write(`\n${C.red(`${problems.length} drift from baseline`)} - the escaping guidance in references/ may be stale:\n`);
    for (const m of problems.slice(0, 60)) process.stdout.write(`  ${C.red('DRIFT')} ${m}\n`);
    if (problems.length > 60) process.stdout.write(`  ... and ${problems.length - 60} more\n`);
    return 1;
  }
  process.stdout.write(`${C.green('OK')} - every case matches the baseline\n`);
  return 0;
}

async function main() {
  const opts = parseArgs(process.argv.slice(2));
  if (opts.help) {
    process.stdout.write(HELP);
    return 0;
  }

  const skillText = fs.readFileSync(path.join(SKILL_DIR, 'SKILL.md'), 'utf8');
  let pinned = /^\s*mermaid_version:\s*["']?([0-9][^"'\s]*)["']?\s*$/m.exec(skillText)?.[1] ?? null;
  if (!pinned) pinned = /^\s+mermaid_version:\s*["']?([0-9][^"'\s]*)["']?\s*$/m.exec(skillText)?.[1] ?? null;
  if (opts.verbose && !pinned) process.stderr.write(C.yellow('warning: mermaid_version not found in SKILL.md\n'));
  const target = opts.mermaidVersion ?? pinned;

  if (opts.clean) {
    const dir = cacheDir(target ?? 'unknown');
    if (fs.existsSync(dir)) {
      fs.rmSync(dir, { recursive: true, force: true });
      process.stdout.write(`removed ${dir}\n`);
    } else {
      process.stdout.write(`nothing to remove at ${dir}\n`);
    }
    return 0;
  }

  if (opts.escaping) return runEscaping(target, opts);

  const userFiles = opts.files.length > 0;
  const files = userFiles ? collectUserFiles(opts.files) : collectFiles();

  const problems = [];
  if (!userFiles) {
    problems.push(...structureChecks(files, pinned));
    problems.push(...keywordChecks(files));
  }

  let blocks = files.flatMap((f) => f.blocks.map((b) => ({ ...b, rel: f.rel, slug: f.frontmatter?.slug ?? path.basename(f.path, path.extname(f.path)) })));
  if (opts.only) blocks = blocks.filter((b) => opts.only.includes(b.slug));

  const skipped = blocks.filter((b) => b.annotation?.directive === 'skip');
  // parse-only annotated blocks still get their grammar checked, just not rendered.
  const versionGated = target ? blocks.filter((b) => b.annotation?.directive !== 'skip' && !appliesToVersion(b, target)) : [];
  const runnable = blocks.filter((b) => b.annotation?.directive !== 'skip' && !versionGated.includes(b));

  let installedVersion = null;
  let results = [];

  if (opts.mode !== 'none') {
    // ZenUML has no first-party diagram support in core mermaid - it only parses once its
    // plugin is registered (registerZenUmlIfPresent), so the plugin has to be installed
    // alongside mermaid/jsdom for parse mode. TODO: render mode (puppeteer/browser) doesn't
    // load or register this plugin yet - ZenUML blocks stay `skip`-annotated for render
    // mode until that's added too.
    assertNodeVersion();
    const packages = opts.mode === 'render' ? [`mermaid@${target}`, 'puppeteer'] : [`mermaid@${target}`, 'jsdom', zenumlFor(target)];
    const dir = ensureDeps(target, packages, opts);
    const mermaidPkg = resolveInCache(dir, 'mermaid', 'package.json');
    if (mermaidPkg) installedVersion = JSON.parse(fs.readFileSync(mermaidPkg, 'utf8')).version;

    process.stderr.write(`${C.bold(`checking ${runnable.length} blocks`)} in ${opts.mode} mode against mermaid ${installedVersion}\n\n`);

    if (opts.mode === 'render') {
      const parseOnly = runnable.filter((b) => b.annotation?.directive === 'parse-only');
      const full = runnable.filter((b) => b.annotation?.directive !== 'parse-only');
      results = await validateRender(full, dir, opts);
      if (parseOnly.length) {
        const dir2 = ensureDeps(target, [`mermaid@${target}`, 'jsdom', zenumlFor(target)], opts);
        results.push(...(await validateParse(parseOnly, dir2)));
      }
    } else {
      results = await validateParse(runnable, dir);
    }
  }

  problems.push(...versionChecks(userFiles ? [] : files, pinned, installedVersion, target));

  for (const r of results) {
    if (r.status === 'fail') {
      problems.push({
        check: opts.mode,
        file: r.rel,
        line: r.line,
        message: `${r.phase ?? opts.mode} failed: ${r.error}`,
        code: r.code,
      });
    }
  }

  // -------------------------------------------------------------------------
  // Report
  // -------------------------------------------------------------------------
  if (opts.json) {
    process.stdout.write(JSON.stringify({ mode: opts.mode, pinned, target, installedVersion, blocks: blocks.length, skipped: skipped.length, versionGated: versionGated.length, problems }, null, 2) + '\n');
    return problems.length ? 1 : 0;
  }

  for (const b of skipped) {
    process.stdout.write(`${C.yellow('SKIP')} ${b.rel}:${b.line} - ${b.annotation.reason ?? 'no reason given'}\n`);
  }
  if (opts.verbose) {
    for (const b of versionGated) {
      process.stdout.write(`${C.dim('N/A ')} ${b.rel}:${b.line} - annotated for other mermaid releases (since=${b.annotation.since ?? '-'} until=${b.annotation.until ?? '-'})\n`);
    }
    for (const r of results.filter((r) => r.status === 'pass')) {
      process.stdout.write(`${C.green('PASS')} ${r.rel}:${r.line}\n`);
    }
  }

  const byCheck = {};
  for (const p of problems) (byCheck[p.check] ??= []).push(p);
  for (const [check, list] of Object.entries(byCheck)) {
    process.stdout.write(`\n${C.bold(check)} - ${C.red(`${list.length} problem(s)`)}\n`);
    for (const p of list) {
      const loc = p.line ? `${p.file}:${p.line}` : p.file;
      process.stdout.write(`  ${C.red('FAIL')} ${loc}\n        ${p.message.replace(/\n/g, '\n        ')}\n`);
      if (p.hint) process.stdout.write(`        ${C.dim(p.hint)}\n`);
      if (p.code) {
        const preview = p.code.split('\n').slice(0, 40).map((l, i) => `        ${String(i + 1).padStart(3)} | ${l}`).join('\n');
        process.stdout.write(`${C.dim(preview)}\n`);
      }
    }
  }

  const passed = results.filter((r) => r.status === 'pass').length;
  process.stdout.write(
    `\n${C.bold('summary')} mode=${opts.mode} blocks=${blocks.length} passed=${passed} skipped=${skipped.length} version-gated=${versionGated.length} problems=${problems.length}\n`
  );
  if (problems.length === 0) process.stdout.write(`${C.green('OK')} - everything checks out\n`);

  return problems.length ? 1 : 0;
}

main().then(
  (code) => process.exit(code),
  (err) => {
    process.stderr.write(`validate-mermaid: unexpected failure\n${err?.stack ?? err}\n`);
    process.exit(2);
  }
);
