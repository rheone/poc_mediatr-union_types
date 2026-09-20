#!/usr/bin/env node
// Minimal headless LSP client used by smoke-test.sh (and by hand) to ask a language server a symbol
// question without an editor. Usage:
//   node lsp-query.mjs --root <dir> --file <path relative to root> --lang <languageId> \
//        [--expect <symbol name>]... [--workspace <query>] [--diagnostics] [--timeout <s>] \
//        [--solution-open <solution path>]   (Roslyn language server: send its solution/open notification) \
//        -- <server command> [server args...]
// Prints a JSON summary. Exit 0 only if every --expect symbol is found (document symbols, or
// workspace/symbol when --workspace is given).
import { spawn } from "node:child_process";
import { readFileSync } from "node:fs";
import { pathToFileURL } from "node:url";
import path from "node:path";

const argv = process.argv.slice(2);
const sep = argv.indexOf("--");
const opts = argv.slice(0, sep);
const [serverCmd, ...serverArgs] = argv.slice(sep + 1);
const get = (n) => opts.filter((v, i) => opts[i - 1] === n);
const root = path.resolve(get("--root")[0] ?? ".");
const file = path.resolve(root, get("--file")[0]);
const lang = get("--lang")[0] ?? "csharp";
const expects = get("--expect");
const wsQuery = get("--workspace")[0];
const solutionOpen = get("--solution-open")[0];
const wantDiagnostics = opts.includes("--diagnostics");
const timeoutMs = Number(get("--timeout")[0] ?? 90) * 1000;

const child = spawn(serverCmd, serverArgs, { stdio: ["pipe", "pipe", "inherit"], cwd: root });
let buf = Buffer.alloc(0);
let nextId = 1;
const pending = new Map();
const diagnostics = [];

const send = (msg) => {
  const body = Buffer.from(JSON.stringify({ jsonrpc: "2.0", ...msg }));
  child.stdin.write(`Content-Length: ${body.length}\r\n\r\n`);
  child.stdin.write(body);
};
const request = (method, params) =>
  new Promise((resolve, reject) => {
    const id = nextId++;
    pending.set(id, { resolve, reject });
    send({ id, method, params });
  });

child.stdout.on("data", (chunk) => {
  buf = Buffer.concat([buf, chunk]);
  for (;;) {
    const headerEnd = buf.indexOf("\r\n\r\n");
    if (headerEnd < 0) return;
    const len = Number(/Content-Length: (\d+)/i.exec(buf.subarray(0, headerEnd).toString())[1]);
    if (buf.length < headerEnd + 4 + len) return;
    const msg = JSON.parse(buf.subarray(headerEnd + 4, headerEnd + 4 + len).toString());
    buf = buf.subarray(headerEnd + 4 + len);
    if (msg.id !== undefined && (msg.result !== undefined || msg.error)) {
      pending.get(msg.id)?.[msg.error ? "reject" : "resolve"](msg.error ?? msg.result);
      pending.delete(msg.id);
    } else if (msg.id !== undefined) {
      send({ id: msg.id, result: null }); // server->client request (e.g. registerCapability): acknowledge
    } else if (msg.method === "textDocument/publishDiagnostics") {
      diagnostics.push(...msg.params.diagnostics);
    }
  }
});

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const flatten = (syms) => syms.flatMap((s) => [s.name, ...flatten(s.children ?? [])]);
const deadline = Date.now() + timeoutMs;

try {
  await request("initialize", {
    processId: process.pid,
    rootUri: pathToFileURL(root).href,
    workspaceFolders: [{ uri: pathToFileURL(root).href, name: path.basename(root) }],
    capabilities: {
      textDocument: {
        documentSymbol: { hierarchicalDocumentSymbolSupport: true },
        publishDiagnostics: {},
      },
      workspace: { symbol: {}, workspaceFolders: true },
    },
  });
  send({ method: "initialized", params: {} });
  if (solutionOpen) send({ method: "solution/open", params: { solution: pathToFileURL(path.resolve(root, solutionOpen)).href } });
  const uri = pathToFileURL(file).href;
  send({
    method: "textDocument/didOpen",
    params: { textDocument: { uri, languageId: lang, version: 1, text: readFileSync(file, "utf8") } },
  });

  let names = [];
  let source = "textDocument/documentSymbol";
  while (Date.now() < deadline) {
    if (wsQuery) {
      source = "workspace/symbol";
      const r = (await request("workspace/symbol", { query: wsQuery }).catch(() => null)) ?? [];
      names = r.map((s) => s.name);
    } else {
      const r = (await request("textDocument/documentSymbol", { textDocument: { uri } }).catch(() => null)) ?? [];
      names = flatten(r);
    }
    if (expects.every((e) => names.some((n) => n === e || n.startsWith(`${e}(`) || n.endsWith(`.${e}`)))) break;
    await sleep(2000);
  }
  if (wantDiagnostics) {
    await sleep(3000);
    // Servers that use pull diagnostics (csharp-ls) answer this instead of pushing.
    const pulled = await request("textDocument/diagnostic", { textDocument: { uri } }).catch(() => null);
    diagnostics.push(...(pulled?.items ?? []));
  }
  const missing = expects.filter((e) => !names.some((n) => n === e || n.startsWith(`${e}(`) || n.endsWith(`.${e}`)));
  console.log(
    JSON.stringify({
      server: [serverCmd, ...serverArgs].join(" "),
      source,
      symbols: names.slice(0, 40),
      missing,
      diagnostics: diagnostics.map((d) => `${d.code ?? ""} ${d.message}`.trim()).slice(0, 10),
    }),
  );
  process.exitCode = missing.length ? 1 : 0;
} catch (e) {
  console.error("lsp-query failed:", e);
  process.exitCode = 2;
} finally {
  child.kill();
}
