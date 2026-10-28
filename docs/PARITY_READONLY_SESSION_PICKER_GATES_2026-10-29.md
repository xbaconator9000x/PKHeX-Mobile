# Read-only Android picker and denied-mutation parity gates (2026-10-29)

**Companion:** [UPSTREAM_READONLY_SESSION_PICKER_REVIEW_2026-10-29.md](UPSTREAM_READONLY_SESSION_PICKER_REVIEW_2026-10-29.md). Source baseline: [PKHeX Mobile `8cc94a06ab90ef8fc2e962e42f5da90193df3923`](https://github.com/xbaconator9000x/PKHeX-Mobile/tree/8cc94a06ab90ef8fc2e962e42f5da90193df3923). Upstreams: PKForge `2df04430e532b78c247f45b936f3068d8f824987`, PKHeX MAUI `a6f7cfb78e1c96155baa67660fd7ae97895e375f`, official PKHeX.Core 26.10.18 source `bfe89c74a3f5265805e364b79bc04ad38ebbc5ea`.

**Status legend:** `E` = already executed and verified in existing CI; `N` = new acceptance test, **not executed by this audit**; `D` = Android device/provider test, not yet executed. A green compilation is never evidence that a provider grant, UI command, rollback or Save As gate passed.

## Existing verified foundation (not new passes)

- `E0`: [CI 40745089981](https://github.com/xbaconator9000x/PKHeX-Mobile/actions/runs/40745089981) compile-probe job's individual steps all succeeded: pinned checkout, official-Core engine, synthetic retail refusal, detached nickname, MAUI preview injection, DI writer/provider refusal smoke and Android compilation.
- `E1`: [CI 40745641338](https://github.com/xbaconator9000x/PKHeX-Mobile/actions/runs/40745641338) main foundation checks succeeded. It is not an Android provider test.
- Existing [writer/provider spy](https://github.com/xbaconator9000x/PKHeX-Mobile/blob/8cc94a06ab90ef8fc2e962e42f5da90193df3923/tests/PKHeXMobile.Containment.Smoke/Program.cs) rejects `WriteAsync`, `WriteScopedAsync` (including unrestricted), and `ISaveFileAccess.WriteAtomicallyAsync`. It does not exercise `IDocumentPicker`, `BoxBrowserViewModel` mutation paths or Android `ContentResolver`.

## P0: grant and source-document protection

| Gate | Harness and controlled stimulus | Required observable result |
| --- | --- | --- |
| **P01 N** | Transform pinned [AndroidDocumentPicker.cs](https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/src/PKForge.App/Platforms/Android/AndroidDocumentPicker.cs) in a temporary checkout; assert exactly one single-file `ACTION_OPEN_DOCUMENT` grant mask and no duplicate/unknown anchors. | Single picker requests `GrantReadUriPermission | GrantPersistableUriPermission` only; any `GrantWriteUriPermission` in its request fails the test. Multi-picker remains read-only. |
| **P02 N** | Feed synthetic picker-result flags (read, write, read+write, neither) into a pure permission-mask helper, with a fake persist callback. | Persist **read only** if present, reject missing read; never persist write even when returned. Invalid/cancelled results cannot acquire permission. |
| **P03 D** | Android instrumentation: pick a source file through a read-only test `DocumentsProvider`, capture requested and persisted grants via platform APIs. | Provider sees read-only request; persisted URI permissions contain read but no write, including after app restart. |
| **P04 D** | Provider returns read-only URI, denies writable file descriptors, or revokes permission between first and second reads. | Preview either reads safely or refuses without modifying source, opening `rwt`, or claiming successful Save As. |
| **P05 N** | Static search of the *adapted pinned* app for `new AndroidSafFileAccess`, `ContentResolver.OpenFileDescriptor`, `OpenOutputStream`, `File.OpenWrite`, `WriteAtomicallyAsync` and other publishing routes, then review each hit. | Every source-document writer is unreachable or explicitly denied; exceptions must be justified individually. DI spy alone is not sufficient evidence. |

## P0: denied mutation must not contaminate the live session

| Gate | Harness and controlled stimulus | Required observable result |
| --- | --- | --- |
| **P06 N** | Open a synthetic official retail fixture in PKForge `SaveSessionService`; capture baseline bytes and slot summaries; call `BoxBrowserViewModel` move/drop with read-only writer. | Denied move returns to the **original** serialized bytes, selected slot, party/box summaries and generation; no `MarkWritten`, source URI write or queued dirty session. This test should **fail against the unadapted upstream catch path**, establishing discrimination. |
| **P07 N** | Repeat P06 with occupied→empty, occupied→occupied swap, same-slot no-op, party↔box and multiple rapid move requests; inject writer rejection after serialization. | All failed moves roll back; concurrent tasks do not resurrect stale candidate bytes, leak semaphore permits, or leave an in-flight write that can later succeed. |
| **P08 N** | Invoke `SaveEditCommand`, `RunMutationAsync`, bulk move/release, sub-editor, gamepad/keyboard and any other exposed mutation commands under explicit preview-only mode. | Every command refuses **before** mutating the live session. Save changes disabled in the view is not accepted as the only guard. |
| **P09 N** | Use a fake writer that throws both `UnsafeSaveWriteException` and generic `IOException` at each mutation route, with a fake read-only file-access spy. | Original and in-memory baseline hashes remain identical after refusal; UI reports *not saved*; no phantom success or backup claim. |
| **P10 N** | Mutate a scratch session, call `RevertToBaseline` and reselect; compare to upstream [SaveSessionServiceTests](https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/tests/PKForge.Engine.Tests/SaveSessionServiceTests.cs). | Reopened session is a different object, byte-identical to the immutable original, and discarded edits do not appear in the next preview. |
| **P11 N** | Start detached nickname preview, then change selected slot, box, document and in-memory mutation generation before displaying result. | Preview remains bound to captured slot/source generation; stale result is discarded; `Cancel` performs no mutation or provider write. |
| **P12 D** | Android emulator/physical device: navigate editor with touch, Back and gamepad; trigger Save changes, move, bulk tools and preview cancellation. | No crash, no source writes, no dirty live session, no stale slot preview; original provider hash unchanged before/after. |

## P0: authenticated future Save As (must remain disabled until all pass)

| Gate | Harness and controlled stimulus | Required observable result |
| --- | --- | --- |
| **P13 N** | Independently capture immutable original bytes + source document ID, provider metadata and fingerprint; read twice before preview, interleave external source replacement. | Reject mismatch, ambiguous format or stale source; `session.Serialize()` is never accepted as on-disk source authority. |
| **P14 D** | Choose destination via separate `ACTION_CREATE_DOCUMENT`; test same URI, provider alias, different URI same backing document, cancelled picker, unsupported MIME and revoked destination. | Same/aliased source refused; original never opened writable; cancelled/revoked destination does not claim a saved file. |
| **P15 N+D** | On synthetic, authenticated positive retail fixtures, edit exactly one supported slot; independently re-open destination and source; inject short write, provider crash, stale source, invalid checksum, changed adjacent slot and interruption. | Success only if destination bytes and intended scope match and source remains unchanged; partial destination is clearly reported and recoverable. No unverified hack writes. |
| **P16 N** | Run same pinned official Core and desktop PKHeX against independently supplied/consented positive retail fixtures and adversarial valid-looking hack fixtures; compare parsing, legality, round-trip and allowed diff. | Per-format positive **and** discriminating negative evidence required before enabling export for that format; synthetic invalid files alone are insufficient. |

## Minimum next implementation PR (one cohesive batch)

1. Keep PKForge UI and official Core. Adapt only the **single-save picker grant** and persisted read mask (P01–P04), with strict pinned-source anchors and negative tests.
2. Add a central **preview-only mutation guard** ahead of live `ApplyEdit`, `MoveSlot`, bulk and gamepad operations. Test the known upstream denied-move rollback gap with a *failing-before/fixed-after* regression (P06–P11). Prefer disabling live mutations entirely in this read-only phase rather than complex concurrent rollback.
3. Add CI smoke that actually invokes the pinned `BoxBrowserViewModel` with a fake provider; do not rely on string assertions or `ReadOnlySaveWriter` unit tests alone. Make all gate verdicts explicit; no `continue-on-error` without a final failing verdict.
4. Only after these pass, implement provider-bound preview and separate Save As (P13–P16). Keep unknown formats and hacks read-only. Do not expose `Save As` as functional until its positive fixtures and Android provider matrix pass.

**Execution statement:** P01–P16 are proposed tests, **not passed**. This audit performed source/CI inspection only, made no Android runtime claims, and changed only audit-owned documentation. The 30-productive-hour delivery target is aspirational, not an acceptance criterion that overrides safe export.
