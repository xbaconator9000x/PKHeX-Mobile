# Identity, shiny, form, egg and ball parity gates — pass R (2026-10-28)

**40 proposed, 0 executed, 0 certified.** Companion [source audit](UPSTREAM_IDENTITY_EGG_FORM_REVIEW_2026-10-28.md). No save editing/export authority.

Execution protocol: pin Core package/source and desktop binary hashes, fixture rights, game/revision/layout, original SHA-256, exact entity/slot, raw before/after fields, desktop expectations, untouched-byte diff and Android provider failure trace. Keep private/consented saves and proprietary assets out of Git.

| Gate | Acceptance scenario | State |
| --- | --- | --- |
| **R-G01** | Valid encounter ball choices: bounded span and returned count match desktop, no exception. | Not run |
| **R-G02** | Invalid encounter/zero legal balls: unavailable rather than unrestricted editable list. | Not run |
| **R-G03** | Met and egg location IDs 0,255,256,60005,max ushort: exact or refused, never truncated. | Not run |
| **R-G04** | Null/out-of-context/localized duplicate locations: numeric ID and game context preserved. | Not run |
| **R-G05** | Egg date today and leap-day: correct year/month/day, no day-number misuse. | Not run |
| **R-G06** | Out-of-range years and invalid dates: no byte wrap or automatic normalization. | Not run |
| **R-G07** | WasEgg true/IsEgg false: view and cancel preserve egg history. | Not run |
| **R-G08** | View/refresh/rotate egg: PP/Ups, handler, nickname, Tera, Version and memories unchanged. | Not run |
| **R-G09** | Explicit egg conversion on clone: full diff and byte-identical cancel. | Not run |
| **R-G10** | Switch saves/slots during met/egg dialog: stale apply rejected. | Not run |
| **R-G11** | Null met/egg date remains null across UI focus, timezone and locale. | Not run |
| **R-G12** | EC hex draft: blank, invalid, overflow, valid 8-digit, cancel and restore. | Not run |
| **R-G13** | PID/EC reroll Gen3/4/5 origins and Gen6+: raw field comparison. | Not run |
| **R-G14** | Shiny Gen3/4 nature edit: preserve shiny or explicit consent. | Not run |
| **R-G15** | Species switch: EntityBlank data-loss preview, cancel and rollback. | Not run |
| **R-G16** | Form absent/battle-only: reject before write, not warning after write. | Not run |
| **R-G17** | Rotom form: moves, PP/Ups, order and slot SHA comparison. | Not run |
| **R-G18** | Gen3 Unown 28 forms: PID/nature/gender/ability/shiny conservation or refusal. | Not run |
| **R-G19** | Ability index -1/0/1/2, hidden slot and unsupported: no silent PID change. | Not run |
| **R-G20** | Shiny on/off star/square: PID, SID, EC and XOR matched to desktop. | Not run |
| **R-G21** | Minted/unminted Gen8/9: Nature and StatAlignment independent, null no-op. | Not run |
| **R-G22** | Gen6 Furfrou/Hoopa timer party versus box; raw remain/elapsed/maximum preserved. | Not run |
| **R-G23** | Alcremie and evolution form arguments: named/raw/triple distinction. | Not run |
| **R-G24** | Gen3/4/5 PID gender/nature/ability bit rules verified against desktop. | Not run |
| **R-G25** | Transferred Gen3-5 origin PK6+: PID/EC coupling and shiny side effects. | Not run |
| **R-G26** | Unsupported date capability: no-op setters detected and refused. | Not run |
| **R-G27** | Egg hatch counters PK2/PB8/PK9/PK4: OT versus current friendship. | Not run |
| **R-G28** | ForceHatchPKM: full nickname/OT/met/date/version/memory diff. | Not run |
| **R-G29** | Forge SetForm invalid warning path: demonstrate write-before-validation; refuse locally. | Not run |
| **R-G30** | Form exception after SetMoves: transactional rollback of PP and slot bytes. | Not run |
| **R-G31** | Unown PID bounded search exhaustion: deterministic safe refusal. | Not run |
| **R-G32** | Forge Gen3/4 nature/ability and Gen5 gender: independent desktop fixtures. | Not run |
| **R-G33** | Read-only v10 candidate: PID/EC/ability slot/XOR/egg/form immutable schema. | Not run |
| **R-G34** | Unknown hacks and ambiguous entity identity never authorize vanilla write. | Not run |
| **R-G35** | Malformed level/obedience and overflow: no crash or mutation. | Not run |
| **R-G36** | Android SAF Back/rotation/revoke/replacement: no stale projection or write. | Not run |
| **R-G37** | Independent same-version desktop fixture corpus with provenance and negative cases. | Not run |
| **R-G38** | Future export exact identity, backup, staging, reopen, diff and rollback. | Not run |
| **R-G39** | Pin SDK/package/source and reproduce comparator builds, not README parity. | Not run |
| **R-G40** | GPL and separate third-party art/font/icon rights review before reuse. | Not run |

**Release gate:** source inspection, same-Core synthetic tests and CI compilation do not prove parity. Future writes require authenticated exact layout, immutable fingerprint, restorable backup, staged output, independent reopen, intended/unintended diff, interruption/rollback and positive/negative hack fixtures. Unsupported ROM hacks must never be written as vanilla.