# Storage, party, box-pointer and bulk-operation acceptance gates — pass N (2026-10-28)

**42 proposed scenarios; zero executed or certified by this audit.** Each is a reproducible implementer ticket linked to [source findings](UPSTREAM_STORAGE_SLOT_REVIEW_2026-10-28.md). Do not treat synthetic tests, build success or source review as desktop-matched parity.

## Test protocol

1. Record exact source and package SHAs, toolchain, game/layout revision, rights-cleared synthetic or consented fixture provenance, original SHA-256 and expected slot/pointer maps. Never commit user saves, ROMs or commercial assets.
2. Obtain **independent** reference observations from desktop PKHeX built against the same 26.10.18 source release; record desktop binary hash. Do not generate both expected and observed manifests from the mobile adapter.
3. Run each command on an isolated clone; check all slot bytes, party count, team/lock pointers, names, wallpapers, untouched-region hashes and intended changes. Test both success and injected failure after every mutation step.
4. Before any Save As, authenticate exact layout, independently reopen candidate, validate checksums and preserve unmodified bytes. Before overwrite, prove durable restorable backup, fresh-source fingerprint, provider readback, cancellation recovery and no partial publish.
5. Track each gate as proposed, synthetic-executed, desktop-matched, Android-provider-executed or certified for one named profile. **All rows below remain proposed.**

## Gate matrix

