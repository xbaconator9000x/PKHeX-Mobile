# Text encoding, Unicode, trash-byte and mobile lifecycle acceptance gates — pass J (2026-10-28)

**Status: 40 proposed scenarios; none executed or certified by this audit.** These trace to [26 source-backed findings](UPSTREAM_TEXT_ENCODING_REVIEW_2026-10-28.md), not to verified desktop feature parity. Prior implementation smoke tests and successful Android compilation do not satisfy these new gates. Priorities: 22 P0, 17 P1, 1 P2.

**Pinned revisions:** official PKHeX `7e26bc53215e4f7bc0041ca375f1042539cb81b3` (Core 26.10.18 source-release `bfe89c74a3f5265805e364b79bc04ad38ebbc5ea`); PKForge `2df04430e532b78c247f45b936f3068d8f824987`; PKHeX MAUI `a6f7cfb78e1c96155baa67660fd7ae97895e375f`; PKHeX-Mobile inspected baseline `99ca02e093afb3c18685979ece2a3dda51671b54`.

| Gate | Finding(s) | Priority | Reproducible test and expected outcome | Required evidence | Audit status |
| --- | --- | --- | --- | --- | --- |
| **JG-01** | T-01 | P0 | Table-drive Gen 1–9 dispatch with each field's pinned generation, language and endian; reject unknown/contradictory contexts before mutation. | Core unit; source-byte before/after | **not executed** |
| **JG-02** | T-01,T-02 | P0 | Encode identical names as GBA Gen3 vs GameCube Gen3 and DS Gen4 vs GC Gen4; compare expected byte order and terminator. | Core unit; BE/LE golden vectors | **not executed** |
| **JG-03** | T-03 | P1 | Decode and re-encode Gen1 trade OT marker and 0x50-terminated empty/nonempty strings without losing raw trailing bytes. | Core unit; raw-byte vector | **not executed** |
| **JG-04** | T-03 | P1 | Feed Gen1 JP and Korean-looking bytes and confirm context ambiguity is recorded, not silently treated as save-format proof. | Core unit; negative corpus | **not executed** |
| **JG-05** | T-04 | P1 | Verify Gen2 JP, French/German, Italian/Spanish dictionary behavior and round-trip limits against desktop 26.10.18. | Desktop matched vectors | **not executed** |
| **JG-06** | T-04 | P1 | Exercise Gen2 Korean two-byte characters, malformed final byte and terminator indexing; no buffer overrun or silent conversion claim. | Core unit/fuzz | **not executed** |
| **JG-07** | T-05 | P0 | Supply Gen3 unmappable Unicode in nickname/OT; detect encoded-prefix truncation and reject unintended name changes. | Core unit; semantic/raw diff | **not executed** |
| **JG-08** | T-05 | P0 | Test Gen3 exact maximum, one over maximum and full destination; preserve 0xFF termination and all untouched bytes. | Core unit; boundary vectors | **not executed** |
| **JG-09** | T-06 | P0 | Verify Gen4/5 0xFFFF vs Gen6–9 0x0000 terminators for empty, full and one-short strings. | Core unit; full-field hash | **not executed** |
| **JG-10** | T-07 | P0 | Compare None vs ClearZero, Clear50, Clear7F and ClearFF on seeded nonzero residual bytes; changes require explicit policy. | Core unit; six-mode matrix | **not executed** |
| **JG-11** | T-07 | P0 | For Gen5 ClearZeroSafeTerminate, assert final 16-bit terminator and compare with normal ClearZero at boundary length. | Core unit; boundary vectors | **not executed** |
| **JG-12** | T-08 | P1 | Round-trip male/female symbols under Gen6/7 half-width/full-width contexts; raw private-use glyphs preserved. | Core unit; glyph vectors | **not executed** |
| **JG-13** | T-08 | P1 | Confirm display-only Unicode normalization cannot alter PKM byte hashes when selecting/copying a Pokémon. | Android/presenter unit | **not executed** |
| **JG-14** | T-09 | P1 | Round-trip Gen7 Simplified Chinese reserved/private-use glyphs and language-specific mapping. | Desktop matched vectors | **not executed** |
| **JG-15** | T-09 | P1 | Round-trip Gen7 Traditional Chinese reserved/private-use glyphs and reject a swapped Chinese-language context. | Desktop matched vectors | **not executed** |
| **JG-16** | T-10 | P0 | Use Gen4 PCD gift-derived OT/nickname trash and a one-byte mismatch; legality must report the exact expected distinction. | Core legality vectors | **not executed** |
| **JG-17** | T-10 | P0 | Compare Japanese/non-Japanese Pal Park OT trash rules with matching and mismatching underlayers. | Core legality vectors | **not executed** |
| **JG-18** | T-10 | P0 | Exercise Gen8+ HOME final-terminator and egg/traded-egg trash branches; report results without changing entity. | Core legality vectors | **not executed** |
| **JG-19** | T-11 | P0 | Call nickname setter only on an isolated clone; cancel must leave original name, flag, checksum and all raw bytes unchanged. | Core unit; immutable hash | **not executed** |
| **JG-20** | T-11 | P0 | Clear nickname with nonzero trash and with default species name already displayed; compare all field bytes and flags. | Core unit; full diff | **not executed** |
| **JG-21** | T-12 | P1 | Generate synthetic Gen1/2 templates with distinct OT residual patterns; prove source unchanged and intended copied trash retained. | Core unit; hash | **not executed** |
| **JG-22** | T-13 | P2 | Inventory desktop trash editor's byte, language, generation, special-character and read-only controls against mobile screen routes; mark absent ones missing. | Source crosswalk; UI checklist | **not executed** |
| **JG-23** | T-14 | P0 | Run a Forge-style backwards conversion where target language is unavailable; verify warnings and exact OT/nickname changes, but never write source. | Core/Forge isolated fixture | **not executed** |
| **JG-24** | T-14 | P0 | Force destination name too long/unrepresentable; show the lossy result and refuse implicit conversion. | Conversion negative vector | **not executed** |
| **JG-25** | T-15 | P1 | Export filenames containing bidi controls, line separators, combining sequences, path separators and emoji; enforce safe distinct display names. | Pure filename unit; provider matrix | **not executed** |
| **JG-26** | T-15 | P1 | Test two entries with equal first eight ID characters and visually identical sanitized names; detect collision before any write. | Collision negative vector | **not executed** |
| **JG-27** | T-16 | P1 | Cancel after N of M archive files, before manifest; record incomplete state and verify source bank unchanged and retry idempotent. | Fake folder failure injection | **not executed** |
| **JG-28** | T-16,T-17 | P0 | Import spoofed .pa9/.pk3rr and mismatched manifest SHA/format; reject without using extension or name as format authority. | Bank parser negative corpus | **not executed** |
| **JG-29** | T-18 | P1 | Type, delete and cancel a nickname in a draft; assert original PKM hash unchanged until explicit apply. | UI state unit | **not executed** |
| **JG-30** | T-19 | P1 | Switch display locale without changing PKM.Language, then edit stored language in a draft and cancel; both source hashes unchanged. | UI state unit | **not executed** |
| **JG-31** | T-20 | P1 | Edit OT and handling trainer strings, then cancel; original text and raw trash must remain byte-identical. | UI state unit | **not executed** |
| **JG-32** | T-21,T-22 | P0 | Open and dismiss MAUI-style trash modal using Cancel, Back and system dismissal; waiter always terminates, with bounded CPU and no writes. | Android lifecycle instrumentation | **not executed** |
| **JG-33** | T-23 | P0 | Open PKM and OT trash requests in rapid succession and switch documents; stale response must not modify any field. | Concurrent request identity test | **not executed** |
| **JG-34** | T-24 | P0 | Instrument main-thread UI affinity and field-identity checks on modal result; no background UI mutation or stale target write. | Android UI-thread test | **not executed** |
| **JG-35** | T-21,T-22,T-23,T-24 | P0 | Recreate Activity, revoke document permission and cancel during text edit; all waiters released and original bytes unchanged. | Android lifecycle/device | **not executed** |
| **JG-36** | T-25 | P1 | Present bidi/combining/PUA/invalid-surrogate labels and verify safe display, stable slot identity and no source mutation. | Presenter smoke/device | **not executed** |
| **JG-37** | T-01,T-05,T-06,T-10,T-25 | P0 | Produce independent desktop PKHeX 26.10.18 name/trash oracle with executable and fixture provenance, compare to mobile projection and byte vectors; keep fixtures private. | Desktop-vs-mobile parity manifest | **not executed** |
| **JG-38** | T-26 | P1 | Record copyright/licence provenance for every proposed text/glyph/font asset and source file; exclude unlicensed art and verify GPL source-offer obligations. | Licence/asset audit | **not executed** |
| **JG-39** | T-01,T-10,T-14,T-17 | P0 | Attempt write authorization with vanilla-shaped unsupported ROM-hack strings/species and spoofed extension; no write or Save As path may be enabled. | Negative write-safety gate | **not executed** |
| **JG-40** | T-16,T-25 | P0 | When export is eventually enabled, stage new document, reopen, compare exact bytes/hash, simulate short write and retain original/backup without falsely claiming SAF atomicity. | Transactional export/device | **not executed** |

