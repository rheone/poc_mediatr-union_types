# CI hang: child `dotnet build` spawned from `dotnet test`, first call only, Linux runners

Compiled 2026-09-18, investigating why `tests/MediatrUnionPoc.Application.Tests/Unions/ExhaustivenessTests.cs`'s
`BuildScratchProjectAsync` — which shells out to `dotnet build "<scratch>.csproj" --nologo -p:NuGetAudit=false`
via `System.Diagnostics.Process` — hangs for exactly the caller's own 3-minute timeout on its
**first** invocation in GitHub Actions (`ubuntu-latest`), while three subsequent scratch builds in
the same test run finish in 1–3s each, and the identical command run as a standalone shell step in
the same job finishes in under a second. Five prior CI round-trips (cold-restore, `NuGetAudit`,
inherited MSBuild env vars, stdin inheritance, polling instead of `WaitForExitAsync`) have each been
independently ruled out as the cause. This pass focuses on MSBuild's own node-reuse/server
machinery, since the outer `dotnet test --no-build` command is confirmed (via CI logs showing
"building ... on node 4") to be running its own multi-node parallel build for the whole duration the
hang occurs in.

Every claim below is cited inline to its primary source (GitHub source/issues in `dotnet/msbuild`,
`dotnet/sdk`, `dotnet/runtime`, or Microsoft Learn docs). Secondary sources are explicitly flagged
and were used only as pointers to primary material, never as a stand-alone fact.

**Primary sources used:**

