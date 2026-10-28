# Generation 7 world and facility parity acceptance matrix — pass Y (2026-10-28)

**Execution status:** 40 proposed cases, **0 executed** in this audit. These are test specifications, not passing tests or certified formats. Companion source findings: [UPSTREAM_GEN7_SYSTEMS_REVIEW_2026-10-28.md](UPSTREAM_GEN7_SYSTEMS_REVIEW_2026-10-28.md).

## Preconditions and oracle

- Run against pinned PKHeX.Core NuGet 26.10.18 and independently baselined same-version desktop PKHeX; the audit's newer source SHA alone is insufficient. Use synthetic sentinel buffers for dangerous setters and privately held consented fixtures for semantic parity.
- Each test records SHA-256 before/after, byte ranges changed, game/layout revision, exact Core package hash, provider identity, expected result and failure trace. Do not commit proprietary saves or assets.
- P0 failure blocks save publication for the affected game/feature. Unknown ROM-hack schemas remain read-only. A successful read is not a write authorization.

| Case | Setup | Action | Required assertion |
|---|---|---|---|
| YG01 — Festival facility index 0–6 | Synthetic Festa block with distinct facility and coin sentinels | Read each valid facility | All seven read; no source mutation |
| YG02 — Facility index 7 | Same block; coins at 0x508 unique sentinel | Attempt GetFestaFacility(7) through guarded adapter | Reject before Core; coin and adjacent bytes unchanged |
| YG03 — Facility negative / huge | Same block | Call -1, int.MaxValue | Reject deterministically; no mutation |
| YG04 — Festa message index | Patterned message and facility bytes | Read/write -1 and out-of-range | Reject without touching unrelated fields |
| YG05 — Phrase and prize bounds | Known phrase 0–106 and reward sentinels | Test -1, first invalid upper index | Reject; valid endpoints preserved |
| YG06 — Festa coin coupling | Negative and max-int inputs, used record 038 | Attempt coin-only change | Reject negative; account for total/used coupling or refuse |
| YG07 — Malformed Festa date | Invalid hour=24 and minute=60 in otherwise valid date | Read projection | Return raw invalid diagnostic, not uncaught exception |
| YG08 — Facility raw boolean | IsIntroduced raw 2 and NPC negative | Read and save unrelated field in draft | Raw bytes preserved unless explicitly edited |
| YG09 — Facility narrowing | Type/color/rank outside byte domain | Attempt edit | Reject, never modulo-wrap |
| YG10 — Resort slot bounds | 93 synthetic stored entries with 4-byte metadata sentinels | Read 0,92,93 | Valid 0/92 detached; 93 rejected; metadata unchanged |
| YG11 — Bean 15-byte isolation | Distinct bytes and adjacent sentinel | Read 15 counters, attempt index15 | Exact 15-byte fingerprint; invalid index rejected |
| YG12 — Bean negative parsing | Synthetic editable count -1 | Run desktop-like parse and guarded adapter | Demonstrate cast hazard; mobile rejects instead of 255 |
| YG13 — Bean fill/clear | All 15 counters and neighboring metadata | Stage fill/clear | Exactly 15 bytes differ; rollback/cancel restores all |
| YG14 — Hall of Fame alias | Unique 12 ushort sentinels in 24 bytes | Read First3/First4 and raw slot 3 | Expose alias; raw slot mapping verified against desktop |
| YG15 — Zygarde SM count | SM 95 cells and surrounding sentinels | Read 0..94, attempt 95 | 95 detached; 95 rejected |
| YG16 — Zygarde USUM count | USUM 100 cells and surrounding sentinels | Read 0..99, attempt 100 | 100 detached; 100 rejected |
| YG17 — Zygarde state domain | Cells 0,1,2,3,65535 | Inspect then attempt invalid write | Display raw unusual values; refuse invalid edits |
| YG18 — EventWork cross-game | SM 4000 flags, USUM 4960 flags | Cross-parse same bytes | No wrong-version read/write authorization |
| YG19 — Battle Tree streak negatives | Streak -1, 0, 65535, 65536 | Stage setter | Negative/overflow rejected; valid endpoints preserved |
| YG20 — Battle Tree 50 trainers | Unique signed IDs and ability values | Read 50 records and index50 | 50 detached records; index50 rejected |
| YG21 — Battle Tree oversized array | 51 records with last invalid | Call upstream setter on disposable clone | Record first-50 mutation before failure; mobile prevalidates and rejects atomically |
| YG22 — Fourth Battle Tree type | Four type sentinels x super/max states | Open/save MAUI-like editor | Fourth type and untouched flags unchanged |
| YG23 — Gyro raw 2 | PokeFinder gyro flag raw 2 | Read and no-op save | Raw 2 retained; UI false does not rewrite to 0 |
| YG24 — PokeFinder counters | SnapCount >9,999,999, thumbsHigh > total | No-op save and explicit edit | No-op preserves raw; explicit edit validates coupled fields |
| YG25 — Camera version picker | Raw versions 0..6 including 1 and 5 | Open/save MAUI-like page | 1–5 round-trip exactly; malformed raw remains read-only |
| YG26 — Battle Agency uninvite bits | Two ushort sentinels with unrelated high bits set | Toggle invited off on disposable clone | Show desktop zeroing; mobile clears only mask or refuses |
| YG27 — Battle Agency three slots | Three distinct PKM records and padding | Edit only agency grade | PKM and unrelated bytes identical |
| YG28 — Festival facility type change | Lucky/exchange latent values | Change type then undo/cancel | No latent-value loss absent explicit approval |
| YG29 — Trainer record live change | Synthetic trainer record and UI change event | Change then Cancel | Original bytes identical |
| YG30 — Save tab partial exception | One invalid field after several valid edits | Trigger SavePage7-like sequence | No partial write; draft discarded |
| YG31 — Stale trainer page | Two synthetic saves A/B, static page constructed on A | Switch to B, invoke Save | No write to A; identity mismatch refuses |
| YG32 — Ultra static page | Two USUM synthetic saves and Surf scores | Switch documents mid-editor | No stale write or cross-document leakage |
| YG33 — Time-offset sentinel | AlolaTime 9,999,999 | Open and save without touching picker | No null dereference; sentinel preserved |
| YG34 — Fashion invalid selection | Synthetic fashion block with all nonzero bytes | Invoke unselected fashion action | No Clear, no mutation, no blocking async .Result |
| YG35 — Fashion payload failure | Short/malformed payload and revoked provider access | Attempt import | Validate length and source before draft; original unchanged |
| YG36 — Stamp reserved bits | Unknown bits set alongside 15 stamp bits | Edit unrelated trainer field | Unknown and known bits unchanged |
| YG37 — Read-only parity desktop SM | Independently baselined consented/synthetic SM save | Compare Festa, 15 beans, 95 cells, 50 scouts | Exact semantic/raw fingerprints against desktop 26.10.18 |
| YG38 — Read-only parity desktop USUM | Independently baselined consented/synthetic USUM save | Compare Agency, Festa, 15 beans, 100 cells | Exact semantic/raw fingerprints against desktop 26.10.18 |
| YG39 — Provider identity/rollback | Two-open Android SAF fake with changed URI revision and partial write | Attempt staged Save As in future writer | Fail closed; original hash and backup recoverable |
| YG40 — Hack masquerade | Synthetic retail-looking Gen7 header with altered block offsets | Try identity detection then write authorization | No verified profile => read-only; never vanilla export |

