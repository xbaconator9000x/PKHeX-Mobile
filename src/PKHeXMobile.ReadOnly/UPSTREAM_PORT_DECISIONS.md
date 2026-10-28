# Upstream port consolidation decisions — implementation track

This is an implementation-owned adoption decision, **not** a replacement for the independent UPSTREAM_/PARITY_ audit. Snapshot date: 2026-10-28. The current Android application uses native .NET for Android; PKForge and PKHeX MAUI use MAUI. Do not claim either full application has been ported.

| Upstream component (immutable revision) | Decision | Evidence and integration gate |
| --- | --- | --- |
| Official PKHeX.Core 26.10.18, release bfe89c74a3f5265805e364b79bc04ad38ebbc5ea | **Adopt** | Exact NuGet \`[26.10.18]\` in ReadOnly project; engine of record for parsing and legality. Same-Core synthetic tests are not independent desktop fixtures. |
| PKForge \`LegalityChecks.cs\` at 2df04430e532b78c247f45b936f3068d8f824987 | **Adapted now** | Grouping and worst-severity logic ported to \`PortedLegalityChecks\`; no forked Core, MAUI, assets or mutating suggestions. Official Core's \`CheckResult\`, \`LegalityLocalizationContext\` and \`LegalityAnalysis\` APIs verified at the pinned release. Tested with synthetic checks and PK1 diagnostic. |
| PKHeX MAUI \`pktabs/PKTab.xaml.cs\` at a6f7cfb78e1c96155baa67660fd7ae97895e375f | **Adapt API pattern** | Uses \`new LegalityAnalysis(pk, sav.Personal)\`; we use the official Core's save-specific personal table for a detached **party-only** diagnostic. Its global mutable \`pk\`/\`sav\`, automatic legalizer, network/injection and storage permission flow are **not imported**. |
| PKForge \`SafeSaveWriter.cs\` and \`SaveSessionService.cs\` at 2df04430e532b78c247f45b936f3068d8f824987 | **Adapt only after safety gates** | Existing validation, backup and race-protection concepts are useful; direct writer import is blocked by forked Core, platform abstraction and user-confirmable suspected-hack write risk. Current domain staging preflight remains non-publishing. |
| PKForge \`BoxBrowserPage.cs\` at the same revision | **Reject direct import for now** | ~272 KB landscape-oriented MAUI/SkiaSharp/controller UI with app-specific theme, sprites, view model and forked Core dependencies. Preserve existing native Android read-only grid until a complete MAUI migration can be compiled and compared on devices. |
| PKHeX MAUI \`boxtab/BoxTab.xaml.cs\` at the same MAUI revision | **Reject direct import for now** | Global mutable save, drag/drop slot setters, remote injection, asset references and no proven transactional save boundary. Revisit the editor UX after verified write fixtures. |
| PKForge hack parsers and Unbound/Radical Red/GS Chronicles tables | **Reject for vanilla writes** | Fork-specific data and separately sourced tables require per-hack provenance and exact versioned layout fixtures. An official Core parse does not authenticate a hack save. |

## Scope of this integration

- Only **occupied party slots** are analyzed (maximum six). An all-box legality sweep would be expensive and lacks a lifecycle/cancellation budget; box slots explicitly say "not evaluated."
- The analysis operates on \`PKM.Clone()\`, uses the same save-specific personal table as PKHeX MAUI's pattern, groups findings using PKForge's reviewed logic, and exposes bounded immutable diagnostics.
- Legality is excluded from the JSON parity manifest until independently matched desktop PKHeX 26.10.18 fixture expectations exist. A provisional valid verdict does not authenticate ROM-hack species/abilities, trade provenance, or permit writing.
- PKForge code adaptation is attributed in source and remains under GPLv3. No third-party artwork, ROMs, saves, secrets or assets are imported.
- Next consolidation target: compare PKForge's save editor service contracts against the existing non-publishing staging pipeline with same-version desktop fixtures and Android provider fault injection, **before** enabling export.
