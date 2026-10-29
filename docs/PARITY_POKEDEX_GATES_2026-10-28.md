# Pokédex, regional dex and acquisition acceptance gates — pass L (2026-10-28)

**40 proposed cases; zero executed/certified by this audit.** See [source findings](UPSTREAM_POKEDEX_REVIEW_2026-10-28.md). All tests must use synthetic or consented rights-cleared fixtures; never commit ROMs, private saves, commercial art or secrets. Pin Core 26.10.18 and desktop source SHA for parity.

| Gate | Scenario | Reproducible setup | Pass or refusal condition |
| --- | --- | --- | --- |
| **DEX-G01** | First-species mapping | Gen1 species 1 and 2 opposite flags | No row shift |
| **DEX-G02** | Final-species mapping | Gen1 last species caught | Last entry persisted |
| **DEX-G03** | Middle-species mapping | Three adjacent species distinct | No neighbor leakage |
| **DEX-G04** | Stale modal | Open A, replace with B, save old modal | Refuse; both byte-identical |
| **DEX-G05** | Bulk checkbox refresh | Seen-all/caught-all on visible rows | UI equals staged model |
| **DEX-G06** | Cancel | Toggle then close | Exact source bytes unchanged |
| **DEX-G07** | Caught/seen validity | Caught true, seen false across games | Explicit valid result or refusal |
| **DEX-G08** | Dex dispatch | Enumerate all supported SAV types | Only implemented editor buttons enabled |
| **DEX-G09** | Trainer dispatch | SAV7b/SAV8BS/SAV8SWSH | No tautological button visibility |
| **DEX-G10** | Gen4 gender history | Male-first and female-first records | Both orders preserved |
| **DEX-G11** | Gen4 adjacent fields | Modify one flag in clone | Spinda/form bytes preserved |
| **DEX-G12** | Gen5 seen variants | Four gender/shiny combinations | Distinct readings |
| **DEX-G13** | Gen5 displayed variants | Four display variants | No conflation with seen |
| **DEX-G14** | BW offsets | BW synthetic form/language flags | Correct offsets |
| **DEX-G15** | B2W2 offsets | B2W2 same data | Correct shifted offsets |
| **DEX-G16** | Gen5 language limit | Species 493 and 494 | No phantom language flag |
| **DEX-G17** | SWSH no DLC | Galar-only blocks | Armor/Crown unavailable |
| **DEX-G18** | SWSH DLC | One and two DLC blocks | Correct regional lookup |
| **DEX-G19** | SV Paldea mode | Empty Kitakami block | 0x18 record selected |
| **DEX-G20** | SV Kitakami mode | Present Kitakami block | 0x20 record selected |
| **DEX-G21** | SV record bounds | Both record sizes, boundary species | No overlap/truncation |
| **DEX-G22** | SV language/display | Nine languages and regional display | Desktop matched fields |
| **DEX-G23** | Kitakami four form states | Distinct obtained/seen/heard/checked | All preserved |
| **DEX-G24** | Bulk block mutation | Clone and SeenNone | Whole block diff, no publication |
| **DEX-G25** | SV seen-only side effects | Existing caught/form/language data | No unapproved collateral |
| **DEX-G26** | SV egg registration | Egg synthetic PKM | No dex registration |
| **DEX-G27** | SV absent/DLC form | DexGroup beyond revision | No phantom entry |
| **DEX-G28** | Z-A sentinel | Species above MaxSpeciesID | Refuse before index zero |
| **DEX-G29** | Z-A mega state | Seen-only request with mega flags | Full entry diff approved |
| **DEX-G30** | SWSH regional flags | Gender/shiny flags preseeded | No loss on toggle |
| **DEX-G31** | Unbound no-op | Custom session positive species | Edit disabled, no success |
| **DEX-G32** | CFRU mapping | National ID differs from hack dex ID | Authenticated profile only |
| **DEX-G33** | Progress denominator | Absent and DLC species mixed | Correct labeled denominators |
| **DEX-G34** | Forge nine cases | Run pinned theory with toolchain | Recorded executed results |
| **DEX-G35** | Desktop parity | 26.10.18 independent fixtures | Field and byte comparisons |
| **DEX-G36** | Switch ciphertext | SCBlock encrypted envelope | Independent serialized reopen |
| **DEX-G37** | Received versus edited | Import then nickname edit | Only import registers |
| **DEX-G38** | Living dex scope | Edit versus generate | Separate approvals and diffs |
| **DEX-G39** | Android lifecycle | Rotate/reopen provider while inspecting | No stale state or mutation |
| **DEX-G40** | Safety and licence | Unsupported hack and asset review | Export blocked; no unlicensed media |

## Evidence protocol and blocking priorities

Record for each gate: app commit, upstream source and package SHAs, save format and revision, fixture provenance, original/output SHA-256, full changed-byte map, desktop comparison, command/toolchain, Android API/provider when applicable, and one of not-run/pass/fail/blocked. A successful same-Core synthetic unit test is not an independent desktop fixture.

**P0 handoff:** DEX-G01–04 address row mapping and stale document identity; G21, G24–25 and G28–31 address record bounds and collateral edits; G35–36 and G39–40 address independent desktop verification, provider interruption and licence safety. Earlier [save safety](SAFETY.md) and [I/O gates](PARITY_IO_BANK_GATES_2026-10-28.md) also remain mandatory.

PKForge's pinned [dex round-trip theory]([FT L49-59](https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/tests/PKForge.Engine.Tests/DexRoundTripTests.cs#L49-L59)) declares nine versions, but its [reload helper]([FT L28-46](https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/tests/PKForge.Engine.Tests/DexRoundTripTests.cs#L28-L46)) clones Switch SCBlock saves instead of independently reopening encrypted serialized bytes. Its [received-import tests]([FI L26-94](https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/tests/PKForge.Engine.Tests/ReceivedDexRegistrationTests.cs#L26-L94)) have three Fact methods. These are useful source-level test candidates, not Android or desktop parity certifications.

**Ownership:** implementation watchdog owns code/tests/build; audit watchdog owns this evidence and gate-status record. No dex save-write permission is granted. Milestones 0 and 1 remain open.
