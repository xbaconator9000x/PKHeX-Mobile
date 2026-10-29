# Provisional architecture

The architecture is deliberately **not locked in** until PKHeX, PKForge and PKHeX MAUI are source-audited. Do not port the desktop WinForms UI blindly. Reuse maintained game-specific behavior from a pinned PKHeX.Core.

## Candidate layers

1. **Domain contracts:** compatibility decisions and typed editing requests, with no Android dependencies.
2. **PKHeX.Core adapter:** exact dependency pin, format detection, parsing, legality, editing and serialization via testable interfaces.
3. **Save I/O service:** scoped document-provider access, fingerprints, backups, staged output, read-back validation and rollback.
4. **Android presentation layer:** responsive party/boxes, Pokémon inspector, advanced game-specific editors, Back navigation and accessibility.
5. **Hack profiles:** authenticated format/version matching, custom entities and per-capability write permissions.
6. **Interoperability boundary:** a documented, versioned profile exchange with PokéBacon Studio, not direct database sharing.

## Hard boundaries

A save is not a ROM. A ROM label, game code, user-supplied profile name or file extension never by itself authorizes modifying a save. Unknown/custom schemas are read-only unless independently validated. Any legal Pokémon result is relative to the matching official/custom game rules and PKHeX.Core revision.

## Upstream policy

Document every upstream SHA, dependency and licence. Bump PKHeX.Core only with fixtures and tests. Never import artwork under a code licence assumption.

## Pending decisions

- Adapt PKForge vs PKHeX MAUI vs develop a new UI.
- PKHeX.Core pin, .NET / Android API baseline and build process.
- Full desktop parity inventory, device support and real-save test corpus.
- Precise PokéBacon integration/licensing boundary.
