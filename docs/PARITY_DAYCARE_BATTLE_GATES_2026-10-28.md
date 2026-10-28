# Daycare, nursery and battle-facility acceptance gates — pass S (2026-10-28)

**Status: 39 proposed, zero executed or certified.** These are implementer tickets, not tests that currently run.

## Test protocol

Pin exact desktop PKHeX, PKHeX.Core package and Android commit. Use distinct synthetic parents, two nonzero adjacent battle records, opposite-valued Subway flags, unusual seeds/egg bits and protected slots. Record fixture origin, before/after SHA-256, raw block diffs, typed projections, expected byte-change allowlist, checksums, and independently re-opened same-version desktop output. Refuse writes on any identity mismatch, unexpected byte change, unsupported layout or provider failure. No ROMs, private saves or game assets in CI.

| Gate | Area | Observable acceptance requirement |
|---|---|---|
| S-D01 | D — read-only daycare | Gen2 two parents plus separate egg and breeding flags preserve raw bytes. |
| S-D02 | D — read-only daycare | Gen5 BW occupancy/EXP/egg-bit preserve unrelated bits. |
| S-D03 | D — read-only daycare | Gen5 BW seed is explicitly unsupported; B2W2 u64 seed matches desktop. |
| S-D04 | D — read-only daycare | Gen6 XY occupancy/EXP/egg/seed match desktop. |
| S-D05 | D — read-only daycare | ORAS two daycare couples have distinct parent slots and seed state. |
| S-D06 | D — read-only daycare | Gen7 UInt128 seed retains high 64 bits. |
| S-D07 | D — read-only daycare | Sword/Shield both nurseries return bounded detached entities. |
| S-D08 | D — read-only daycare | BDSP raw egg flag and parent occupancy do not normalize unusual bytes. |
| S-D09 | D — read-only daycare | BDSP high-bit sign-extended seed and signed step counter retain identity. |
| S-D10 | D — read-only daycare | Negative and out-of-range facility indices refuse. |
| S-D11 | D — read-only daycare | Read-only UI leaves source and cached Core bytes unchanged. |
| S-W01 | W — withdrawal safety (disabled) | USUM synthetic withdrawal moves exact identity into unprotected empty box. |
| S-W02 | W — withdrawal safety (disabled) | Full destination refuses without any changes. |
| S-W03 | W — withdrawal safety (disabled) | Protected empty battle-team destination refuses. |
| S-W04 | W — withdrawal safety (disabled) | Failure between destination write and source clear rolls back. |
| S-W05 | W — withdrawal safety (disabled) | Serialization or provider failure restores original and backup. |
| S-W06 | W — withdrawal safety (disabled) | Concurrent original-file change aborts publication. |
| S-W07 | W — withdrawal safety (disabled) | ORAS secondary withdrawal preserves primary nursery. |
| S-W08 | W — withdrawal safety (disabled) | Gen2 withdrawal preserves egg and breeding flags. |
| S-W09 | W — withdrawal safety (disabled) | BDSP withdrawal preserves other parent, egg state and RNG seed. |
| S-W10 | W — withdrawal safety (disabled) | Unknown ROM-hack layout refuses all writes. |
| S-B01 | B — battle facilities | Gen3 Frontier 16-bit stat edit preserves neighboring 16-bit value. |
| S-B02 | B — battle facilities | Gen3 all seven silver/gold symbols round-trip. |
| S-B03 | B — battle facilities | Gen3 Frontier Pass bit persists separately. |
| S-B04 | B — battle facilities | Gen3 Cancel leaves small block byte-identical. |
| S-B05 | B — battle facilities | Gen4 Castle rank edit preserves neighboring signed 16-bit rank. |
| S-B06 | B — battle facilities | Gen4 Hall streak persists and checksum verifies. |
| S-B07 | B — battle facilities | Gen4 print change then Cancel restores work data. |
| S-B08 | B — battle facilities | Gen5 Flag3=1 Flag7=0 no-op Save preserves both. |
| S-B09 | B — battle facilities | Gen5 Flag3=0 Flag7=1 no-op Save preserves both. |
| S-B10 | B — battle facilities | Gen5 eight Set fields survive no-op Save without recomputation. |
| S-B11 | B — battle facilities | Gen7 Tree streaks/flags match desktop and unrelated event bits stay intact. |
| S-B12 | B — battle facilities | Switching documents A/B cannot write stale A state. |
| S-A01 | A — Android and publication | Opening daycare/frontier viewer does not invoke any mutation. |
| S-A02 | A — Android and publication | Back, Cancel and lifecycle interruption preserve original. |
| S-A03 | A — Android and publication | Provider access revocation fails closed. |
| S-A04 | A — Android and publication | Independent same-version desktop PKHeX reopens every claimed format. |
| S-A05 | A — Android and publication | Vanilla-looking ROM-hack variant is refused for export. |
| S-A06 | A — Android and publication | All fixtures are synthetic or private/consented, none committed. |

## Release barriers and traceability

The highest-priority regression sequence is **S-B01** (Gen3 16-bit neighbor), **S-B05** (Gen4 16-bit neighbor), **S-B08/S-B09** (Gen5 independent flags), **S-W03/S-W04** (protected slot and rollback), **S-B12** (stale save), then **S-A04/S-A05** (independent desktop/format authentication). Do not enable any editor/export while these relevant gates remain unproven. Companion source findings: [UPSTREAM_DAYCARE_BATTLE_REVIEW_2026-10-28.md](UPSTREAM_DAYCARE_BATTLE_REVIEW_2026-10-28.md). Milestones 0/1 remain open; no test is claimed passed here.