## Evidence protocol and release policy

1. **Pure tests first:** synthetic, rights-cleared string fields with known raw bytes, generation, game context, language and endianness; seed residual/trash bytes intentionally. Compare whole fields, checksums and source hashes, not just decoded labels.
2. **Independent desktop oracle:** use same-version official PKHeX.Core 26.10.18 and a separately executed desktop PKHeX instance, with SDK/runtime/package hashes, fixture rights and manifest schema recorded. Synthetic tests using one Core assembly twice do not prove desktop parity.
3. **Android negative testing:** inject cancel/Back/rotation/provider revocation, overlapping editors and stale targets. Never copy MAUI's busy polling or background UI mutation. Record actual device/emulator/API/provider versions.
4. **Write-safety release gate:** no writer is currently enabled. A legal-looking Pokémon, file extension, UI text or successful decode never authenticates a ROM-hack save. Any eventual export needs a certified exact layout, independent staged re-open, original-byte backup, full permitted-delta comparison and verified new-document publication.
5. **Rights:** keep private or synthetic fixtures outside the repository; never add ROMs, user saves, keys, game art or non-redistributable fonts. Document GPLv3 obligations for adapted source.

**Handoff order:** P0 JG-32–35 (text-editor lifecycle safety if adapted), JG-01/07–11/16–20 (raw byte fidelity), JG-37/39/40 (independent parity and write authorization), then P1 localization/export-name parity and P2 desktop UI crosswalk. Do not mark any gate complete from source inspection alone.
