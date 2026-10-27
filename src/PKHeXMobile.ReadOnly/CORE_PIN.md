# PKHeX.Core read-only integration pin

This experimental adapter references the official NuGet package [PKHeX.Core 26.10.18](https://www.nuget.org/packages/PKHeX.Core/26.10.18) with the exact version range `[26.10.18]` (no floating updates). The matching upstream [release tag `26.10.18`](https://github.com/kwsch/PKHeX/tree/bfe89c74a3f5265805e364b79bc04ad38ebbc5ea) resolves to commit `bfe89c74a3f5265805e364b79bc04ad38ebbc5ea`. The GitHub tag identity and NuGet package version have been checked; **a cryptographic package-to-commit reproducible build comparison has not been performed**.

The audit's newer reference commit `7e26bc53215e4f7bc0041ca375f1042539cb81b3` is **not** the dependency in this adapter. Changes since the package release require explicit source and fixture comparison before any dependency upgrade. No code or artwork is copied from the mobile forks.

## Current capability boundary

- A bounded stream reader and immutable SHA-256 snapshot support non-seekable document-provider input. A second read detects changed bytes.
- `ReadOnlySaveInspector.TryInspect` calls the official Core save detector on a private copy and projects trainer, party and occupied box slots **only when Core recognizes the save, reports valid checksums, and layout caps are met**.
- ZIP and GZip inputs are rejected before calling Core; archives are not supported.
- All projections are **provisional** and explicitly `SupportedReadOnly`, with no verified profile. There is no Save As, in-place write, serialization, checksum repair, ROM-hack recognition, or Android UI.
- Existing smoke tests are synthetic **negative cases only**. A passing CI build does not certify a single game format. Positive fixtures, game-specific semantic comparison against desktop PKHeX `26.10.18`, malformed-header and false-positive tests, Android SAF provider tests and independent checksum validation are required before advertising format support.
- Any plausible vanilla identification may still be a custom hack. No save may be written based solely on this adapter.
