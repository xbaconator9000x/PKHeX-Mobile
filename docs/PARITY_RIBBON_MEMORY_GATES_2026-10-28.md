# Ribbon, mark, memory and cosmetics acceptance matrix — pass K (2026-10-28)

**State:** 36 distinct **proposed** acceptance scenarios. **0 executed or certified by this audit.** Source rationale and immutable file citations are in [pass K source review](UPSTREAM_RIBBON_MEMORY_REVIEW_2026-10-28.md). Do not confuse source presence, a passed Android compile, or Forge's eight upstream unit tests with verified desktop-to-mobile feature parity.

## Oracle and execution contract

- Record tested repository commit, pinned official PKHeX.Core 26.10.18 package identity, official source-release commit `bfe89c74a3f5265805e364b79bc04ad38ebbc5ea`, SDK, Android target, debug/release/trimming configuration, fixture provenance, and expected raw-byte hash.
- Use **independent same-version desktop PKHeX** output as the field/legality oracle. A mobile round-trip through the same Core code alone is insufficient for independent parity. Include synthetic PK1/PK2/PK3/PK7/PK8/PK9/PA9 and negative unsupported layouts; do not distribute real saves or ROMs.
- For every case record **PASS/FAIL/BLOCKED**, observed vs expected, exception and full diff, affected and untouched byte ranges, checksum/block changes, output identity and recovery behavior. A rejected operation must leave source bytes unchanged.
- A test involving writing is **blocked** until Milestone 0/1 fixture and layout gates are satisfied. Never use a user confirmation or successful legality check to authorize an unverified hack.
- Do not import PKForge or MAUI images, sprites, fonts or compiled DLLs without rights verification and GPL-compliant attribution.

## Scenario register

