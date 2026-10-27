# Upstream source checkpoint — 2026-10-27

This is an **initial source inspection**, not a build/run verification or full desktop parity audit. The SHAs below identify the upstream heads inspected; they are not yet vendored dependencies or a commitment to use any particular implementation.

## Source snapshots

| Source | Branch | Inspected commit | Key entry points |
| --- | --- | --- | --- |
| [PKHeX](https://github.com/kwsch/PKHeX) | `master` | `7e26bc53215e4f7bc0041ca375f1042539cb81b3` | `PKHeX.Core/`, `PKHeX.WinForms/`, `Tests/` |
| [PKForge](https://github.com/sofianeelhor/PKForge) | `main` | `2df04430e532b78c247f45b936f3068d8f824987` | `src/PKForge.Engine/`, `Infrastructure/`, `App/` |
| [PKHeX MAUI](https://github.com/santacrab2/PKHeXMAUI) | `pkhexmobile` | `a6f7cfb78e1c96155baa67660fd7ae97895e375f` | `PKHeX.MAUI.csproj`, `SaveEditors/`, `pktabs/` |

## Verified findings from files

1. **PKHeX.Core is independently compiled as .NET 10.** Its [project file](https://github.com/kwsch/PKHeX/blob/7e26bc53215e4f7bc0041ca375f1042539cb81b3/PKHeX.Core/PKHeX.Core.csproj) declares `net10.0` and `GPL-3.0-or-later`. The engine has separate Saves, PKM, Legality, MysteryGifts, Editing and other modules. This supports using it as our canonical format engine.

2. **PKForge already has useful Android and safety architecture.** The [Android project](https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/src/PKForge.App/PKForge.App.csproj) targets `net10.0-android` and MAUI, with separate Domain, Engine, Infrastructure, App, Chrome and AutoMod projects. The [safe writer](https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/src/PKForge.Infrastructure/SafeSaveWriter.cs) implements candidate validation, layout-risk checks, slot-scope changes, backup-before-write and platform atomic-write calls. These are good design references, but **their behavior has not yet been executed or verified in our tests**.

3. **PKForge does not directly pin upstream kwsch/PKHeX.** Its [submodule configuration](https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/.gitmodules) points `external/PKHeX` at the `sofianeelhor/PKForge-PKHeX` fork. The [engine project](https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/src/PKForge.Engine/PKForge.Engine.csproj) references that submodule. **Maintenance concern:** adapting the entire project may also mean inheriting a forked Core and its divergence; audit exact differences and update burden before choosing it.

4. **PKForge documents a concrete ROM-hack data-corruption incident.** In [WriteSafety.cs](https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/src/PKForge.Engine/WriteSafety.cs), maintainers describe a CFRU-derived save misinterpreted as vanilla FireRed, resulting in invalid Pokémon decoding and sector rewrites. The code now attempts layout-risk identification, unchanged round-trip checks and scoped-slot comparisons. This is upstream's documented incident and mitigation, **not** an independently reproduced incident by this project. Its lesson directly informs our fail-closed format authentication and write safeguards.

5. **PKForge's custom save recognition uses explicit routes.** Its [SaveParser.cs](https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/src/PKForge.Engine/SaveParser.cs) checks certain custom save signatures and structures, including Unbound, GS Chronicles, Radical Red and Luminescent Platinum. We must examine false positives, version changes and recovery cases before enabling custom writes for a new hack.

6. **PKHeX MAUI is a different starting point.** Its [project configuration](https://github.com/santacrab2/PKHeXMAUI/blob/a6f7cfb78e1c96155baa67660fd7ae97895e375f/PKHeX.MAUI.csproj) references `PKHeX.Core` NuGet package version `26.7.7` and targets multiple MAUI platforms, including `net10.0-android36.0`. Unlike PKForge's referenced source project, a package dependency could simplify upstream updates; feature coverage and adapted code still require verification. Its README expressly warns against ROM-hack use.

## Provisional recommendation (not final architecture decision)

Use **PKHeX.Core** as the engine of record. Evaluate PKForge's save validation, Android UI, device support and tests as a leading **reference/adaptation candidate**, but audit the PKForge-PKHeX fork and separately licensed art first. Compare PKHeX MAUI for simpler upstream package tracking and potentially easier maintenance. Do not import either mobile UI yet.

## Next audit gates

- Build all three pinned source snapshots with recorded toolchains, without copying copyrighted game data.
- Inspect Android document providers and confirm actual recovery/in-place-save behavior under exceptions.
- Check desktop WinForms editors against mobile implementation files to populate an itemized parity matrix.
- Cross-test fixtures, official and custom formats, negative cases and unchanged round trips.
- Audit forks, plugin dependencies, binary references, media assets and GPL-compatible redistribution.
- Record the UI/adaptation decision before adding upstream code.

No APK, PKHeX.Core integration or real save tests exist in **this** repo yet.