## Coverage mapping and gate disposition

- **Boundary/raw preservation:** YG01–YG18, YG20, YG23–YG28 and YG36. Distinguish Core source hazards from mobile adapter behavior; source analysis is not execution.
- **Transactional/cancellation:** YG19, YG21, YG29–YG35, YG39–YG40. No Save As or write API may ship until identity, backup, staged validation and rollback pass.
- **Independent semantic parity:** YG37–YG38 must compare game-specific fields to desktop PKHeX 26.10.18; same-Core synthetic round trips do not qualify.
- **Device acceptance:** YG39 additionally requires real provider/emulator coverage for interruption, revocation and process death. A mock-only pass is insufficient.

## Implementation handoff order

1. Block invalid Festa facility index 7 and preserve raw Hall of Fame / Battle Agency bits (YG02, YG14, YG26).
2. Remove MAUI-style static save references, live writes and partial Save-page behavior before adapting those pages (YG29–YG35).
3. Implement immutable Gen7 projection with 93 resort slots, 15 beans, 95/100 Zygarde cells, 50 Battle Tree trainers and four streak modes (YG10–YG22).
4. Independently verify SM/USUM against desktop and validate provider recovery before any export (YG37–YG40).

**Status:** all cases unexecuted. No feature parity or Android save-writing capability is certified by this matrix.
