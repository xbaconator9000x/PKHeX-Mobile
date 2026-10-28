# Inventory, pouch, item flag and save safety acceptance gates — pass M (2026-10-28)

**38 proposed scenarios, zero executed or certified in this audit.** Source evidence: [inventory audit](UPSTREAM_INVENTORY_REVIEW_2026-10-28.md). This is a test specification, not a claim of feature parity or save safety. Use only synthetic or consented rights-cleared fixtures, never commercial assets, ROMs or private saves in the repository.

| Gate | Scenario | Reproducible setup | Pass/refusal condition |
| --- | --- | --- | --- |
| **BAG-G01** | Remote detached-array copy | MAUI remote block bytes and cloned save buffer | Remote read must alter intended buffer or fail visibly; never claim success from detached copy |
| **BAG-G02** | Remote failure | Inject remote write exception after staging | Original and remote state unchanged or recoverable; explicit error |
| **BAG-G03** | Stale document | Open A modal, switch global to B, apply bulk action | Reject; A/B original bytes unchanged |
| **BAG-G04** | Duplicate item names | Two IDs share same localized label | Stable numeric IDs; no name-first aliasing |
| **BAG-G05** | Unknown item placeholder | Blank name table entry maps to `(Item #NNN)` | Preserve original ID; no alias to first placeholder |
| **BAG-G06** | Negative quantity | Enter -1 for existing item | Refuse or validated zero transition, no wraparound |
| **BAG-G07** | Quantity overflow | Enter max-int or >per-item maximum | Explicit refusal/clamp matching desktop rules |
| **BAG-G08** | Pouch capacity | Provide >capacity nonempty rows | Preflight refuses; no partial draft |
| **BAG-G09** | Duplicate ID rows | Same ID in two pouch rows | Reject or deterministic explicit merge; no last-write-wins surprise |
| **BAG-G10** | Static count isolation | Open two editor instances with different counts | No shared count or reset on unrelated navigation |
| **BAG-G11** | No-selected pouch | Fire tab/refresh event with index -1 | No exception or mutation |
| **BAG-G12** | Row sprite identity | Duplicate names, reorder rows, change selected item | Correct row/ID and captured save context |
| **BAG-G13** | Cancel/Back | Change 3 fields then dismiss modal | Byte-identical original; no background operation |
| **BAG-G14** | SV 3000 records | Gen9 SV synthetic 0xBB80 inventory block | Exact ID/count/pouch/flags/padding snapshot |
| **BAG-G15** | ZA 3000 records | Gen9a 0xBB80 item records | Different pouch IDs and flags preserved |
| **BAG-G16** | ZA extra 0x80 region | Newer ZA source with 0xBC00 layout versus pinned Core | No newer unlock flags assumed; version-gated refusal |
| **BAG-G17** | SV untouched illegal slots | Single quantity change with nonstandard IDs present | Full-block diff; refuse collateral deletion |
| **BAG-G18** | ZA untouched illegal slots | Single quantity change plus story/unused IDs | No silent cleanup of unrelated records |
| **BAG-G19** | SV absent legal IDs | Serialize one pouch with unlisted legal IDs | Zeroing behavior detected; no unapproved write |
| **BAG-G20** | ZA Tasty Trash | Key-item pouch contains ID 2598 sentinel | Explicit destructive-diff refusal |
| **BAG-G21** | SV obtained/new/favorite | Preseed distinct flag combinations | No conflation of count 0 and obtained |
| **BAG-G22** | ZA notify/shop/held | Preseed independent flags including Mega Stone held | All supported flags and padding survive |
| **BAG-G23** | ZA Mega Shard patch | Synthetic pre/post block 0x0ABC6547 | Correct 9999 vs old max without guessing |
| **BAG-G24** | ZA protected quest IDs | Colorful Screw and Cherished Ring | Current protected count preserved; disallowed ring refused |
| **BAG-G25** | FRLG encrypted count | Known security key and 2 item counts | Round-trip exact XOR; no false plain-text quantity |
| **BAG-G26** | FRLG PC key exception | Same count in PC and bag | PC unencrypted; keyed bag verified |
| **BAG-G27** | FRLG HM count | HM and TM in same pouch | HM max 1; TM normal cap |
| **BAG-G28** | Can-contain vs legal | Valid pouch ID but unobtainable item | Distinguish representation, legality and write permission |
| **BAG-G29** | Desktop sanitize read | Unknown/out-of-range source IDs | Reader reports anomalies; source hash unchanged |
| **BAG-G30** | Desktop SV/ZA column policy | Try changing item ID versus quantity | Item-ID edit disabled until separately authenticated |
| **BAG-G31** | Forge engine direct invalid ID | Bypass UI picker, call SetItemCount with wrong pouch | Engine rejects before serialization |
| **BAG-G32** | Forge pending retry | Fail RunMutationAsync after clearing pending | Queued changes retained, no false success |
| **BAG-G33** | Forge preset rollback | Fail at each Nth item of multi-item preset | Exact pre-edit bytes recoverable, no partial publish |
| **BAG-G34** | Preset cross-game | Same generation but different game/ROM-hack profile | No implicit compatibility based on generation/name |
| **BAG-G35** | Concurrent preset store | Two writers, injected crash during replace | No lost update, corrupted JSON or silent success |
| **BAG-G36** | Desktop/Android independent parity | Core 26.10.18 desktop independently baselined item fixtures | Field+raw-block comparison, provenance, provider lifecycle and SHA-256 recorded |
| **BAG-G37** | Unsupported ROM-hack inventory | CFRU/expanded custom item IDs in vanilla-shaped save | No write capability or vanilla certification |
| **BAG-G38** | Asset and GPL compliance | Audit each displayed item sprite and redistributed source | Only rights-cleared assets, attribution and GPL obligations satisfied |

## Required execution record

For each scenario: pinned official source SHA and Core 26.10.18 package provenance, app commit, independent desktop fixture provenance and format revision, original/staged/reopened SHA-256, expected and actual typed fields, exact byte diff including flags/padding, command/toolchain, Android API and document provider if applicable, cancellation/recovery outcome, and explicit `not-run`, `pass`, `fail` or `blocked`. A successful compile or a same-Core-generated synthetic fixture is **not** independent desktop parity.

**Safety ordering:** BAG-G01–09 and G13 are immediate session/data-integrity blockers. G14–24 are Gen9/Gen3 structure and side-effect blockers. G31–37 block unverified direct writes, partial preset transactions, unknown profiles and provider publication. G36 requires a truly independent same-version desktop baseline; G38 requires per-asset licence evidence. In-place writes and unsupported ROM-hack saves remain prohibited. The broader [SAFETY.md](SAFETY.md), [I/O gates](PARITY_IO_BANK_GATES_2026-10-28.md) and [entity-format gates](PARITY_ENTITY_CONVERSION_GATES_2026-10-28.md) also apply.

**Implementer handoff:** Build immutable read-only pouch snapshots first; do not import MAUI's editor or Forge's item art. Add negative tests for out-of-range item IDs, duplicate localized names, corrupted zero-count flags, Gen3 keyed quantities, Z-A revision mismatch and stale document/provider identity. Then independently validate complete serialized outputs before any write/export capability is enabled.

**Ownership:** This document records proposed acceptance gates only; implementation owns executable tests and application code. Milestones 0 and 1 remain open.
