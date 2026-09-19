# Sub-Agent Briefing

Pass paths, not pasted rules. A sub-agent that writes or modifies a test class gets all of:

1. **Production class** under test (path)
2. **Gap report** for that class from Discovery
3. **Rule files** (paths): `references/quality-checklist.md`, the detected test-framework companion and mocking companion (`SKILL.md`, `REFERENCE.md`, `ANTI-PATTERNS.md`), and `references/multiframework.md` when the project targets net4x or netstandard
4. **Demand**: every checklist item applied to every test in the class, written at the lowest common denominator target framework
5. **Report**: what was written and what was skipped, with reasons. Leave test runs to the main agent (Sweep Loop step 7).

Omitting item 2 or 3 caused the most expensive recoveries.
