# Read-only high-risk input preflight (implementation note)

This module is a **negative-only screen**, never an official-game authenticator or write-compatibility test. It rejects known risky envelopes *before* the pinned retail PKHeX.Core parser could produce misleading party/box projections. Unrecognized ROM hacks can still parse as retail, even when every checksum passes. All formats remain read-only and export remains disabled.

Source-grounded patterns, reviewed at immutable upstream commits:

- [PKForge SaveParser](https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/src/PKForge.Engine/SaveParser.cs) supplies the 8-sector threshold for Unbound `0x01121999` and GS Chronicles `0x66290096` signatures, plus the Luminescent Platinum size/high-revision marker. The pattern is only an indicator; the app does not load or edit these hacks.
- [PKForge RadicalRedFormat](https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/src/PKForge.Engine/RadicalRed/RadicalRedFormat.cs) supplies the 14 CFRU and retail FRLG checksum windows and the section-footer offsets. This implementation independently computes both checksum families for **risk rejection only**. A match does not establish a specific hack/version. The retail window table is not a general Ruby/Sapphire/Emerald classifier.
- [Official PKHeX SAV3](https://github.com/kwsch/PKHeX/blob/bfe89c74a3f5265805e364b79bc04ad38ebbc5ea/PKHeX.Core/Saves/SAV3.cs) documents 14 sector fragments per save slot, retail sector footer fields and checksum recomputation during serialization. This explains why retail-shaped hacks must not be serialized with the wrong sector layout.

Executable smoke tests synthesize marker-positive and near-miss envelopes, a CFRU checksum-window positive and invalid-checksum negative. These are not independently sourced real game saves, do not prove actual Radical Red/Unbound/GS Chronicles compatibility, and do not certify retail format identity. Do not replace source-level findings or parity gates with these tests.

**Security boundary:** The current scanner is deliberately incomplete; a new hack revision, a different CFRU window layout, altered species IDs, a padded/embedded container, or a valid vanilla checksum over custom data may evade it. No UI or downstream API may turn the absence of a risk marker into `SupportedWrite`. The only permitted result from the reader is `SupportedReadOnly` with a null verified profile.
