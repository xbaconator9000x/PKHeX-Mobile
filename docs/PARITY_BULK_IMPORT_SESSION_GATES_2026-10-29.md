# Bulk-import and provider-bound session execution gates (2026-10-29)

Companion to [UPSTREAM_BULK_IMPORT_SESSION_HANDOFF_2026-10-29.md](UPSTREAM_BULK_IMPORT_SESSION_HANDOFF_2026-10-29.md). Audited baseline: mobile [`5f3f371dd8fba521d148296ef3432afaa0764ac5`](https://github.com/xbaconator9000x/PKHeX-Mobile/tree/5f3f371dd8fba521d148296ef3432afaa0764ac5), pinned PKForge `2df04430e532b78c247f45b936f3068d8f824987`, PKHeX MAUI `a6f7cfb78e1c96155baa67660fd7ae97895e375f`, official Core `bfe89c74a3f5265805e364b79bc04ad38ebbc5ea` / package `26.10.18`.

**Status:** All **12 gates below are proposed, unexecuted**. The [PR #57 compatibility probe](https://github.com/xbaconator9000x/PKHeX-Mobile/actions/runs/40771346458) passes *existing* folder containment, per-file bounded reads, detached synthetic nickname and MAUI compile; it does **not** execute these new aggregate, session ordering or device gates. Do not count this document as tested functionality.

## P0: aggregate multi-file import / lifecycle

| Gate | Deterministic setup and action | Pass condition / evidence |
| --- | --- | --- |
| BI-01 count refusal before URI allocation | Fake `ClipData` with 0, 1, limit, limit+1 and 10,000 entries; invoke transformed `HandleMultiResult` or disabled feature entry. | Excess refused **before** allocating `List<PickedDocument>` or querying names; bounded allocations; no stale pending picker. If feature disabled, demonstrate no user-reachable `PickManyAsync` call. |
| BI-02 aggregate byte budget | Fake `ISaveFileAccess` returning N individually valid ~32 MiB blobs; run actual adapted `PksmTransferFlow.ImportAsync` or its extracted import coordinator. | Count and **aggregate** byte budgets enforced before `files.Add`/decode. Limit+1 refuses, previously retained arrays released, no bank changes, no write calls. Report peak retained bytes/allocations; per-file cap alone fails. |
| BI-03 cancellation and late result | Pre-cancelled `PickManyAsync`, cancellation after launch, late Android result, second pick and single/multi interleaving. | No hung task, no late rebinding, pending state released safely; no grants persisted for cancelled result; no new picker consumes old result. |
| BI-04 grant and provider failure | Return URI without read grant, read-once only, revoked read, null stream and one file that changes between independent opens. | Missing grant refused; read-once accepted only for immediate bounded import if supported; revoked/changed source refuses and discards batch. All streams disposed; zero writes. |
| BI-05 bank import transaction | Valid synthetic PKSM bank + invalid sibling, user cancels confirmation, bank Apply throws after partial plan, and duplicate/oversize files. | No partial bank mutation on cancellation or rejected plan; if Apply is non-atomic, rollback or disable import. Separate internal bank state from Android SAF writes; assert both. |

## P0: source/session generation and preview

| Gate | Deterministic setup and action | Pass condition / evidence |
| --- | --- | --- |
| BI-06 out-of-order opens | Block provider read for A; fully open B; release A. Repeat blocking A after engine parse but before activation. | B remains current and active; A disposed without `MakeActive`; no stale UI refresh or old document identity. Use controlled TaskCompletionSources, not sleeps. |
| BI-07 close/revert while opening | Start slow A; call `Close()` or `RevertToBaseline()`; release A. Repeat with cancellation immediately after parse. | Close stays closed, revert keeps chosen baseline, A never reactivates; no leaked candidate or stale identity-confirmed flag. |
| BI-08 same URI identity and bytes | Fake provider A/B same URI, equal bytes but different opaque identity; then same identity but bytes change after verification. | First mismatch rejected on identity; second rejected on fingerprint/reopen; matching two reads is not a retail-layout authentication. No live session replacement. |
| BI-09 source-vs-serialization trap | Provide original bytes O whose engine session `Serialize()` produces N (normalization) before any user edit. Trigger nickname preview. | Preview uses immutable provider O or refuses; never treats N as original. Check captured fingerprint and selected slot. |
| BI-10 stale preview/overlay | Begin preview for A; change document, box, slot or nickname, close/revert, or cancel while overlay awaits; finish old preview. | Old overlay never displays as B; selection generation and original fingerprint checked after each await; no live mutation and no original file write. |
| BI-11 authentic retail versus hack lookalike | Same official Core 26.10.18 on desktop and adapted mobile: consented positive retail save and valid-looking CFRU/Unbound/Radical Red lookalikes of same length/container; do not commit fixtures. | Record format decision, hashes, official-Core parse and serialize comparisons, permitted edit scope and all refusals. Unknown/hack remains preview-only. Malformed-only negatives do not pass. |

## P1: folder enumeration resource budget

| Gate | Deterministic setup and action | Pass condition / evidence |
| --- | --- | --- |
| BI-12 folder list cap/cancellation | Fake `DocumentsContract` cursor with 0, limit, limit+1, 100,000 entries and mid-enumeration cancellation, including provider exceptions. | Enumerate at most configured limit; cancel inside loop; close cursor; refuse oversized folder with clear feedback. If folder import disabled, prove entry point unreachable and no enumeration. |

## Execution harness and release evidence

- Reuse [`SaveInputSnapshot`](https://github.com/xbaconator9000x/PKHeX-Mobile/blob/5f3f371dd8fba521d148296ef3432afaa0764ac5/src/PKHeXMobile.Domain/SaveInputSnapshot.cs), [`SaveDocumentSession`](https://github.com/xbaconator9000x/PKHeX-Mobile/blob/5f3f371dd8fba521d148296ef3432afaa0764ac5/src/PKHeXMobile.Domain/SaveDocumentSession.cs), [`DetachedRetailNicknameEditor`](https://github.com/xbaconator9000x/PKHeX-Mobile/blob/5f3f371dd8fba521d148296ef3432afaa0764ac5/src/PKHeXMobile.PortConsolidation/DetachedRetailNicknameEditor.cs), existing source and folder provider-spy tests, and the exact temporary PKForge checkout. **Do not create a second save parser.**
- For every test record method name, input class/size/count, source pin and adaptation SHA, exact command, assertions, failure mode, CI URL and result. Label portable synthetic tests separately from Android instrumentation and physical-device evidence.
- Add CI verdict that fails on **any** skipped/failing compatibility stage. `continue-on-error` steps must be checked by the final verdict. Compile success is not device execution, and an empty or skipped test is not success.
- Verify [PKForge GPLv3](https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/LICENSE), [PKHeX MAUI GPLv3](https://github.com/santacrab2/PKHeXMAUI/blob/a6f7cfb78e1c96155baa67660fd7ae97895e375f/LICENSE.txt), [official Core GPL-3.0-or-later](https://github.com/kwsch/PKHeX/blob/bfe89c74a3f5265805e364b79bc04ad38ebbc5ea/PKHeX.Core/PKHeX.Core.csproj), file notices, assets and transitive dependencies before copying code into the product. No ROMs, personal saves, keys, copyrighted art or signing credentials.
- **Release gates remain separate:** verified genuine retail positive fixtures; valid-looking hack negatives; independent distinct-destination Save As readback/alias and original-source race checks; real Android SAF provider/device behavior; signed reproducible APK; feature-by-feature desktop parity. None are certified here.

**Implementation order:** (1) disable or cap multi-file import; (2) generation-bound source activation and close/revert; (3) provider-original detached preview with cancellation; (4) verified retail/hack differential; (5) independently validated distinct Save As; (6) selective desktop parity. No in-place original document writes.

**Ownership:** this audit changes only `docs/UPSTREAM_*` and `docs/PARITY_*`. All app, script, test, workflow and dependency implementation belongs to the parallel implementation stream.
