# This is a test.

**Porting environment** for a complete, Android-first Pokémon save editor, with the long-term goal of desktop PKHeX feature parity and optional integration with PokéBacon Studio.

> **Status: experimental read-only Android debug APK builds in CI, not device-certified. There is no save editor or export yet.** This is an independent project, not endorsed by PKHeX's maintainers, Nintendo, Game Freak or The Pokémon Company.

## Goals

- Reuse the maintained **PKHeX.Core** engine for Pokémon parsing, legitimacy/legality checks, game rules and save serialization instead of developing a divergent parser.
- Design proper touchscreen navigation for Pokémon and boxes, party, trainer, inventory, Pokédex, mystery gifts, event flags, batch tools and generation-specific editors.
- Make save safety mandatory: independently verify file identity and save format, keep untouched originals, take backups, validate every serialized output and refuse unsupported or ambiguous saves.
- Add **individually verified** ROM-hack profiles, including later PokéBacon Studio interoperability, without conflating custom data with official-game legality.
- Pin and test upstream revisions; document every parity gap rather than treating a basic viewer as a full port.

## Architecture direction

**C# / .NET 10 + PKHeX.Core + Android-first UI** is the initial direction, not an irrevocable implementation decision. Before copying existing mobile UI code, audit [PKForge](https://github.com/sofianeelhor/PKForge), [PKHeX MAUI](https://github.com/santacrab2/PKHeXMAUI), and desktop [PKHeX](https://github.com/kwsch/PKHeX) against their actual source.

## Repository

- `src/PKHeXMobile.Domain`: bounded, immutable input snapshots and fail-closed save-write contracts.
- `src/PKHeXMobile.ReadOnly`: official PKHeX.Core 26.10.18 read-only adapter and two-open document verification.
- `src/PKHeXMobile.Android`: experimental scoped-storage Android picker, trainer/party view and read-only box slot grid.
- `src/PKHeXMobile.Inspect` and `src/PKHeXMobile.Verify`: desktop diagnostics and private-fixture comparison tools.
- `tests/PKHeXMobile.Domain.Smoke`: simple domain contract checks.
- `docs/ROADMAP.md`: milestone acceptance criteria.
- `docs/ARCHITECTURE.md`: candidate subsystems and trust boundaries.
- `docs/SAFETY.md`: save safety and real-world verification gates.
- `docs/UPSTREAM_REVIEW.md`: code audit and feature parity plan.
- `docs/POKEBACON_INTEGRATION.md`: future interoperability design.
- `THIRD_PARTY.md`: attribution and upstream import rules.

## Check the foundation

With the .NET 10 SDK and Python 3 installed:

```sh
dotnet build src/PKHeXMobile.Domain/PKHeXMobile.Domain.csproj --configuration Release
dotnet run --project tests/PKHeXMobile.Domain.Smoke/PKHeXMobile.Domain.Smoke.csproj --configuration Release
python3 -m unittest discover -s tests -p 'test_*.py' -v
python3 scripts/check_repository.py
```

The Android UI and pinned PKHeX.Core adapter are present, but **independently validated real-save fixtures, physical-device/provider tests and any safe save writer are still missing**. CI compilation and same-Core synthetic smoke tests do not certify game compatibility or export safety.

## Licence and safety

This repository is GPL-3.0-only; see [LICENSE](LICENSE). Preserve any imported code's copyright and licence notices. Never commit copyrighted game ROMs, console keys, proprietary game assets, private saves or APK signing material. Do not use altered Pokémon to deceive people in online trades or competition.

[PokéBacon Studio](https://github.com/xbaconator9000x/pokebacon-studio) remains a separate project until a validated, compatible integration is designed.
