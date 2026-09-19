// Data for `validate-mermaid.mjs --escaping`: every special character in every label position, in raw
// and escaped form. The documented escaping rules (references/general/authoring-rules.md and each type's
// "Escaping" section) are what this matrix established; `tools/escaping-baseline.json` records the outcome
// per release so a Mermaid upgrade that changes any of them shows up as drift.

export const CHARS = {"lt": "<", "gt": ">", "hash": "#", "semi": ";", "colon": ":", "dq": "\"", "lp": "(", "rp": ")", "lb": "[", "rb": "]", "lc": "{", "rc": "}", "pipe": "|", "amp": "&", "bt": "`"};

const NAMED = { '<': 'lt', '>': 'gt', '"': 'quot', '&': 'amp' };

/** Escape spellings tried for a character: #n; and &#n; always, #name; and &name; where a named code exists. */
function forms(c) {
  const n = c.codePointAt(0);
  const f = { raw: c, hashnum: `#${n};`, htmlnum: `&#${n};` };
  if (NAMED[c]) {
    f.hashname = `#${NAMED[c]};`;
    f.htmlname = `&${NAMED[c]};`;
  }
  return f;
}

/** Label positions. `{L}` is replaced by "x <text> y"; the matrix expects "x <char> y" back in the SVG text. */
export const CONTEXTS = {
  "flow.node.raw": "flowchart TD\n  A[{L}] --> B",
  "flow.node.q": "flowchart TD\n  A[\"{L}\"] --> B",
  "flow.edge.q": "flowchart TD\n  A -->|\"{L}\"| B",
  "seq.alias": "sequenceDiagram\n  participant A as {L}\n  A->>A: x",
  "seq.msg": "sequenceDiagram\n  A->>B: {L}",
  "seq.note": "sequenceDiagram\n  A->>B: hi\n  Note over A,B: {L}",
  "seq.title": "sequenceDiagram\n  title {L}\n  A->>B: hi",
  "class.member": "classDiagram\n  class A {\n    +{L}\n  }",
  "class.label.q": "classDiagram\n  class A[\"{L}\"]",
  "class.rel": "classDiagram\n  A --> B : {L}",
  "state.desc.q": "stateDiagram-v2\n  state \"{L}\" as s1\n  [*] --> s1",
  "state.trans": "stateDiagram-v2\n  [*] --> s1 : {L}",
  "er.comment.q": "erDiagram\n  A {\n    string x \"{L}\"\n  }",
  "er.rel.q": "erDiagram\n  A ||--o{ B : \"{L}\"",
  "pie.label.q": "pie\n  \"{L}\" : 1",
  "pie.title": "pie title {L}\n  \"a\" : 1",
  "gantt.task": "gantt\n  dateFormat YYYY-MM-DD\n  section S\n  {L} :a1, 2024-01-01, 1d",
  "gantt.section": "gantt\n  dateFormat YYYY-MM-DD\n  section {L}\n  t :a1, 2024-01-01, 1d",
  "journey.task": "journey\n  title T\n  section S\n    {L}: 5: Me",
  "journey.title": "journey\n  title {L}\n  section S\n    t: 5: Me",
  "mindmap.plain": "mindmap\n  root\n    {L}",
  "mindmap.q": "mindmap\n  root[\"{L}\"]\n    child",
  "timeline.title": "timeline\n  title {L}\n  2020 : e",
  "timeline.event": "timeline\n  2020 : {L}",
  "timeline.section": "timeline\n  section {L}\n  2020 : e",
  "kanban.q": "kanban\n  col[\"{L}\"]\n    t1[\"{L}\"]",
  "xy.title.q": "xychart-beta\n  title \"{L}\"\n  x-axis [\"a\", \"b\"]\n  line [1, 2]",
  "xy.cat.q": "xychart-beta\n  x-axis [\"{L}\", \"b\"]\n  line [1, 2]",
  "quad.title": "quadrantChart\n  title {L}\n  x-axis Low --> High\n  y-axis Low --> High\n  quadrant-1 A\n  P: [0.3, 0.3]",
  "quad.point": "quadrantChart\n  x-axis Low --> High\n  y-axis Low --> High\n  quadrant-1 A\n  {L}: [0.3, 0.3]",
  "quad.point.q": "quadrantChart\n  x-axis Low --> High\n  y-axis Low --> High\n  quadrant-1 A\n  \"{L}\": [0.3, 0.3]",
  "git.commit.q": "gitGraph\n  commit id: \"{L}\"",
  "sankey.q": "sankey\n\nA,\"{L}\",1",
  "arch.raw": "architecture-beta\n  service s(server)[{L}]",
  "block.q": "block\n  a[\"{L}\"]",
  "treemap.q": "treemap-beta\n  \"{L}\"\n    \"leaf\": 1",
  "req.text.q": "requirementDiagram\n  requirement r {\n    id: 1\n    text: \"{L}\"\n    risk: high\n    verifymethod: test\n  }",
  "c4.q": "C4Context\n  Person(p, \"{L}\")",
  "usecase.q": "usecase-beta\n  actor A(\"{L}\")\n  A --> U",
  "agentflow.q": "agentflow-beta TB\n  a[\"{L}\"]@{ shape: task }",
  "railroad.term.q": "railroad-ebnf-beta\n  r = \"{L}\" ;",
  "venn.q": "venn-beta\n  set A[\"{L}\"]",
  "radar.q": "radar-beta\n  axis a[\"{L}\"], b[\"x\"], c[\"y\"]\n  curve k[\"c\"]{1,2,3}",
  "ishikawa.raw": "ishikawa-beta\n  Problem\n    Cause\n      {L}",
  "wardley.q": "wardley-beta\n  title \"{L}\"\n  component C [0.5, 0.5]",
  "treeview.q": "treeView-beta\n  \"{L}\"\n    \"b\"",
  "packet.q": "packet\n+8: \"{L}\"\n+8: \"b\"",
  "cynefin.q": "cynefin-beta\n  title T\n  clear\n    \"{L}\"",
  "swimlane.node.raw": "swimlane-beta LR\n  subgraph C\n    a[{L}]\n  end",
  "swimlane.node.q": "swimlane-beta LR\n  subgraph C\n    a[\"{L}\"]\n  end",
  "wardley.title.raw": "wardley-beta\ntitle {L}\ncomponent C [0.5, 0.5]"
};