| ID | Priority | Reproducible scenario and required outcome | Findings | State |
| --- | --- | --- | --- | --- |
| SG-01 | P0 | Pointer swap adjacent boxes with a team pointer; pointer must follow the same entity | N01,N20 | Proposed / unexecuted |
| SG-02 | P0 | Pointer swap nonadjacent boxes with duplicate pointers; no stale indices | N01 | Proposed / unexecuted |
| SG-03 | P0 | Pointer move forward/backward with linked team slots; expected pointer remap | N02 | Proposed / unexecuted |
| SG-04 | P0 | MoveBox 0→1 on distinct A,B: output must be B,A without duplication | N03 | Proposed / unexecuted |
| SG-05 | P0 | MoveBox last→first and middle→last: lossless permutation of all slot hashes | N03 | Proposed / unexecuted |
| SG-06 | P0 | MoveBox involving locked region: refusal and exact unchanged bytes | N04 | Proposed / unexecuted |
| SG-07 | P0 | SortBoxes protected slot: exact protected bytes and pointers unchanged | N05 | Proposed / unexecuted |
| SG-08 | P0 | Direct box setter into protected empty slot: refused | N06,N08 | Proposed / unexecuted |
| SG-09 | P0 | SetBoxData mixed protected/unprotected: no unacknowledged partial commit | N07 | Proposed / unexecuted |
| SG-10 | P1 | NextOpenBoxSlot full, empty and protected-empty scenarios | N08 | Proposed / unexecuted |
| SG-11 | P1 | Sparse PartyData no-op: preserve physical members and dex/record fields | N09,N12 | Proposed / unexecuted |
| SG-12 | P1 | SetPartySlotAtIndex blank first/middle/last: expected count and all slots | N10 | Proposed / unexecuted |
| SG-13 | P1 | DeletePartySlot at each of six positions: exact compact order and tail | N11 | Proposed / unexecuted |
| SG-14 | P1 | Two concurrent saves with differing import policy: no global contamination | N12 | Proposed / unexecuted |
| SG-15 | P1 | Large legitimate box paging: bounded memory, cancellation and stale UI | N13 | Proposed / unexecuted |
| SG-16 | P1 | ClearBoxes interrupted after each deletion: candidate rollback | N14 | Proposed / unexecuted |
| SG-17 | P1 | CompressStorage with linked team and erased tails: pointer and byte integrity | N15 | Proposed / unexecuted |
| SG-18 | P1 | LoadBoxes clear-first with zero compatible entities: unchanged original | N16 | Proposed / unexecuted |
| SG-19 | P1 | LoadBoxes valid then malformed: no partial import publication | N16 | Proposed / unexecuted |
| SG-20 | P1 | Two exported entities with identical filenames: both accounted for | N17 | Proposed / unexecuted |
| SG-21 | P1 | Decrypted party-format export versus raw box backup: explicit format identity | N18 | Proposed / unexecuted |
| SG-22 | P1 | StorageSlotSource party/battle/starter/locked combinations: correct flags | N19 | Proposed / unexecuted |
| SG-23 | P1 | BDSP locked and unlocked team slots: pointer/flag consistency | N20 | Proposed / unexecuted |
| SG-24 | P1 | Desktop layout edit Cancel/Save: only Save changes origin | N21 | Proposed / unexecuted |
| SG-25 | P1 | Gen 2–9 box names, wallpaper, fixed art and unlock capabilities match desktop | N22,N23 | Proposed / unexecuted |
| SG-26 | P0 | Forge duplicate into protected-empty slot: no write | N24 | Proposed / unexecuted |
| SG-27 | P0 | Forge box swap failure on second write: both slots restored | N25 | Proposed / unexecuted |
| SG-28 | P0 | Forge party→box displaced member and full party: atomic refusal | N25 | Proposed / unexecuted |
| SG-29 | P0 | Forge GZip bundle oversized, negative length, truncated count: bounded refusal | N26 | Proposed / unexecuted |
| SG-30 | P0 | Forge living-dex malformed second entity: no partial first placement | N26 | Proposed / unexecuted |
| SG-31 | P1 | Forge sort with duplicate selected box and locked slot: no duplication | N27 | Proposed / unexecuted |
| SG-32 | P1 | Forge bulk cancel after every entry: baseline restored | N28 | Proposed / unexecuted |
| SG-33 | P0 | Risk confirmation for unsupported hack: still no export permission | N29 | Proposed / unexecuted |
| SG-34 | P0 | MAUI drag swap second write throws: both original slots intact | N30 | Proposed / unexecuted |
| SG-35 | P0 | Remote send succeeds/local write fails and inverse: no false success | N31 | Proposed / unexecuted |
| SG-36 | P1 | MAUI action sheet on A then document B opens: stale party edit refused | N32 | Proposed / unexecuted |
| SG-37 | P0 | MAUI batch empty instruction and protected slot: zero writeback | N33 | Proposed / unexecuted |
| SG-38 | P1 | MAUI random team with 0–5 free slots: atomic refusal | N34 | Proposed / unexecuted |
| SG-39 | P1 | MAUI share duplicate filename and canceled share: no false receipt | N35 | Proposed / unexecuted |
| SG-40 | P1 | Mobile slot flags, team membership, box layout compared to independent desktop | N36 | Proposed / unexecuted |
| SG-41 | P1 | GPL notices, asset provenance and dependency rights independently verified | N37 | Proposed / unexecuted |
| SG-42 | P0 | Authenticated candidate reopen, raw diff, backup readback and rollback before any write | N01,N03,N06,N25 | Proposed / unexecuted |

## Prioritized implementation sequence

**First: blocking P0 cases** SG-01–SG-09, SG-26–SG-30, SG-33–SG-35, SG-37 and SG-42. Reproduce the two Core pointer errors and MoveBox permutation on pinned source in an isolated test harness; check whether a later official release has a fix before upgrading. No source patch, API use or ROM-hack override should be enabled without independent fixtures.

**Second: metadata and read-only coverage** SG-10–SG-25, SG-31, SG-36 and SG-40. Add bounded typed slot flags, team links and box layout projections without allowing writes.

**Third: mobile and release parity** SG-32, SG-38, SG-39 and SG-41, then device/provider lifecycle and backup tests. This matrix extends but does not supersede [ROADMAP.md](ROADMAP.md), [SAFETY.md](SAFETY.md) or other parity gates.
