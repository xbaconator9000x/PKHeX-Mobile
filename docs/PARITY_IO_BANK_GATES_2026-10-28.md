# Container, Android SAF and bank acceptance gates — pass C (2026-10-28)

**Evidence status: all scenarios below are proposed / NOT EXECUTED.** These are deterministic test designs for the implementation watchdog, not claims of Android functionality or completed desktop parity. Sources: [focused source audit](UPSTREAM_IO_BANK_REVIEW_2026-10-28.md), [general verification plan](PARITY_VERIFICATION_PLAN_2026-10-28.md), [safety specification](SAFETY.md). Never commit personal saves, ROMs, console keys, extracted commercial artwork or private fixture manifests.

## Fixture manifest and provenance contract

Each fixture must record: test ID; rights/provenance; original length and SHA-256; **outer container** type and header/footer hash if any; inner save generation, exact edition/revision where independently known; independent baseline method; Core NuGet version and source tag; toolchain; expected acceptance/refusal; and whether the test is synthetic, desktop-compared, Android-provider-tested or recovery-verified. Keep real fixtures private. The existing [offline verifier](https://github.com/xbaconator9000x/PKHeX-Mobile/blob/main/src/PKHeXMobile.Verify/USAGE.md) compares only trainer/party/box projections and is **not** a wrapper, provider, backup or bank certifier.

Use **same-version desktop PKHeX 26.10.18** for mobile Core comparisons; repeat relevant source-specific behavior separately against official audit SHA 7e26bc53215e4f7bc0041ca375f1042539cb81b3. Preserve exact original bytes for every negative case. A positive parser result never authorizes a write.

## Concrete acceptance matrix

| ID | Priority | Synthetic or consented input / failure injection | Required observable outcome | Gate |
| --- | --- | --- | --- | --- |
| C-01 | P1 | Gen 3 raw 0x20000 and 0x10000 saves, independently verified active-sector order | Identical inner projection to matching desktop Core; original SHA unchanged | desktop |
| C-02 | P1 | Gen 3 raw plus 16-byte RTC footer; mutate footer independently | No footer loss; changed footer reported; exact outer bytes on read-only open | desktop |
| C-03 | P1 | Gen 1/2/3 RTC footer sizes accepted by [official handler](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/Saves/Util/Recognition/SaveHandlerFooterRTC.cs) and near-miss sizes | Only independently verified envelopes recognized; preserve every footer byte | desktop |
| C-04 | P1 | DeSmuME .dsv (0x80000 + 0x7A) with valid and corrupted terminal marker | Correct handler selection; malformed marker rejected; footer unchanged | desktop |
| C-05 | P1 | ARDS .duc (0x80000 + 0xA4), valid and forged header | Size-only detection is not identity proof; unsupported/ambiguous identity write-locked | desktop |
| C-06 | P1 | GameCube GCI with 0x40 header and valid/invalid game code, plus whole memory-card image | Exact header preservation; no treating a whole memory card as a single .gci | desktop |
| C-07 | P1 | NSO SRAM wrapper with correct/incorrect hash and valid/invalid header length | Wrapper/hash validation recorded; no silent hash repair on read-only open | desktop |
| C-08 | P1 | RetroArch #RZIP valid chunked envelope and truncated/oversized/wrong-Adler variants | Clear unsupported-container refusal in current reader; no decompression or write unless separately gated | unit |
| C-09 | P1 | GBA padded 0x00/0xFF tails, exact chip-size erased upper half, random non-pad tail | Exact-size save never trimmed; unknown tail preserved; no false success from shape alone | desktop |
| C-10 | P1 | Byte-identical wrapper roundtrip, then one controlled inner mutation | Unchanged outer bytes byte-identical; controlled change preserves unrelated wrapper and checksum rules | write-locked |
| S-01 | P0 | SAF provider returning only a non-seekable content stream, no filesystem path | Read-only projection via stream, no FullPath, no broad-storage grant | device |
| S-02 | P0 | Read-only provider; URI permission revoked between two opens | Explicit refusal; original unchanged; no write or success toast | device |
| S-03 | P0 | Provider returns A then B bytes on second read; same URI and display name | SHA mismatch refusal, not stale data display | unit/device |
| S-04 | P0 | Provider gives same bytes but different URI/document ID after replacement | No automatic identity equivalence or write permission | device |
| S-05 | P0 | Short/zero-length reads and >32 MiB stream from provider | Bounded refusal, no unbounded allocation or parser invocation | unit/device |
| S-06 | P0 | New-document Save As with short write, lost connection, no-space and readback mismatch | Failure reported, partial output quarantined, original and verified backup unchanged | write-locked |
| S-07 | P0 | Explicit overwrite race after initial fingerprint, cancellation during write | Fresh source check; no overwrite of changed source; recoverable exact original | write-locked |
| B-01 | P0 | Bank Replace(id) with index.json.tmp blocked by a directory | Previously stored entity bytes **and** index metadata identical after failure and restart | unit |
| B-02 | P0 | Bank Replace(id) interrupted after partial entity write | Old entity remains retrievable; no index-to-blob mismatch | recovery |
| B-03 | P0 | Bank export interrupted after first of N files, old manifest present | Old complete archive remains usable or new export marked incomplete; never false success | device |
| B-04 | P1 | Bank manifest lists wrong hash, missing file, duplicate name or extra unlisted file | Verified-import mode rejects mismatches; loose-import mode clearly marked unverified | unit |
| B-05 | P1 | Backup .bin exists but sidecar write fails, or sidecar exists with corrupted bytes | No overwrite authorised until backup has been read back and verified; recovery point retained | recovery |
| B-06 | P1 | Backup retention/pruning during failure, last known-good version | Never delete the only verified recovery point; original SHA restorable | recovery |
| B-07 | P1 | Bank index.json corrupt, .bak valid but entity blob changed | Index fallback alone insufficient; blob hash mismatch detected and write locked | unit |
| L-01 | P1 | MAUI 26.7.7, our 26.10.18 and official newer audited source on same independent fixture | Engine versions reported separately; no cross-version parity pass | desktop |

**24 rows**: C-01–C-10 (10), S-01–S-07 (7), B-01–B-07 (7), L-01 (1) = **25 rows**. Treat the explicit row IDs as authoritative; the total is 25.

## Execution order, ownership and stopping rules

1. **Implementation watchdog owns tests/code:** first C-08 and S-01–S-05 as pure read-only negative tests; add explicit RZIP refusal and a provider identity abstraction without Android dependencies. Preserve the existing reader's 32 MiB cap and non-mutating behavior.
2. Next build **private** same-Core desktop baselines for C-01–C-07/C-09, then verify positive/negative format boundaries. A wrapper detector's success is not proof of game identity.
3. Add a disposable in-memory/fault-injection bank prototype and B-01/B-02/B-04/B-07 tests before introducing any bank storage. Do not port the observed PKForge Replace write order.
4. Android SAF read-only picker and device provider matrix S-01–S-05 must pass before promising APK save inspection. Do not request MANAGE_EXTERNAL_STORAGE for this path.
5. **All S-06/S-07, B-03/B-05/B-06 and C-10 remain write-locked** until independently verified source identity, backup readback, safe staged publication and post-publish verification are proven on real providers.
6. Audit watchdog owns the source citations, matrix status and parity documentation; it must not modify src/, tests/, build or README. Future PokéBacon interchange is separately versioned and authenticated, never silently treated as a vanilla PKHeX save.

No row has been executed in this source-audit pass. Mark each later as source-inspected, unit-passed, desktop-compared, device-passed, write-certified or blocked; never substitute one status for another.
