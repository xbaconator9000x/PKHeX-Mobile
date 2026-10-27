# Future PokéBacon Studio interoperability

PKHeX Mobile is deliberately independent while save editing, licensing and mobile reliability mature. The longer-term product goal is an all-in-one PokéBacon Studio workflow.

## Possible exchange format

A versioned manifest can describe exact game/ROM fingerprint, save-layout revision, custom species/moves/abilities/items and numeric IDs, and explicitly supported editing capabilities. PokéBacon may author a manifest, but PKHeX Mobile must **independently verify** identity and supported semantics before permitting a write.

Do not treat map-editor JSON, displayed species names or ROM header text as verified evidence. Preserve unsupported bytes and warn about loss before any conversion.

Example future flow: create a ROM in PokéBacon, export verified profile metadata, open emulator save in PKHeX Mobile, review matching identity, make supported edits, export a verified new save, reload in emulator.

## Licensing

PKHeX.Core is GPLv3. Sharing derived code with PokéBacon could impose GPL obligations on a combined distribution. Prefer a documented data-interchange boundary first; review licences before embedding either program within the other.

**No integration or profiles are implemented.**
