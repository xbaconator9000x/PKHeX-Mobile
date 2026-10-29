# Pinned upstream fork-delta review — pass B (2026-10-28)

**Status: source-level review of all 21 fork-side changed paths, NOT a build, semantic sign-off, or write-safety certification.** This follows [pass A](UPSTREAM_SOURCE_AUDIT_2026-10-27.md). No fork code, game data, binary personal table, or artwork has been imported into PKHeX-Mobile.

## Immutable comparison and scope

- Official PKHeX audit revision: [`7e26bc53215e4f7bc0041ca375f1042539cb81b3`](https://github.com/kwsch/PKHeX/tree/7e26bc53215e4f7bc0041ca375f1042539cb81b3).
- PKForge's **gitlinked** Core fork: [`91e9c88667a6a191a000db68e2db19505eb87561`](https://github.com/sofianeelhor/PKForge-PKHeX/tree/91e9c88667a6a191a000db68e2db19505eb87561); parent PKForge [`2df04430e532b78c247f45b936f3068d8f824987`](https://github.com/sofianeelhor/PKForge/tree/2df04430e532b78c247f45b936f3068d8f824987).
- [Compare official head against gitlinked fork](https://github.com/sofianeelhor/PKForge-PKHeX/compare/7e26bc53215e4f7bc0041ca375f1042539cb81b3...91e9c88667a6a191a000db68e2db19505eb87561): diverged, **4 commits ahead / 87 behind**, merge base `24c9e3c31f9f84ef9fd999a1306a8fb129d4f07e`; **21 fork-side changed paths = 7 added + 14 modified**, +3,549/-37 textual diff lines reported by GitHub (binary file has no textual patch). This is a comparison of fork-side commits against their common ancestor, **not** a complete inventory of all differences from current official Core.
- Current PKHeX-Mobile [read-only Core pin](../src/PKHeXMobile.ReadOnly/CORE_PIN.md) is NuGet **26.10.18**, associated with official release commit [`bfe89c74a3f5265805e364b79bc04ad38ebbc5ea`](https://github.com/kwsch/PKHeX/tree/bfe89c74a3f5265805e364b79bc04ad38ebbc5ea), **not** either audit head or the PKForge fork.
- [Compare package-tag source to audit head](https://github.com/kwsch/PKHeX/compare/bfe89c74a3f5265805e364b79bc04ad38ebbc5ea...7e26bc53215e4f7bc0041ca375f1042539cb81b3): official audit head is **10 commits ahead**, **55 changed paths**: 34 Core, 19 WinForms and 2 binary legality fixture files. These counts are GitHub compare results, not source execution or package-to-commit cryptographic attestation. The changed Core paths include Gen 4 Safari structures, Gen 3 FRLG item storage, legality verifiers and Showdown parsing. Keep desktop comparison on **the same 26.10.18 package** as the adapter until an upgrade passes fixtures.

## All fork-side paths: semantic disposition and test obligation

Each row was matched to the pinned compare patch; “test” means a **required** test, not a completed one.

| # | Pinned fork path | Delta | Behavior / scope | Acceptance obligation |
| ---: | --- | --- | --- | --- |
| 1 | [CompassBlockKeys.cs](https://github.com/sofianeelhor/PKForge-PKHeX/blob/91e9c88667a6a191a000db68e2db19505eb87561/PKHeX.Core/Compass/CompassBlockKeys.cs) | added; +1459/-0 | Compass block-key catalogue | Verify versioned key provenance, hash mapping and collision handling; do not infer save compatibility from key presence. |
| 2 | [GameVersion.cs](https://github.com/sofianeelhor/PKForge-PKHeX/blob/91e9c88667a6a191a000db68e2db19505eb87561/PKHeX.Core/Game/Enums/GameVersion.cs) | modified; +5/-0 | New BDSPLUMI enum value 54 | Prove no version-ID collision on upstream rebases or cross-format entity serialization. |
| 3 | [GameStrings.cs](https://github.com/sofianeelhor/PKForge-PKHeX/blob/91e9c88667a6a191a000db68e2db19505eb87561/PKHeX.Core/Game/GameStrings/GameStrings.cs) | modified; +3/-1 | Lumi item-name lookup | Audit fallback/localisation and new resource provenance before packaging. |
| 4 | [GameUtil.cs](https://github.com/sofianeelhor/PKForge-PKHeX/blob/91e9c88667a6a191a000db68e2db19505eb87561/PKHeX.Core/Game/GameUtil.cs) | modified; +2/-0 | Lumi game/context routing | Verify all contexts, conversions and unsupported-game refusals. |
| 5 | [PlayerBag8b.cs](https://github.com/sofianeelhor/PKForge-PKHeX/blob/91e9c88667a6a191a000db68e2db19505eb87561/PKHeX.Core/Items/Bags/PlayerBag8b.cs) | modified; +23/-19 | Lumi-specific bag/cleaning branch | Test vanilla cleanup unchanged; test hack bag preservation and legal ranges. |
| 6 | [ItemStorage8BDSPLumi.cs](https://github.com/sofianeelhor/PKForge-PKHeX/blob/91e9c88667a6a191a000db68e2db19505eb87561/PKHeX.Core/Items/ItemStorage8BDSPLumi.cs) | added; +33/-0 | Lumi bag legal-item tables | IsLegal accepts any KeyItems type via short-circuit; test unknown IDs and counts before reuse. |
| 7 | [MiscVerifierG3.cs](https://github.com/sofianeelhor/PKForge-PKHeX/blob/91e9c88667a6a191a000db68e2db19505eb87561/PKHeX.Core/Legality/Verifiers/Misc/MiscVerifierG3.cs) | modified; +4/-7 | Gen 3 transfer-trash rule change | Global Gen 3 legality behavior changed; compare legal/illegal XD/Colosseum transfers against pinned official Core. |
| 8 | [MoveInfo.cs](https://github.com/sofianeelhor/PKForge-PKHeX/blob/91e9c88667a6a191a000db68e2db19505eb87561/PKHeX.Core/Moves/MoveInfo.cs) | modified; +4/-4 | Lumi move-context branches | Verify move availability, PP and dummy-move semantics per version. |
| 9 | [PB8.cs](https://github.com/sofianeelhor/PKForge-PKHeX/blob/91e9c88667a6a191a000db68e2db19505eb87561/PKHeX.Core/PKM/PB8.cs) | modified; +1/-1 | Unseal retail PB8 | Inheritance surface changes; test vanilla PB8 behavior and derived hack records. |
| 10 | [PB8LUMI.cs](https://github.com/sofianeelhor/PKForge-PKHeX/blob/91e9c88667a6a191a000db68e2db19505eb87561/PKHeX.Core/PKM/PB8LUMI.cs) | added; +22/-0 | Derived Luminescent Pokémon record | Validate extra species/items, form tables, serialization and cross-context conversion. |
| 11 | [EntityBlank.cs](https://github.com/sofianeelhor/PKForge-PKHeX/blob/91e9c88667a6a191a000db68e2db19505eb87561/PKHeX.Core/PKM/Util/EntityBlank.cs) | modified; +1/-0 | Blank Lumi record creation | Prove blank defaults, context and downstream conversion handling. |
| 12 | [EntityContext.cs](https://github.com/sofianeelhor/PKForge-PKHeX/blob/91e9c88667a6a191a000db68e2db19505eb87561/PKHeX.Core/PKM/Util/EntityContext.cs) | modified; +6/-1 | New Gen8bLumi context | Audit every context switch and transfer/legality fallback for unsupported cases. |
| 13 | [PersonalTable.cs](https://github.com/sofianeelhor/PKForge-PKHeX/blob/91e9c88667a6a191a000db68e2db19505eb87561/PKHeX.Core/PersonalInfo/PersonalTable.cs) | modified; +5/-0 | New personal table binding | Require exact game/mod revision and legal provenance for species table. |
| 14 | [PersonalTable8BDSP.cs](https://github.com/sofianeelhor/PKForge-PKHeX/blob/91e9c88667a6a191a000db68e2db19505eb87561/PKHeX.Core/PersonalInfo/Table/PersonalTable8BDSP.cs) | modified; +3/-2 | Configurable max species | Test table bounds and malformed table lengths; retain retail max. |
| 15 | [personal_bdsplumi](https://github.com/sofianeelhor/PKForge-PKHeX/blob/91e9c88667a6a191a000db68e2db19505eb87561/PKHeX.Core/Resources/byte/personal/personal_bdsplumi) | added; +0/-0 | Binary personal-table resource | Separate game-derived asset rights and format provenance; do not copy automatically. |
| 16 | [text_ItemsLUMI_en.txt](https://github.com/sofianeelhor/PKForge-PKHeX/blob/91e9c88667a6a191a000db68e2db19505eb87561/PKHeX.Core/Resources/text/items/text_ItemsLUMI_en.txt) | added; +1837/-0 | Lumi item-name resource | Separate localisation and content rights review; no blind import. |
| 17 | [SAV8BS.cs](https://github.com/sofianeelhor/PKForge-PKHeX/blob/91e9c88667a6a191a000db68e2db19505eb87561/PKHeX.Core/Saves/SAV8BS.cs) | modified; +1/-1 | Unseal retail save type | Ensure retail BDSP checksum/write behavior unchanged under subclassing. |
| 18 | [SAV8BSLuminescent.cs](https://github.com/sofianeelhor/PKForge-PKHeX/blob/91e9c88667a6a191a000db68e2db19505eb87561/PKHeX.Core/Saves/SAV8BSLuminescent.cs) | added; +37/-0 | Lumi save subclass / checksum override | SetChecksums() is empty because mod hash is unknown; no write certification without independent algorithm. |
| 19 | [Zukan8bLumi.cs](https://github.com/sofianeelhor/PKForge-PKHeX/blob/91e9c88667a6a191a000db68e2db19505eb87561/PKHeX.Core/Saves/Substructures/Gen8/LUMI/Zukan8bLumi.cs) | added; +87/-0 | Lumi 1,025-species Pokédex | Test bit/nibble offsets, species bounds and preservation of unrelated bytes. |
| 20 | [SaveFileType.cs](https://github.com/sofianeelhor/PKForge-PKHeX/blob/91e9c88667a6a191a000db68e2db19505eb87561/PKHeX.Core/Saves/Util/Detection/SaveFileType.cs) | modified; +3/-0 | Lumi file-type discriminator | Test enum routing and unknown save variants. |
| 21 | [SaveUtil.cs](https://github.com/sofianeelhor/PKForge-PKHeX/blob/91e9c88667a6a191a000db68e2db19505eb87561/PKHeX.Core/Saves/Util/SaveUtil.cs) | modified; +13/-1 | Lumi/Compass detection paths | Lumi discriminator is size+high-word marker; Compass size prefilter uses SV hash. Neither proves full mod identity. |

## Critical source findings

### P0-FORK-01 — Luminescent save checksum is intentionally unresolved

The fork [`SAV8BSLuminescent.cs`](https://github.com/sofianeelhor/PKForge-PKHeX/blob/91e9c88667a6a191a000db68e2db19505eb87561/PKHeX.Core/Saves/SAV8BSLuminescent.cs) explicitly overrides `SetChecksums()` with an empty method; its comment says the mod-owned stored hash differs from retail BDSP MD5 and is not yet independently verified. A successful `Write()` on that subclass therefore **cannot by itself demonstrate that an edited save has a valid mod checksum**. Separately, the fork's [`SaveUtil.cs`](https://github.com/sofianeelhor/PKForge-PKHeX/blob/91e9c88667a6a191a000db68e2db19505eb87561/PKHeX.Core/Saves/Util/SaveUtil.cs) classifies Luminescent with a known size and `0xFFFF` high-word revision marker. This is a classifier, not proof of a specific mod version or a valid integrity hash.

**Required:** keep Luminescent read-only in our own official-Core adapter; no migration to fork code and no write switch until independently verified revision-specific hash/checksum algorithm, fixture corpus (including altered header false positives), exact output diff, emulator reload, and clean failure on unknown revisions. An upstream “supports Lumi” label is insufficient.

### P0-FORK-02 — Compass is not a general hack compatibility profile

The fork adds [`CompassBlockKeys.cs`](https://github.com/sofianeelhor/PKForge-PKHeX/blob/91e9c88667a6a191a000db68e2db19505eb87561/PKHeX.Core/Compass/CompassBlockKeys.cs) (1,459 added lines) and a [`SaveUtil.cs`](https://github.com/sofianeelhor/PKForge-PKHeX/blob/91e9c88667a6a191a000db68e2db19505eb87561/PKHeX.Core/Saves/Util/SaveUtil.cs) size window `0x43B000..0x43C000` for Compass v2.1.x, then checks the standard SV save hash. A valid outer SV hash and the presence of named blocks do not independently prove correct Compass block semantics, game revision, or permission to write. Do not extrapolate these identifiers to Shattered or unrelated ROM hacks.

**Required:** versioned block manifest, required/forbidden keys and sizes, collision checks, independent mod-version proof, unknown-block preservation, negative controls from retail SV and other mods, and unchanged/edit/reload tests.

### P1-FORK-03 — fork-wide legality and inventory differences

The [`MiscVerifierG3.cs`](https://github.com/sofianeelhor/PKForge-PKHeX/blob/91e9c88667a6a191a000db68e2db19505eb87561/PKHeX.Core/Legality/Verifiers/Misc/MiscVerifierG3.cs) patch changes the GameCube-to-GBA trash-byte rule for **all** Gen 3 legality checks, not just Lumi. The [`ItemStorage8BDSPLumi.cs`](https://github.com/sofianeelhor/PKForge-PKHeX/blob/91e9c88667a6a191a000db68e2db19505eb87561/PKHeX.Core/Items/ItemStorage8BDSPLumi.cs) `IsLegal` short-circuits true for any `KeyItems` pouch type; whether this is intentionally permissive needs invalid-ID and count tests. [`PlayerBag8b.cs`](https://github.com/sofianeelhor/PKForge-PKHeX/blob/91e9c88667a6a191a000db68e2db19505eb87561/PKHeX.Core/Items/Bags/PlayerBag8b.cs) skips retail `CleanIllegalSlots` for the subclass. These are concrete behavior changes and must not silently replace official rules.

**Required:** same-entity legality A/B against official 26.10.18 and fork, legal/illegal GameCube transfer examples, retail BDSP bag byte-diff regression, Lumi item ID/quantity limits and out-of-range rejection.

### P1-PIN-01 — two different “pinned upstream” baselines

Our [read-only inspector](../src/PKHeXMobile.ReadOnly/ReadOnlySaveInspector.cs) uses NuGet `[26.10.18]`, while the earlier audit used a source head 10 commits newer. Neither is a mistake, but mixing their output as though it were one engine would invalidate parity claims. The adapter's successful same-Core-generated Gen 1 synthetic test is not independent desktop comparison.

**Required:** reproducible restore lockfile/package hash, exact desktop PKHeX build with matching Core, synthetic plus independently authored fixtures, and a documented migration delta before any Core bump. Record each test's engine version and save format, not just a green CI check.

## Disposition / open gate

- **Adopt now:** none of the fork's code or resources. Continue official Core as the reference engine and use source findings only as test cases.
- **Isolate behind a future profile:** Lumi species tables, item rules, Pokédex, hash, Compass block schema. Do not infer official legality or Shattered support.
- **Cross-version regression:** Gen 3 legality change, enum/context switch coverage, Gen 4 Safari/Gen 9 ZA and other official changes since NuGet 26.10.18.
- **Still unverified:** independent upstream builds, full fork-vs-official *tree* reconciliation, actual checksum algorithms, per-game fixtures, device/provider crash behavior, and rights for fork binary personal tables and game-derived strings.

This file is an auditable **source-diff disposition**, not a proof that 21 features work. See [next-pass parity and test gates](PARITY_VERIFICATION_PLAN_2026-10-28.md).
