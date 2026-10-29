# Secondary SAF I/O and session provenance: executable acceptance gates (2026-10-29)

Companion to [UPSTREAM_SECONDARY_IO_SESSION_HANDOFF_2026-10-29.md](UPSTREAM_SECONDARY_IO_SESSION_HANDOFF_2026-10-29.md). Baseline: mobile [`a9d18b417fff3d24e1e7823b54d7709e34b495c7`](https://github.com/xbaconator9000x/PKHeX-Mobile/tree/a9d18b417fff3d24e1e7823b54d7709e34b495c7), pinned PKForge `2df04430e532b78c247f45b936f3068d8f824987`, PKHeX MAUI `a6f7cfb78e1c96155baa67660fd7ae97895e375f`, official PKHeX Core release `bfe89c74a3f5265805e364b79bc04ad38ebbc5ea` / package `26.10.18`.

**Status:** 16 **proposed and unexecuted** new regression gates. They do not supersede the already-passing PR #54 bounded single-save read smoke or PR #55 single-picker lifecycle tests. Tests must execute against the exact transformed PKForge checkout, not just textual patterns. No personal saves, ROMs, keys or proprietary art in the repo.

## Reuse existing harness

- [`SaveInputSnapshot`](https://github.com/xbaconator9000x/PKHeX-Mobile/blob/a9d18b417fff3d24e1e7823b54d7709e34b495c7/src/PKHeXMobile.Domain/SaveInputSnapshot.cs) for 32 MiB capped immutable bytes and fingerprints.
- [`SaveDocumentSession`](https://github.com/xbaconator9000x/PKHeX-Mobile/blob/a9d18b417fff3d24e1e7823b54d7709e34b495c7/src/PKHeXMobile.Domain/SaveDocumentSession.cs) for provider identity and independent reopen.
- [`ReadOnlySaveFileAccess` adaptation](https://github.com/xbaconator9000x/PKHeX-Mobile/blob/a9d18b417fff3d24e1e7823b54d7709e34b495c7/scripts/pkforge_source_read_hardening.py), [`DetachedRetailNicknameEditor`](https://github.com/xbaconator9000x/PKHeX-Mobile/blob/a9d18b417fff3d24e1e7823b54d7709e34b495c7/src/PKHeXMobile.PortConsolidation/DetachedRetailNicknameEditor.cs), [`PKHeXMobile.BoundedSource.Smoke`](https://github.com/xbaconator9000x/PKHeX-Mobile/tree/a9d18b417fff3d24e1e7823b54d7709e34b495c7/tests/PKHeXMobile.BoundedSource.Smoke) and existing CI compatibility probe. Do not introduce a parallel parser.
- A fake `IFolderFileAccess` with separate counters for `ReadFileAsync`, `WriteFileAsync`, `ListFilesAsync`; a fake picker returning configurable Android flags; fake provider with independent opens, optional identity, delays, cancellation and revocation. For actual SAF grants and Android activity lifecycle, use instrumentation/device tests and record API level/provider.

## P0: independently writable folder service (implement first)

| ID | Reproducible setup/action | Required result and evidence |
| --- | --- | --- |
| SI-01 separate writer DI | Resolve `ISafeSaveWriter`, `ISaveFileAccess` and `IFolderFileAccess` from the adapted MAUI service provider. Call all three write methods against spies. | Every write refuses **before** any resolver output stream or file creation. `IFolderFileAccess.WriteFileAsync` cannot bypass save writer denial; spy counts remain zero. Test actual DI graph. |
| SI-02 folder picker least privilege | Invoke `PickFolderAsync` on Android with a fake activity/intent spy. Return read+persistable+write flags. | Requested and persisted folder grants contain **read only**, no write; missing read grant fails. No permission escalation. Device confirmation needed for actual SAF. |
| SI-03 existing-name overwrite | Place a sentinel `manifest.json` and `example.bnk` in a fake folder; invoke `BankArchive.ExportAsync` and `PksmTransferFlow.ExportAsync` through adapted services. | In preview-only mode neither sentinel changes, no new child appears, no `OpenOutputStream(...,"wt")` invocation. Distinguish app-internal bank changes from SAF writes. |
| SI-04 bounded folder import | Fake `IFolderFileAccess.ReadFileAsync` streams 32 MiB + 1 byte, empty, non-seekable, partial read and cancellation; test bank/archive import route. | Oversize/empty refuse before entity parsing; all streams closed; bounded memory, zero folder writes. Exact cap and container-specific size requirements documented. |
| SI-05 alternative file access | Enumerate DI registration and source-level references to `AndroidFolderFileAccess`, `AndroidSafFileAccess`, `IFolderFileAccess`, `ISaveFileAccess`, and any direct `ContentResolver.OpenOutputStream/OpenFileDescriptor` in the transformed checkout. | Every user-accessible write path classified: denied in preview, separately gated export, or unreachable with proof. An unreviewed direct writer fails the gate. |
| SI-06 folder import-only usability | Use a bounded valid synthetic PKSM bank archive, fake read-only folder and revoked permission; test import and cancellation. | Read-only import works when supported; revocation fails without mutation, with explicit user feedback. No accidental promise that bank import authenticates retail saves. |

## P0: immutable source and session ordering

| ID | Reproducible setup/action | Required result and evidence |
| --- | --- | --- |
| SI-07 concurrent open inversion | Start opening A, block its first or second provider read; fully open B; then release A. Repeat blocking A after parse but before activation. | B remains current and visible; A is disposed/discarded. Latest generation wins; no stale `MakeActive`, no late UI refresh. |
| SI-08 close/revert inversion | Start slow open A; call Close or RevertToBaseline while A is pending; then release A. Also start preview A and close/revert during edit. | Close/revert invalidates old generation, no resurrected session or preview; original bytes remain immutable. |
| SI-09 identity A/B with same bytes | First and second opens produce identical bytes but different opaque document IDs, or identity present then absent. | Session activation refused, old session preserved or explicitly cleared. Matching fingerprint alone is insufficient. |
| SI-10 provider source A/B same URI | Provider returns bytes A twice and later bytes B at the same URI, including changes after a successful preview. | Before next preview or export, independent reopen detects B and invalidates A; no stale candidate or publication. |
| SI-11 normalization trap | Construct an engine session whose `Serialize()` differs from the provider's original immutable snapshot before user edits. Invoke nickname preview. | Preview receives original provider bytes or refuses; never uses normalized session serialization as its source baseline. |
| SI-12 stale preview result | Start nickname preview on A, change selection to B (same URI/different bytes, or different URI), then complete A. | A's overlay never appears under B; generation, fingerprint, source identity and slot are rechecked after asynchronous work. |
| SI-13 failed/cancelled open | Inject engine parse failure, revoked read grant, cancelled second open, and cancellation immediately before `MakeActive`. | No leaked/disposed-current session, no activation of cancelled source, no retained stale identity-confirmed flag; explicit error state. |
| SI-14 retail vs hack provenance | Test a known-positive retail fixture and valid-looking CFRU/Radical Red/Unbound lookalikes of matching length and container padding using official Core 26.10.18. | Only independently authenticated retail layout is eligible for eventual edit/export; ambiguous/hack remains preview-only. Negative malformed bytes alone do not pass. Record same-version desktop outputs and hashes without uploading private fixtures. |

## P1: independent multi-picker lifecycle

| ID | Reproducible setup/action | Required result and evidence |
| --- | --- | --- |
| SI-15 multi-picker cancellation | Call `PickManyAsync` with a pre-cancelled token; cancel after launch; deliver late Android result; start another picker. | No stale results rebinding, no hung task, pending state released safely, no unexpected URI grants or retained references. Test single and multi picker interleaving. |
| SI-16 multi-import grant/count | Return no read flag, read-once only, revoked read and very large `ClipData` count. Use fake import provider and real transformed picker. | Missing read denied, read-once allowed only for immediate bounded import, explicit batch cap/feedback, cancellation and resource cleanup; zero writes. |

## Evidence, pass criteria and integration order

For every gate, record **test class/method, exact mobile and upstream SHAs, official Core version, command, assertions, CI URL and result**. A test *specified* in this document is not a test *passed*. Separate (a) portable synthetic tests, (b) Android instrumentation against fake/real SAF providers, (c) same-version desktop comparison on consented fixtures, and (d) signed physical-device release tests.

Suggested implementation batch: (1) disable/deny the independent folder writer and write grants (SI-01–05), (2) provider-bound session generation and detached preview (SI-07–13), (3) disable multi-import until SI-15/16 pass or adapt it narrowly, (4) positive retail/hack discrimination and independently verified distinct-destination Save As. **Do not enable any original-URI write path.**

The [PR #55 official-Core probe](https://github.com/xbaconator9000x/PKHeX-Mobile/actions/runs/40763529850) and [main foundation checks](https://github.com/xbaconator9000x/PKHeX-Mobile/actions/runs/40764005050) passed **before** these gates were written; they are not evidence of passing SI-01–16. The 30-productive-hour goal is aspirational, not a guarantee of full desktop parity. The implementation stream owns app/source/tests/CI changes; this audit owns only `docs/UPSTREAM_*` and `docs/PARITY_*`.