- [`dotnet/msbuild` — `documentation/MSBuild-Server.md`](https://github.com/dotnet/msbuild/blob/main/documentation/MSBuild-Server.md) — design doc for MSBuild Server: IPC (named pipes), pipe-naming/hashing, enable/disable env vars.
- [`dotnet build-server` — .NET CLI reference (Microsoft Learn)](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-build-server) — documents the three build servers (MSBuild, Razor, VB/C# compiler) and `dotnet build-server shutdown`.
- [dotnet/msbuild#5440 — "Fix visual studio applying nodereuse despite MSBUILDDISABLENODEREUSE being set"](https://github.com/dotnet/msbuild/pull/5440) — PR fixing a code path (Design-Time Builds) that ignored the env var by hardcoding `nodeReuse: true`; introduces the centralized `Traits`-based check.
- [dotnet/msbuild#15049 — "Node reuse probe failures are fatal"](https://github.com/dotnet/msbuild/issues/15049) — root-caused a CI-only MSBuild node-reuse failure against a container with many stale node processes; distinguishes "failing to reuse" (recoverable) vs. "failing to launch" (fatal).
- [dotnet/msbuild#15060 — "Node reuse over-provisioning heuristic keeps zero nodes when builds share a machine"](https://github.com/dotnet/msbuild/issues/15060) — explains the node-reuse idle timeout (15 min, tuned for interactive dev) and how orphaned nodes accumulate system-wide in CI.
- [dotnet/msbuild#15061 — "Node reuse node count includes nodes that can't be reused by the counting instance"](https://github.com/dotnet/msbuild/issues/15061) — companion issue; node-reuse probing produced "249 pipe-connect timeouts against 69 successful pipe connections" in one CI run.
- [dotnet/sdk#42821 — `"dotnet build" hangs when called from test`](https://github.com/dotnet/sdk/issues/42821) — open, unresolved issue matching this repo's exact symptom shape (child `dotnet build` spawned from a test process hangs in CI, first run far slower than later runs), no maintainer root-cause yet.
- [dotnet/sdk#43432 — `dotnet test hangs with .NET9 RC1/RC2 in CI build on Ubuntu`](https://github.com/dotnet/sdk/issues/43432) — Ubuntu-only, `dotnet test`-only hang; workaround is splitting `dotnet build` + `dotnet test --no-build` into separate steps.
- [dotnet/sdk#45461 — `[NET9] dotnet test hangs during build`](https://github.com/dotnet/sdk/issues/45461) — stack trace pinpoints the hang inside MSBuild's `BuildSubmission.Execute()`; intermittent, unresolved.
- [dotnet/runtime#134043 — "Concurrent shared compilations can fail releasing the client mutex on Linux"](https://github.com/dotnet/runtime/issues/134043) / [dotnet/roslyn#85264 (companion)](https://github.com/dotnet/roslyn/issues/85264) — Roslyn compiler-server named client mutex, `WaitOne`/`ReleaseMutex` thread-affinity violated under concurrent `Csc` tasks on Linux/.NET 11, observed in Helix CI.
- [dotnet/runtime#133736 — "Process reports its ptrace-stopped child as exited when the parent is the tracer"](https://github.com/dotnet/runtime/issues/133736) and [PR #133948 (fix)](https://github.com/dotnet/runtime/pull/133948) — misclassification in the runtime's SIGCHLD-driven child-reaping code (`CheckChildren`).
- [PR dotnet/runtime#133930 — "Fix `Process.Kill(entireProcessTree: true)` hang on macOS"](https://github.com/dotnet/runtime/pull/133930) — describes the SIGCHLD reaper (`CheckChildren`) spinning forever while holding `s_childProcessWaitStates`/`s_processStartLock` under a waitid quirk (macOS-specific, but the shared-lock architecture it describes is cross-platform).

**Secondary sources (flagged inline wherever used):** none used as fact — WebSearch aggregator
summaries were used only to locate the primary GitHub issues/docs above, and every claim quoted
below was independently re-fetched from the primary source itself.

---

## 1. Does the outer MSBuild multi-node build block the FIRST child `dotnet build`'s ability to get a node/scheduler slot?

**Finding: plausible and consistent with documented behavior, but not confirmed as *the* mechanism — no primary source describes this exact "child process blocked by outer process's node count" scenario.** What the primary sources *do* establish is that MSBuild's node-reuse probing is expensive, stateful, and shared machine-wide by process name/mode, not scoped per parent process:

- Node-reuse counting is global to the machine, not per-build: `CountActiveNodesWithMode` and `GetPossibleRunningNodes` "filter only on process name and NodeMode, but nothing checks whether a counted node is actually available to the counting instance" (dotnet/msbuild#15061).
- Idle nodes are kept alive for 15 minutes — "explicitly tuned for an interactive developer. In CI nobody 'does another build in this time,' so every orphaned node lingers for 15 minutes and the system-wide count climbs" (dotnet/msbuild#15060). A `dotnet test` run against 5 projects, each spawning its own VSTest-target MSBuild nodes, is exactly the kind of workload that produces many concurrently-live nodes.
- Reuse probing against those nodes is not cheap when they're busy: one CI run instrumented in dotnet/msbuild#15061 showed "249 pipe-connect timeouts against 69 successful pipe connections" — i.e., the *majority* of reuse-probe attempts in a busy CI build time out rather than succeed or fail fast.
- A related failure mode (dotnet/msbuild#15049) shows that when a reuse probe against a stale/busy node throws, the code path can abort the *entire* build with `MSB0001` instead of falling back to `StartNewNode` — "one transient fault against one stale candidate aborts the build with MSB0001, with a working fallback available and untried." That specific bug fails fast rather than hangs, so it doesn't explain a 3-minute block by itself, but it's evidence of the same reuse-probing subsystem misbehaving specifically under CI-scale node counts.

> [!NOTE]
> None of these issues is your exact scenario (a *child* `dotnet build` process, spawned by `Process.Start` from inside a `dotnet test`-orchestrated MSBuild build, competing for reuse against the *outer* build's own live nodes). But dotnet/sdk#42821 is: an open, unresolved, and un-triaged issue titled exactly `"dotnet build" hangs when called from test`, filed against GitHub Actions, where the reporter's own words are "the first run took ~15 minutes [on an Ubuntu VM], then finishes fast on consecutive runs" — this is the closest primary-source match to your exact symptom pattern found anywhere in this research pass, and it has **no maintainer-provided root cause as of this writing.**

**Confidence: medium** that node-reuse contention with the outer build's live nodes is *a* contributing mechanism; **low/unconfirmed** on the precise blocking primitive, since no source traces the hang to a specific line of code in this exact parent/child topology.

---

## 2. Synchronization primitives NOT covered by `DOTNET_CLI_DISABLE_BUILD_SERVERS` / `MSBUILDDISABLENODEREUSE`

**Finding: yes — there is a documented history of exactly this kind of gap, where one control surface (an env var) doesn't reach every code path that makes a node-reuse/server decision.**

- dotnet/msbuild#5440's own PR description states plainly: "MSBuild is launched differently for Design Time Builds compared to regular builds. These builds don't respect the environment variable `MSBUILDDISABLENODEREUSE` because they explicitly set `nodeReuse` to `true`" in the API call itself, bypassing the env-var check entirely. The fix consolidated call sites onto a single `Traits`-class property — but that PR is scoped to Visual Studio design-time builds, not to `dotnet build`/`dotnet test`'s CLI-driven paths, so it does not by itself prove your exact code path is covered.
- The MSBuild Server (a **separate, newer** subsystem from classic worker-node reuse) has its own IPC and its own enable/disable surface: named pipes keyed by a `MSBuildServer-{hash}` name, "hash... is basically hash of the handshake object" identifying architecture, user, and MSBuild version (`documentation/MSBuild-Server.md`). This is a **different synchronization primitive** (a named pipe + implicit "is a listener present" check) than the worker-node reuse mechanism `MSBUILDDISABLENODEREUSE`/`-nodeReuse` control, and it is gated by its own env var — Microsoft Learn's MSBuild Server page states the CLI-side default is controlled by `DOTNET_CLI_USE_MSBUILD_SERVER` (opt-in in some SDK versions, and separately reported by MSBuild's own server doc as toggled via a `DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER=1` disable switch). **Neither of these MSBuild-Server-specific variables is the same variable as `DOTNET_CLI_DISABLE_BUILD_SERVERS`**, which per the `dotnet build-server` CLI reference is really an umbrella for the `--disable-build-servers` CLI *option* added in .NET 7 for `build`/`restore`/`publish` — and per dotnet/sdk#31651 ("Add support for `--disable-build-servers` to more MSBuild-driven commands"), that option's coverage is **incomplete across CLI commands**, i.e. it does not reach every command/build path in the SDK.
- Roslyn's own compiler server (VBCSCompiler, used for shared C#/VB compilation) uses a **named client mutex**, entirely independent of the MSBuild worker-node or MSBuild-Server pipes: `BuildServerConnection.RunServerBuildRequestAsync`'s `tryConnectToServerAsync` "is deliberately non-async so `WaitOne` and `ReleaseMutex` execute on the same thread" (dotnet/runtime#134043, describing a real thread-affinity bug in that exact code path on Linux/.NET 11, observed at a 0.52% failure rate in Helix CI on Ubuntu 22.04). This mutex is not named or gated by either `DOTNET_CLI_DISABLE_BUILD_SERVERS` or `MSBUILDDISABLENODEREUSE` — it's a third, independent synchronization primitive in the startup path of any `dotnet build` that compiles C#, and `dotnet build-server shutdown --vbcscompiler` (or `--razor`) is the only documented way to reset it (Microsoft Learn, `dotnet build-server`).

> [!WARNING]
> This is the most actionable finding of the whole research pass: **there are at least three independent synchronization layers** a `dotnet build` startup can touch — (1) classic MSBuild worker-node reuse (`MSBUILDDISABLENODEREUSE`/`-nodeReuse`), (2) the newer MSBuild Server named-pipe listener (`DOTNET_CLI_USE_MSBUILD_SERVER`/a separate disable env var per MSBuild's own doc), and (3) the Roslyn/VB-CS compiler server's named mutex (only reset via `dotnet build-server shutdown --vbcscompiler`, not any env var tried so far). Your CI attempts have only ever touched (1) and the CLI's `--disable-build-servers`-adjacent umbrella, not (3), and the *effective* env var name for (2) is inconsistently reported even across Microsoft's own docs (see confidence note below) — so it may not have been fully disabled either.

**Confidence: medium-high** that a gap exists in principle (documented precedent in #5440, #31651); **low/unconfirmed** on which exact primitive is the one stalling in your specific 3-minute hang, since no source traces a hang of that duration to any one of these three mechanisms directly.

---

## 3. First-use `Process.Start`/child-reaping cost under a busy multi-process .NET host on Linux

**Finding: there is a real, documented class of bug in the runtime's Linux child-process-reaping code, but nothing found describes it as specifically "first Process.Start only" or scaling with the number of concurrently active child/grandchild processes in the way your symptom would require.**

- The runtime's SIGCHLD-driven reaper (`CheckChildren`) has had at least one confirmed bug where it can "spin forever while holding `s_childProcessWaitStates`/`s_processStartLock`" due to a `waitid` quirk misreporting stopped (not exited) children — but that specific bug is **macOS-only** per its own description (dotnet/runtime PR #133930, fixed alongside `Process.Kill(entireProcessTree: true)` hangs).
- A closely related, more recent bug — "Process reports its ptrace-stopped child as exited when the parent is the tracer" (dotnet/runtime#133736, fixed by PR #133948) — does affect **Linux**, but requires the .NET process to itself be a ptrace tracer of the child (not the default situation for a plain `Process.Start`), and describes a *misreport* (child looks exited when it isn't), not a multi-minute block.
- No primary source found describes SIGCHLD/`waitpid` dispatch becoming "backlogged" as a function of how many *other, unrelated* child/grandchild processes a busy multi-node MSBuild host has active — the closest analogue found (dotnet/runtime#103812, "ThreadPool unfriendly to background processes") is about idle ThreadPool workers consuming CPU via spin-waiting, not about process-reap latency, and is not process-count-scaling in the way needed to explain "hangs on call #1, instant on calls #2–4."
- The user's own experiment (replacing `WaitForExitAsync` with a manual `HasExited` poll, still hanging the full 3 minutes) is itself strong first-hand evidence against a SIGCHLD-dispatch theory: if the *notification* mechanism were the bottleneck, polling `HasExited` (a direct `waitpid(WNOHANG)`-style check, not signal-dependent) should have shown the child process's actual exit time, and it did not — the process was genuinely still running for the full 3 minutes, not just failing to report its exit.

> [!NOTE]
> Given the polling experiment already ruled out, this line of investigation should be considered
> **effectively closed for this bug** even though the general bug class (documented Linux/macOS
> child-reaping issues in `dotnet/runtime`) is real. The hang is a genuine block on the *child's own
> startup/build*, not a notification-delivery problem in the *parent*.

**Confidence: low** that this explains the symptom (contradicted by the polling experiment already run); **medium** that the general bug class exists and is real, just not load-bearing here.

---

## 4. `-nodeReuse:false` vs. `MSBUILDDISABLENODEREUSE` — same effect?

**Finding: historically NOT reliably equivalent — there is a documented case where they diverged because different code paths inside MSBuild only checked one or the other, not both.**

- dotnet/msbuild#5440's PR body confirms Design-Time Builds "don't respect the environment variable `MSBUILDDISABLENODEREUSE`" because that specific caller "explicitly set[s] `nodeReuse` to `true`" via the build-request API, which takes precedence at that call site regardless of the environment variable. This is a documented case of the **env var being silently overridden by a hardcoded API-level setting**, not the command-line switch and env var disagreeing with each other directly — but it demonstrates the underlying risk: node-reuse is decided per call site, and prior to the fix, at least one call site ignored the env var entirely.
- The fix in that PR was to introduce a single centralized `Traits`-class property ("I'll go ahead and replace every manual check for this flag with this new property," per a reviewer comment quoted in the PR) specifically because *multiple, independent manual checks* of the flag existed across the codebase before that consolidation — strong evidence that, at least historically, "did you set the env var" and "did the code path in question actually check it" were two different questions.
- No primary source found states that the `-nodeReuse:false` **command-line switch** (as opposed to environment variables generally) is authoritative over all env-var-based paths, or vice versa — the PR's fix consolidates *env-var* checking, not command-line-switch-vs-env-var precedence specifically. `dotnet build`/`dotnet test` invoke MSBuild via the SDK's own driver, not `MSBuild.exe` directly, so `-nodeReuse:false` (an `MSBuild.exe`/`msbuild -nr:false` switch) isn't even necessarily plumbed through to the SDK-level `dotnet build` CLI invocation your scratch-project child process uses — none of the fetched sources confirm the .NET CLI forwards an explicit `-nodeReuse:false` switch differently from how it forwards `MSBUILDDISABLENODEREUSE`.

**Confidence: medium** that they are not guaranteed equivalent in general (documented divergence exists for at least one call site); **unconfirmed** whether `dotnet build`'s own CLI-to-MSBuild plumbing treats them identically for your specific scratch-project invocation — this needs direct testing (`dotnet build ... -nodeReuse:false` passed explicitly as an MSBuild property/switch on the child invocation, not just the env var) since it wasn't yet tried per the "already ruled out" list (only `MSBUILDDISABLENODEREUSE` was tried, not the CLI switch form).

---

## 5. Known workaround patterns for spawning a child `dotnet build` from inside a running `dotnet test`/VSTest host on Linux CI

**Finding: the most concrete, repeatedly-corroborated real-world workaround found across multiple independent issues is process/step isolation — never spawn the child build from inside the same `dotnet test` invocation's process tree, i.e., don't do what this test does.**

- dotnet/sdk#43432's own conclusion, from direct experimentation: "the working approach: execute `dotnet build` separately, then run `dotnet test --no-build`" — i.e., split the outer build and the outer test into separate CI steps, so `dotnet test` never itself hosts an active MSBuild multi-node build while anything (child or otherwise) needs to also invoke `dotnet build`.
- dotnet/sdk#45461 independently arrived at the identical workaround ("running `dotnet build` beforehand followed by `dotnet test --no-build` consistently works") for a stack trace that pins the hang specifically inside MSBuild's `BuildSubmission.Execute()` — i.e., inside MSBuild's own build-submission machinery, not VSTest or the test adapter layer, which is consistent with this being a node/server contention problem rather than a test-runner problem.
- No Roslyn-repo or other prominent OSS "compile a scratch project as a child process from inside a test host" pattern was found during this pass that documents a specific Linux-CI-safe recipe; this remains an open gap in the research (worth a follow-up pass specifically crawling `dotnet/roslyn`'s own compiler test suite for how it shells out to `csc`/`dotnet build`, if it does so via child process at all rather than in-proc `CSharpCompilation` — **not verified in this pass**).
- `actions/runner-images` was searched specifically for ubuntu-latest-specific causes (process/file-descriptor limits, cgroup throttling, antivirus/defender scanning of freshly-exec'd binaries). **No such issue was found.** The closest hits were unrelated Ubuntu-24.04-migration slowdowns (`actions/runner-images#11790`, `#11432`) and a `debuginfod` network-fetch slowness report (`actions/runner-images#14545`) — the latter is specifically about symbol-server lookups during crash/backtrace handling, not build startup, and was not corroborated as relevant to a plain `dotnet build` invocation.

> [!NOTE]
> The convergence of two independent, unrelated bug reporters (#43432, #45461) on the exact same
> workaround — split the build and the test into separate steps/processes — combined with #45461's
> stack trace pinning the hang inside MSBuild's own `BuildSubmission.Execute()` rather than
> anywhere in VSTest, is the strongest circumstantial evidence in this whole research pass that the
> root cause is genuinely "MSBuild's own build-submission/node machinery misbehaves when another
> MSBuild build is already active in the same process tree" — independent of the specific env vars
> tried so far.

**Confidence: high** that step/process isolation (never running a child `dotnet build` while a `dotnet test`-driven MSBuild build is active in the same or a parent process) is a reliable workaround, since it's independently corroborated twice; **low/unconfirmed** on runner-image-specific causes (process limits, AV scanning) — no evidence found either way.

---

## Ranked next things to try

1. **Move the scratch builds out of the `dotnet test` process tree entirely** (highest confidence — directly corroborated twice, dotnet/sdk#43432 and #45461, and matches the "hangs inside `BuildSubmission.Execute()`" stack trace). Concretely: have CI run the four `CompileTimeChecks` scratch builds as a **separate GitHub Actions step**, shelled out directly (not via a test process), writing results to a file the test then asserts against — sidesteps the parent/child MSBuild coexistence problem rather than fighting it.
2. **Explicitly disable the Roslyn/VBCSCompiler build server for the child process** via `dotnet build-server shutdown --vbcscompiler` immediately before the CI job's Test step, and/or pass `-p:UseSharedCompilation=false` on the scratch build's own `dotnet build` invocation. This targets synchronization primitive (3) from §2 — the named client mutex in `BuildServerConnection` — which is the one mechanism identified in this pass that none of the five prior attempts touched at all.
3. **Pass `-nodeReuse:false` as an explicit MSBuild switch/property on the child `dotnet build` invocation itself**, not just the `MSBUILDDISABLENODEREUSE` env var — per §4, the env var and the switch are not proven to be plumbed identically through the SDK's CLI driver, and only the env var form has been tried so far.
4. **If (1) is not viable, serialize the scratch builds to run before `dotnet test` starts its own multi-node build** (e.g., a pre-Test CI step that primes/completes all four scratch builds, similar to the already-tried "cold-restore" experiment in the "ruled out" list — but note that experiment pre-*built* the project, it did not run the build *before the outer `dotnet test` process existed*, which is the meaningfully different condition here per finding 1/5).
5. **Instrument the hang directly** rather than continuing to guess at env vars: capture `MSBUILDDEBUGPATH`/`MSBUILDDEBUGCOMM=1` diagnostic output and/or a process/thread dump of the stuck child `dotnet build` at the moment of the hang (e.g., via `dotnet-dump` or `procfs` inspection right before the 3-minute timeout fires). Every issue cited in §1 and §5 that reached a concrete root cause did so via exactly this kind of low-level MSBuild diagnostic logging or stack trace — guessing at env vars has produced five ruled-out negatives, while direct instrumentation (as in dotnet/sdk#45461's `BuildSubmission.Execute()` trace) is what actually localized the fault in the closest analogous public issue.
