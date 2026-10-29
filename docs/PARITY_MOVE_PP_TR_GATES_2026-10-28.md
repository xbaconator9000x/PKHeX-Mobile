# Move, PP, relearn and technical-record parity gates — pass Q (2026-10-28)

**40 unique proposed scenarios; 0 executed; 0 certified.** Companion [source findings](UPSTREAM_MOVE_PP_TR_REVIEW_2026-10-28.md). These are actionable specifications, not completed features. Save editing/export and unsupported hack writing remain disabled.

**Execution protocol:** Pin exact Core package/source SHA and same-version desktop binary. Record fixture rights, game/revision/layout, source SHA-256, slot, expected and actual fields, independent reopen, diff whitelist, provider environment and failure trace. Keep synthetic or consented fixtures private; do not commit saves, ROMs or commercial assets.

## Read-only and identity

| Gate | Tickets | Acceptance scenario | State |
| --- | --- | --- | --- |
| **Q-G01** | Q-01,Q-02 | Open/refresh/Back/rotate depleted-PP entities across generations; source bytes unchanged. | Not run |
| **Q-G02** | Q-03 | PP Ups draft change does not alter current PP unless explicitly requested. | Not run |
| **Q-G03** | Q-04,Q-13 | Switch document/slot with stale dialog; reject apply, preserve both files. | Not run |
| **Q-G04** | Q-05,Q-06 | Two move pages and delayed legality refresh cannot cross-bind data. | Not run |
| **Q-G05** | Q-08 | Compare move power, category, type and accuracy across Gen1/3/5/8/9 desktop. | Not run |
| **Q-G06** | Q-09 | Alpha selection differs from move1; verify exact chosen Alpha move and null refusal. | Not run |
| **Q-G07** | Q-32 | Read-only projection has four raw IDs/PP/Ups, relearn capability and slots; immutable. | Not run |
| **Q-G08** | Q-26,Q-29 | Retain raw anomalous PP; hack-derived box PP is explicitly unavailable. | Not run |
| **Q-G09** | Q-14 | Pre-Gen6 relearn unsupported, Gen6+ exact raw four slots. | Not run |
| **Q-G10** | Q-22,Q-25 | TR index, mapped move, capacity, used count, permit and raw flag separately. | Not run |

## Core mutation and legality

| Gate | Tickets | Acceptance scenario | State |
| --- | --- | --- | --- |
| **Q-G11** | Q-15 | One move-ID change: diff all four PP before/after on clone. | Not run |
| **Q-G12** | Q-16 | Moves [0,A,0,B] with distinct PP/Ups: verify reorder and full diff. | Not run |
| **Q-G13** | Q-17 | Compare AddMove and SetMoves at MaxMoveID and MaxMoveID+1. | Not run |
| **Q-G14** | Q-18 | Egg PP/PPUps legal and illegal cases, all slots and supported formats. | Not run |
| **Q-G15** | Q-19 | Party/box PP legality Gen1/3/4/7/8/9 with encounter context. | Not run |
| **Q-G16** | Q-20 | Build both exact source revisions; reproduce PP-verifier edge-case difference. | Not run |
| **Q-G17** | Q-21 | Relearn encounter, egg, bonus, duplicate and cleared-history cases. | Not run |
| **Q-G18** | Q-07 | Randomized versus level-up suggestions are separate cancellable previews. | Not run |
| **Q-G19** | Q-23 | None/ForceAll/LegalAll/LegalCurrent TR changes on clones; cancel preserves bytes. | Not run |
| **Q-G20** | Q-24 | ZA first/middle/last TM display and bit index; no off-by-one or out-of-range. | Not run |
| **Q-G21** | Q-25 | ZA permit false does not expose editable learned-TR controls. | Not run |
| **Q-G22** | Q-14,Q-22 | Reject unsupported relearn setter and invalid record indices before mutation. | Not run |

## Upstream mobile negative cases

| Gate | Tickets | Acceptance scenario | State |
| --- | --- | --- | --- |
| **Q-G23** | Q-01 | MAUI-style binding on load must not heal PP; record event sequence. | Not run |
| **Q-G24** | Q-02 | Blank/non-numeric/overflow PP text causes safe refusal, not crash. | Not run |
| **Q-G25** | Q-10,Q-11 | Duplicate localized move names and reordered TR rows resolve by numeric index. | Not run |
| **Q-G26** | Q-10,Q-12 | Inject failure after each cleared/updated TR flag; exact rollback. | Not run |
| **Q-G27** | Q-27 | Valid PP/Ups followed by malformed relearn count: no partial mutation. | Not run |
| **Q-G28** | Q-28 | Run pinned Forge synthetic tests; do not label desktop parity. | Not run |
| **Q-G29** | Q-29,Q-30 | CFRU/Unbound compact boxed layouts: no PP write without authenticated profile. | Not run |
| **Q-G30** | Q-30 | Hack party packed-byte boundaries and interrupted writes fail closed. | Not run |
| **Q-G31** | Q-31 | Cancel after move preview/selection: zero session/file mutation. | Not run |
| **Q-G32** | Q-04,Q-13 | Revoke provider URI while dialog open: stale apply safely refused. | Not run |

## Export, integration and rights

| Gate | Tickets | Acceptance scenario | State |
| --- | --- | --- | --- |
| **Q-G33** | Q-15,Q-23 | Stage/reopen edited PKM and full save, diff requested and untouched fields. | Not run |
| **Q-G34** | Q-20 | Manifest Core package/source and desktop binary hashes, revision and expectations. | Not run |
| **Q-G35** | Q-24,Q-27 | Each injected failure preserves original SHA-256 and restorable backup. | Not run |
| **Q-G36** | Q-30 | PokéBacon profile version/ID maps plus positive and negative layout fixtures. | Not run |
| **Q-G37** | Q-32 | Physical Android SAF picker/Back/rotation/replacement/interruption/revocation. | Not run |
| **Q-G38** | Q-01,Q-20 | Same-version independent desktop-matched read corpus per claimed generation. | Not run |
| **Q-G39** | Q-23 | No game art, ROMs, private saves, DLLs or credentials in code or CI artifacts. | Not run |
| **Q-G40** | Q-32 | GPL notices and independently licensed assets reviewed before importing. | Not run |

## Release boundary

Core synthetic tests cannot replace desktop-matched saves; desktop parity cannot replace physical Android provider interruption tests. Every write requires authenticated exact identity, backup, staging, independent reopen, unintended-change review, rollback and positive/negative profile fixtures. Unknown ROM hacks remain read-only.
