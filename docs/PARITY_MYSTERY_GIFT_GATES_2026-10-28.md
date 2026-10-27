# Mystery Gift, stored-card and event archive acceptance gates — pass H (2026-10-28)

**Planned only: 27 cases, zero executed in this audit.** Every case traces to a source finding in [pass H](UPSTREAM_MYSTERY_GIFT_REVIEW_2026-10-28.md). Pinned source: PKHeX `7e26bc53215e4f7bc0041ca375f1042539cb81b3`; PKForge `2df04430e532b78c247f45b936f3068d8f824987`; MAUI `a6f7cfb78e1c96155baa67660fd7ae97895e375f`; mobile baseline `2e3e1fc47fc29f9a573bfe6a988892e66928aea1`. The app's PKHeX.Core **26.10.18** package must be compared with a matching desktop Core, not assumed equivalent to the newer inspected source.

| Gate | Finding(s) | Priority | Reproducible action and expected outcome | Required evidence | Status |
| --- | --- | --- | --- | --- | --- |
| HG-01 | H-01 | P0 | Synthetic WA9 clone: runtime class, context, extension and serialized bytes remain WA9-equivalent; if pinned Core fails, record upstream defect and disable clone-dependent ZA workflows. | unit/desktop | not executed |
| HG-02 | H-02,H-03 | P0 | For all 15 explicit extension dispatch arms, test valid synthetic sizes/extensions and wrong-extension rejection; do not equate size with validity. | unit | not executed |
| HG-03 | H-02 | P0 | Same-length WA8/WC9/WA9 inputs exercise heuristic discriminator bytes 0xF and 0x2C0; mismatches return ambiguous/unsupported, never authorized. | fuzz/unit | not executed |
| HG-04 | H-02 | P1 | WC6/WC7 same-size and full-container heuristics exercise year and 0x205 discriminators, with independent desktop comparison. | unit/desktop | not executed |
| HG-05 | H-03 | P1 | Random, all-zero and adversarial same-size cards are never treated as trusted distribution or write-safe save. | negative unit | not executed |
| HG-06 | H-04,H-20 | P1 | Per save type Gen4/5/6/7/7b/8/8a/8b/9/9a: distinguish file parser, stored inbox, received flags and mutation support; unsupported explicit. | capability matrix | not executed |
| HG-07 | H-05 | P0 | HGSS Lock Capsule at special 12th slot is preserved across no-op and isolated edit; other Gen4 saves refuse that slot. | desktop fixture | not executed |
| HG-08 | H-05 | P1 | PCD-to-PGT conversion, incompatible slot types and interstitial empty slots yield deterministic targets with no accidental card replacement. | unit/desktop | not executed |
| HG-09 | H-06 | P0 | Gen5 encrypted mystery block no-op and single-card change survive independent reopen; checksum/at-rest bytes match same-version desktop. | private fixture | not executed |
| HG-10 | H-07 | P0 | Received flags and album slots diff separately; clearing one flag does not clear unrelated flags; failure rolls back both. | fault injection | not executed |
| HG-11 | H-08 | P0 | Cancel after card/flag edits leaves original snapshot and document hash unchanged; stale provider revision prevents publication. | fault injection | not executed |
| HG-12 | H-09 | P0 | Instrument MAUI View/Set/Delete tapped sender and target slot on Android; invalid cast/-1 cannot mutate a gift. | device | not executed |
| HG-13 | H-09,H-12 | P1 | First and last slot, empty slot, 200% font and portrait rotation preserve tap mapping and focus descriptions. | device | not executed |
| HG-14 | H-10 | P1 | Import from local, cloud and revoked Android content URI; cancellation and permission errors are explicit, no path-based assumption. | device | not executed |
| HG-15 | H-11 | P1 | View used card without consent, dismiss prompt, navigate away mid-prompt: no mutation, stale selection or delayed write. | lifecycle | not executed |
| HG-16 | H-13 | P1 | Parallel archive A/B indexing cannot relabel A's cards with B's source paths; manifest revision captured. | concurrency | not executed |
| HG-17 | H-13,H-17 | P1 | Synthetic duplicate-content and deliberately collision-simulated keys cannot merge different cards or change stable selected ID. | unit | not executed |
| HG-18 | H-14 | P0 | Occupied destination and destination changed after preview: Receive refuses and preserves full save bytes. | fault injection | not executed |
| HG-19 | H-14,H-20 | P0 | Unsupported CFRU-like save with a parseable gift cannot be written despite legal generated entity. | negative fixture | not executed |
| HG-20 | H-15 | P0 | Multi-item gift with one full pouch, invalid item or overflow does not partially publish or silently skip an item. | fault injection | not executed |
| HG-21 | H-15 | P1 | Successful multi-item gift independently reparses all pouch quantities and preserves unrelated bag bytes. | desktop fixture | not executed |
| HG-22 | H-16 | P1 | ZA Gen9a card/album request returns unsupported until dedicated profile; no Gen9 fallback. | unit | not executed |
| HG-23 | H-17 | P1 | Gallery refresh after preview, changed raw order, duplicate title/card ID: stale selection refuses apply. | concurrency | not executed |
| HG-24 | H-18 | P1 | Two cards with same sanitized name, preexisting output and full-disk/provider failure: unique names, no overwrite, readback hashes. | fault injection | not executed |
| HG-25 | H-19 | P0 | For each claimed stored-card family compare read-only slot ID, card type, used flag and received flags against independent matching-Core desktop output. | desktop fixture | not executed |
| HG-26 | H-20 | P1 | Audit every proposed card sprite, font and gallery distribution file for copyright, provenance, licence and export rights before bundling. | licence audit | not executed |
| HG-27 | H-01,H-06,H-14,H-15 | P0 | No save writer enabled without authenticated layout, staged output, byte-preserving backup, rollback and independent same-version desktop round-trip. | safety gate | not executed |

## Execution and stop rules

1. Record exact upstream source SHA, installed Core package hash/version, .NET/Android SDK, fixture provenance and SHA-256, command, exit code, test runner and CI URL. Keep all actual saves, ROMs, gallery collections, signing material and private document URIs **outside the repository**.
2. Prefer synthetic cards and blank-save objects for unit tests. Use rights-cleared, private, independently produced desktop fixtures for claimed save-format behavior; test both successful and negative layouts. Source inspection alone never passes a gate.
3. For every mutation attempt, compare source and staged SHA-256, expected card/flag/pouch/slot deltas, unrelated bytes, checksum and wrapper metadata. Reopen with independent same-version desktop PKHeX and prove exact original-byte backup restoration after injected failures.
4. For Android tests, include content URI providers, cancellation, process recreation, TalkBack, font scaling, occupied targets and stale preview. Avoid FullPath assumptions and record provider-specific limitations.
5. **Hard stop:** no export/write of a save, including ROM hacks, until authenticated layout and version, complete staging/backup/rollback, independent desktop round-trip and concurrency/fault gates pass. Gift legality, file extension, user risk acknowledgment and card size are not write authorizations.

**Status accounting:** 27 planned; **0 executed / 0 passed / 0 certified**. Milestones 0 and 1 remain open.
