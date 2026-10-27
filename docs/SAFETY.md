# Save integrity specification

**No save reader or writer is implemented in the foundation.** The example SaveWriteGuard is an authorization *contract*, not evidence a file is safe to change.

## Before any real write is allowed

1. **Verify identity:** detect exact supported save format, game and layout revision using a pinned, tested PKHeX.Core and verified profile. Unknown or conflicted input must be read-only.
2. **Protect original:** Save As by default; provide an explicit, restorable original-byte backup before a requested in-place write.
3. **Avoid races:** recheck file identity before writing; handle replaced documents, stale edits, permission revocation and process interruptions.
4. **Write to staging:** serialize into temporary output and independently reopen/validate format, checksum/block semantics and requested edits before publishing.
5. **Compare changes:** check all intended fields and record unexpected changes in untouched regions, including custom/unknown data.
6. **Recover:** never publish partial or corrupted output; prove backup restoration reproduces exact input bytes.
7. **Scope capabilities:** custom hacks require per-profile independent validation; a filename or JSON profile cannot authorize writes.

## Minimum regression fixtures

Representative official saves for each advertised game, synthetic negative/truncated files, unsupported variations with plausible headers, stale-file replacements, save-size changes, interrupted writes and emulator import/reload.

Do not put user saves, actual proprietary ROMs or console keys in this repository. Record the provenance and rights of any fixture.