| Gate | Priority | Finding | Setup / action | Acceptance oracle | Status |
| --- | --- | --- | --- | --- | --- |
| **RMG-01** | P0 | RMA-09 | Synthetic PK9 with RibbonPartner=true (index 110) | Typed ribbon index remains 110, not default ChampionKalos index 0; source bytes unchanged | Proposed |
| **RMG-02** | P0 | RMA-09 | Iterate all 111 valid RibbonIndex values plus MAX_COUNT | Round-trip mapping is injective for 0–110; sentinel rejected; no hardcoded scan | Proposed |
| **RMG-03** | P0 | RMA-11 | Open two document sessions, then replace first while ribbon modal pending | Stale modal refuses; no edits or cross-session state bleed | Proposed |
| **RMG-04** | P0 | RMA-12 | Inject reflection/property failure after first of several ribbon updates | Original entity bytes unchanged; no partial applied state | Proposed |
| **RMG-05** | P0 | RMA-14 | Open OT and HT memories and press Save without selecting feelings | Original bytes and all feeling fields preserved; never byte 255 from -1 | Proposed |
| **RMG-06** | P0 | RMA-15 | Valid friendship change with null memory argument/selection | Reject entire edit before mutation; no friendship partial write | Proposed |
| **RMG-07** | P0 | RMA-16 | Memory variable 256 and ushort.MaxValue across supported formats | No byte truncation; checked field-width behavior and desktop comparison | Proposed |
| **RMG-08** | P0 | RMA-19 | Type changed height/weight, then Android Back/cancel | Exact original entity and save bytes preserved | Proposed |
| **RMG-09** | P0 | RMA-19 | Type changed contest stats, then replace document while editor open | No old-entity mutation, no stale write to new document | Proposed |
| **RMG-10** | P0 | RMA-04 | Bulk set valid ribbons on clone with Super Training flags | Explicit expected collateral changes only; untouched raw bytes verified | Proposed |
| **RMG-11** | P0 | RMA-06 | Egg with affixed title, and unowned ribbon selected | Both refused before any save candidate is published | Proposed |
| **RMG-12** | P0 | RMA-26 | Package inventory of all referenced art/font/resource families | Each item has origin, licence, attribution, permitted redistribution; otherwise excluded | Proposed |
| **RMG-13** | P1 | RMA-01 | PK1/PK2/PK3/PK7/PK8/PK9/PA9 synthetic ribbon projections | Property names, types and values match same-version desktop oracles | Proposed |
| **RMG-14** | P1 | RMA-02 | CountMemoryContest=40 and CountMemoryBattle=8 in supported entities | Raw storage retained; game-specific display limits independently checked | Proposed |
| **RMG-15** | P1 | RMA-02 | Gen9 battle-memory count 8 in a synthetic edge fixture | No silent normalization to 7 in read-only; editing eligibility separately enforced | Proposed |
| **RMG-16** | P1 | RMA-03 | Obtainable ribbon preview from cloned entity | Input hash unchanged before and after preview | Proposed |
| **RMG-17** | P1 | RMA-05 | Encounter personality mark and bulk valid-ribbon operation | Mark untouched when applicator explicitly skips it | Proposed |
| **RMG-18** | P1 | RMA-05 | Bulk remove ribbons vs remove marks on synthetic Gen8 | Different mutation scopes reported; no conflation | Proposed |
| **RMG-19** | P1 | RMA-06 | Affix value below -1 or above AffixedRibbon.Max | Reject with typed validation; no sbyte wrap | Proposed |
| **RMG-20** | P1 | RMA-08 | SWSH-only, Gen9-visited and neither-history entities | Allowed title ceilings differ exactly as Core rules | Proposed |
| **RMG-21** | P1 | RMA-07 | Gen8 mark with incompatible weather/time/location | Legality result matches desktop pinned Core; no global context leakage | Proposed |
| **RMG-22** | P1 | RMA-07 | Gen9 outbreak/static and WC9 mark scenarios | Separate source rules and outcomes, not a generic toggle | Proposed |
| **RMG-23** | P1 | RMA-10 | Select title A, then select title B without clearing A | Selected title becomes B; explicit clear yields None | Proposed |
| **RMG-24** | P1 | RMA-13 | Numeric ribbon edit 0→1→maximum→0 | Value is editable and retained; no boolean-only substitution | Proposed |
| **RMG-25** | P1 | RMA-17 | Transferred entity with OT generation 6, handler format 8 | OT and HT argument lists/context differ as desktop; values preserved | Proposed |
| **RMG-26** | P1 | RMA-18 | OT and HT have distinct names and memory phrases | Preview displays correct trainer name and localization | Proposed |
| **RMG-27** | P1 | RMA-20 | Change height scalar with dependent absolute height/weight | All side effects enumerated; no hidden mutation on preview | Proposed |
| **RMG-28** | P1 | RMA-20 | Toggle automatic CP and cancel edit | CP unchanged after cancel; change preview shows computed difference | Proposed |
| **RMG-29** | P1 | RMA-21 | Out-of-range numeric ribbon value and unknown ribbon property | Explicit reject, not silent clamp or reflection fallback | Proposed |
| **RMG-30** | P1 | RMA-21 | Memory ribbon count set then cleared | Presence bit and count remain consistent in independent reopen | Proposed |
| **RMG-31** | P1 | RMA-22 | Affix an owned ribbon then unowned ribbon | Owned accepted only in isolated draft; unowned refused | Proposed |
| **RMG-32** | P1 | RMA-23 | Set memory to None from nonzero values | Memory, intensity, feeling, variable zeroed together only on explicit edit | Proposed |
| **RMG-33** | P1 | RMA-24 | Memory transferred through Gen6/Gen8/Bank contexts | Desktop legality and handler language results match | Proposed |
| **RMG-34** | P1 | RMA-25 | Repeat Forge's eight synthetic awards cases using official pinned Core | Independently executed results documented; do not count upstream test presence | Proposed |
| **RMG-35** | P1 | RMA-27 | Debug vs trimmed Android release ribbon reflection inventory | Identical typed field inventory or explicit unsupported result; no silent omissions | Proposed |
| **RMG-36** | P1 | RMA-28 | Read-only snapshot of award and memory fields from two saves | Immutable, source-byte-preserving, cancellation-safe, no write/export enabled | Proposed |

## Evidence thresholds and implementer handoff

**P0 fail-closed gates:** RMG-01–12. An unvalidated field index, an unset memory picker, stale modal, reflection failure, or cancelled cosmetic edit must never modify any source bytes. Explicit rejection is preferable to partial success.

**Read-only gate:** RMG-13–22 and RMG-35–36 require immutable field snapshots and desktop matching for every *claimed* entity type. Core's `RibbonInfo` reflection and source-matched enum are not enough to certify a trimmed Android build.

**Write/edit gate:** RMG-23–34 remain blocked until authenticated save layouts, verified backups, isolated drafts, staged Save As, source identity recheck, independently reopened output, unexpected-diff checks and provider interruption/rollback tests are passing. A full-save reopen in an upstream unit test is not an Android transaction.

**Future PokéBacon interoperability:** A versioned profile must identify the exact save layout and supported ribbon/memory schema, not just the species list or enum names. Unknown custom ribbons, custom IDs and ROM-hack storage remain read-only until a separately authenticated profile has positive and negative fixtures. Preserve unknown bytes and refuse silent vanilla reinterpretation.

The implementer owns src/, tests/, app and workflows; this audit owns only `docs/UPSTREAM_*` and `docs/PARITY_*`. No production code, assets or dependency changes are authorized by this matrix.
