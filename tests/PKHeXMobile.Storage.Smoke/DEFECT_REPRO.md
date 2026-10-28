# Pinned PKHeX.Core storage-defect runtime reproductions

The Storage.Smoke Program.cs suite now executes two high-risk storage diagnostics
against the exact PKHeX.Core [26.10.18] NuGet pin. It never opens a user save,
writes a file or enables storage rearrangement in PKHeX Mobile.

- N01/N02 — slot pointer remapping: SlotPointerUtil.UpdateSwap(0,1,30)
  and UpdateMove(0,1,30) are exercised with multiple ordinary, boundary,
  out-of-range and negative synthetic references. The pinned Core currently
  leaves them unchanged. The test also checks a separately calculated swap
  oracle and confirms the Core result differs.
- N03 — box identity loss: a blank synthetic SAV9SV gets distinct
  species in boxes 0 and 1, then invokes MoveBox(0,1) in memory. The
  expected pinned behavior duplicates box 0 into box 1, losing the former
  box 1 identity.

These are known-defect regression sentinels: they assert observed bad
behavior so a dependency update cannot silently change semantics without
review. They are not correctness tests for a fixed implementation. If upstream
fixes the methods, the assertions must be replaced with conservation,
pointer-mapping, protected-slot and independent desktop round-trip gates.
This suite does not prove actual user-save data loss, protect against every
reordering hazard, authenticate ROM-hack layouts, or authorize any write.
Source-level audit: docs/UPSTREAM_STORAGE_SLOT_REVIEW_2026-10-28.md;
proposed gates: docs/PARITY_STORAGE_SLOT_GATES_2026-10-28.md.
