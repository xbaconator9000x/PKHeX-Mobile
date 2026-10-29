# Session activation transaction and release handoff gates — 2026-10-29

Companion to [UPSTREAM_SESSION_ACTIVATION_TRANSACTION_HANDOFF_2026-10-29.md](UPSTREAM_SESSION_ACTIVATION_TRANSACTION_HANDOFF_2026-10-29.md). Audited main `bb57c444166515b7406f14d2a38302e503ba88a0`; PKForge `2df04430e532b78c247f45b936f3068d8f824987`; PKHeX MAUI `a6f7cfb78e1c96155baa67660fd7ae97895e375f`; official Core release `bfe89c74a3f5265805e364b79bc04ad38ebbc5ea` / NuGet `26.10.18`.

**Evidence split:** PR #59 provider-origin preview and PR #60 actual-service provider-read ordering smoke **passed** [official-Core probe 40780548476](https://github.com/xbaconator9000x/PKHeX-Mobile/actions/runs/40780548476); [merged-main Foundation 40781034083](https://github.com/xbaconator9000x/PKHeX-Mobile/actions/runs/40781034083) passed. The **12 gates below are new, proposed and unexecuted**. A passing Android compile is not device execution or save-write certification.

## P0 — session activation commit boundary (actual adapted PKForge service)

| Gate | Controlled interleaving / failure injection | Required pass evidence |
| --- | --- | --- |
| AT-01 check-to-activation Open/Open | A completes provider read and parse; block A **after final epoch check but before `MakeActive`**; start/commit B; release A. Use barriers/hooks, not sleeps. | B remains sole `Current`, `CurrentSession`, `ProviderOrigin` and active engine; A disposed; no stale UI. Test must fail against current unsynchronized transform or prove interleaving impossible under a documented lock. |
| AT-02 check-to-activation Open/Close | Pause A at same post-check point; `Close()` completes; release A. | Session stays closed, no engine activation after Close, no preview binding or candidate leak. |
| AT-03 slow Open/Revert | Start slow A, `RevertToBaseline()` on B, then release A; repeat A paused post-check. | Revert owns a newer epoch; B's reverted baseline stays active; A discarded. Existing PR #60 did not cover this. |
| AT-04 slow Open/write notification | Start slow A, call `MarkWritten` with B's ID (synthetic only), then release A. | New epoch invalidates A or explicitly rejects this operation while opening; no stale baseline or write authority. Original provider writer remains denied. |
| AT-05 parse/activation fault cleanup | Force `OpenSession`, `MakeActive`, snapshot and `Dispose` failures separately, with a disposable spy candidate and old B. | No candidate leak, no partially published new `Current`/binding, consistent old-or-closed state, zero provider writes. Report actual exception types. |
| AT-06 cancellation after parse | Cancel A just after parse and immediately before activation, including cancellation racing with B/Close. | Cancelled A cannot activate or change session; candidate disposed exactly once; cancellation not misreported as successful open. |
| AT-07 multithread stress with invariant | Deterministic seeded interleavings of Open A/B, Close, Revert and preview on actual service; capture invariant at each committed epoch. | `Current` and `CurrentSession` agree with `ProviderOrigin.EngineSession` whenever bound; generations strictly increase on invalidation; no disposed active session or writes. |

## P0/P1 — source, UI and optional import

| Gate | Controlled setup | Required pass evidence |
| --- | --- | --- |
| AT-08 same bytes, replaced provider identity | A/B return identical bytes and document ID but distinct provider generation/identity; compare [SaveDocumentSession](https://github.com/xbaconator9000x/PKHeX-Mobile/blob/bb57c444166515b7406f14d2a38302e503ba88a0/src/PKHeXMobile.Domain/SaveDocumentSession.cs) and actual adapted PKForge preview. | Identity mismatch refused or explicitly classified preview-only/unverified; **never** promoted to Save As. |
| AT-09 stale preview overlay | Complete preview checks, pause overlay display; change slot, document, navigation or Close, then resume. | No preview for A appears under B; no stale `PadMenu` state or live mutation. Test real View/UI lifecycle, not just a source-string assertion. |
| AT-10 optional PKSM multi-import unreachable or bounded | Trigger user UI and direct `PickManyAsync` with limit+1/10,000 fake items and files summing above aggregate budget. | Feature genuinely disabled with no user-reachable importer, **or** pre-allocation count, total-byte, cancellation and read-grant limits enforced; zero writes and bounded retained memory. Previous per-file cap alone fails. |
| AT-11 retail/hack differential on exact Core | Consented genuine retail positive and valid-looking same-size hack negative (private fixture runner, no checked-in saves); compare official desktop Core and adapted engine. | Matching retail parse/serialize/legality verdicts and exact format; lookalike hack refused; synthetic malformed negatives alone insufficient. |
| AT-12 distinct Save As/provider durability | Once implemented, test separate destination, same-document alias, changed original, write interruption, cancelled output, independent reopen/readback, scoped diff and checksums on Android SAF. | Original URI/hash unchanged; no original `rwt`; every failed/aliased/ambiguous output refused, destination bytes independently verified; device/provider matrix recorded. Until then **Save As not certified**. |

## Execution contract

- Prefer extending [ProviderOriginLifecycle.Smoke](https://github.com/xbaconator9000x/PKHeX-Mobile/blob/bb57c444166515b7406f14d2a38302e503ba88a0/tests/PKHeXMobile.ProviderOriginLifecycle.Smoke/Program.cs) and the existing pinned transform; do not create another save parser or generic session mock that cannot exercise PKForge's `MakeActive` boundary. Expose an injectable activation barrier only in a test adapter if needed.
- Every gate must record test name, pinned source SHA, adapted main/PR SHA, command, input classification, assertion count, failure/pass, CI URL and whether it ran on portable synthetic C#, Android emulator or physical Android device. No fabricated execution counts. Preserve the [fail-closed workflow verdict](https://github.com/xbaconator9000x/PKHeX-Mobile/blob/bb57c444166515b7406f14d2a38302e503ba88a0/.github/workflows/pkforge-official-core-probe.yml) and reject skipped stages; run PR and merged-main CI.
- Maintain GPL-3.0-only repository licensing and upstream notices. Do not commit copyrighted ROMs/assets, private saves, signing keys, third-party DLLs without rights verification, or secrets. Do not change README first line `# This is a test.`.
- **Ownership:** audit owns only `docs/UPSTREAM_*` and `docs/PARITY_*`; implementation owns app, src, tests, scripts, workflows and dependencies. These are acceptance gates, not evidence that features exist.

**Immediate implementation priority:** AT-01–AT-07, then AT-10 (or disable importer), then device/real-save gates AT-08–AT-12. Retain all existing read-only write barriers.