/** One-off cases: [id, code, expected text or null]. */
export const FIXED = [
  [
    "fixed|sankey doubled dq",
    "sankey\n\nA,\"say \"\"hi\"\" now\",1",
    "say \"hi\" now"
  ],
  [
    "fixed|sankey unicode quoted",
    "sankey\n\nA,\"café\",1",
    "café"
  ],
  [
    "fixed|sankey unicode unquoted",
    "sankey\n\nA,café,1",
    "café"
  ],
  [
    "fixed|sankey comma quoted",
    "sankey\n\nA,\"a, b\",1",
    "a, b"
  ],
  [
    "fixed|arch quoted brackets",
    "architecture-beta\n  service s(server)[\"a [b] c\"]",
    "a [b] c"
  ],
  [
    "fixed|arch quoted plain",
    "architecture-beta\n  service s(server)[\"a b c\"]",
    "a b c"
  ],
  [
    "fixed|arch parens in label",
    "architecture-beta\n  service s(server)[f(x)]",
    "f(x)"
  ],
  [
    "fixed|arch quoted parens",
    "architecture-beta\n  service s(server)[\"f(x)\"]",
    "f(x)"
  ],
  [
    "fixed|c4 typographic quotes",
    "C4Context\n  Person(p, \"say “hi”\")",
    "say “hi”"
  ],
  [
    "fixed|c4 single quotes",
    "C4Context\n  Person(p, \"say 'hi'\")",
    "say 'hi'"
  ],
  [
    "fixed|c4 backslash dq",
    "C4Context\n  Person(p, \"say \\\"hi\\\"\")",
    "say \"hi\""
  ],
  [
    "fixed|c4 hash raw",
    "C4Context\n  Person(p, \"a # b\")",
    "a # b"
  ],
  [
    "fixed|c4 comma quoted",
    "C4Context\n  Person(p, \"a, b\")",
    "a, b"
  ],
  [
    "fixed|c4 paren quoted",
    "C4Context\n  Person(p, \"f(x)\")",
    "f(x)"
  ],
  [
    "fixed|wardley quoted comp colon",
    "wardley-beta\ncomponent \"a: b\" [0.5, 0.5]",
    null
  ],
  [
    "fixed|wardley quoted comp paren",
    "wardley-beta\ncomponent \"f(x) [b]\" [0.5, 0.5]",
    null
  ],
  [
    "fixed|wardley plain spaces",
    "wardley-beta\ncomponent Cup of Coffee [0.5, 0.5]",
    null
  ],
  [
    "fixed|wardley anchor+link quoted",
    "wardley-beta\nanchor \"Cust: A\" [0.9, 0.9]\ncomponent \"B: c\" [0.5, 0.5]\n\"Cust: A\" -> \"B: c\"",
    null
  ],
  [
    "fixed|treeview bare colon",
    "treeView-beta\n  a: b\n    c",
    null
  ],
  [
    "fixed|eventmodel colon in id",
    "eventmodeling\ntf 01 ui Cart:UI",
    null
  ],
  [
    "fixed|eventmodel data all chars",
    "eventmodeling\ntf 01 ui CartUI {a < b > c ; d # e : f \"g\" (h) [i]}",
    null
  ],
  [
    "fixed|packet bare label",
    "packet\n+8: Version",
    null
  ],
  [
    "fixed|mindmap markdown string",
    "mindmap\n  root\n    \"`a (b) [c] {d}`\"",
    null
  ]
];

/** Deterministic case list: [id, code, expect]. Order matters - the baseline is positional. */
export function buildCases() {
  const cases = [];
  for (const [ctx, template] of Object.entries(CONTEXTS)) {
    for (const [charName, ch] of Object.entries(CHARS)) {
      for (const [form, text] of Object.entries(forms(ch))) {
        cases.push([`${ctx}|${charName}|${form}`, template.replace('{L}', `x ${text} y`), `x ${ch} y`]);
      }
    }
  }
  for (const f of FIXED) cases.push([f[0], f[1], f[2]]);
  return cases;
}
