using System.Text.Json;
using PKHeX.Core;
using PKHeXMobile.Domain;
using PKHeXMobile.ReadOnly;

static void Must(bool ok, string message)
{
    if (!ok) throw new Exception(message);
}
static ExpectedPokemonStatSpread Spread(PokemonStatSpread s) =>
    new(s.HP, s.Attack, s.Defense, s.SpecialAttack, s.SpecialDefense, s.Speed);
static ExpectedPokemonMoveIds Moves(PokemonMoveIds m) =>
    new(m.First, m.Second, m.Third, m.Fourth);

// Deliberately synthetic. Source-byte invariance is tested after constructing
// each entity; the fixture is NOT an independently baselined desktop save.
var modern = new PK9
{
    Species = 25, PID = 0x12345678, EncryptionConstant = 0xA1B2C3D4,
    ID32 = 0x12340056, MetLocation = 500, EggLocation = 600,
    MetYear = 26, MetMonth = 10, MetDay = 28,
    EggYear = 25, EggMonth = 2, EggDay = 15,
    Ball = 4, MetLevel = 10, OriginalTrainerFriendship = 77,
};
var before = modern.Data.ToArray();
var pokemon = ReadOnlySaveInspector.ProjectPokemon(0, modern);
var d = pokemon.Details!;
var id = d.Identity!;
Must(modern.Data.SequenceEqual(before), "Read-only identity projection changed PK9 bytes");
Must(id.PersonalityId == modern.PID && id.EncryptionConstant == modern.EncryptionConstant &&
    id.TrainerId32 == modern.ID32 && id.ShinyXor == modern.ShinyXor,
    "PID/EC/ID32/XOR getter mismatch");
Must(id.MetLocation == 500 && id.EggLocation == 600,
    "16-bit met/egg locations were truncated");
Must(id.MetDate.IsoDate == "2026-10-28" && id.EggDate.IsoDate == "2025-02-15" &&
    id.MetDate.Year == 26 && id.EggDate.Month == 2,
    "Nullable dates or raw date components were lost");
Must(id.IsEgg == pokemon.IsEgg && id.IsShiny == pokemon.IsShiny &&
    id.WasEgg == modern.WasEgg, "Egg/shiny state diverged from pinned Core");

modern.PID = 0;
modern.MetLocation = 2;
modern.EggDay = 0;
Must(id.PersonalityId == 0x12345678 && id.MetLocation == 500 &&
    id.EggDate.Day == 15, "Identity snapshot retained mutable PKM data");

var invalidDate = new PK9 { Species = 25, MetYear = 26, MetMonth = 13, MetDay = 31 };
var invalidBefore = invalidDate.Data.ToArray();
var invalid = ReadOnlySaveInspector.ProjectPokemon(0, invalidDate).Details!.Identity!;
Must(invalid.MetDate.IsoDate is null && invalid.MetDate.Month == 13 &&
    invalidDate.Data.SequenceEqual(invalidBefore),
    "Invalid calendar bytes must remain raw, not become today's date");

var classic = new PK1 { Species = 25 };
var classicBefore = classic.Data.ToArray();
var classicId = ReadOnlySaveInspector.ProjectPokemon(0, classic).Details!.Identity!;
Must(classicId.CoreFormat == 1 && classicId.MetDate.IsoDate is null &&
    classic.Data.SequenceEqual(classicBefore),
    "Classic virtual defaults must remain diagnostic, not synthesized dates");

var actual = new SaveReadOnlyResult("SYNTHETIC", "SV", 9, "TEST", 1, 2, 1, 0, 0,
    [pokemon], [], new SaveFingerprint(1, "TEST"));
var details = new ExpectedPokemonReadOnlyDetails(d.Nature, d.AbilityId,
    d.HeldItemId, d.Gender, d.Form, Moves(d.Moves),
    Spread(d.IndividualValues), Spread(d.EffortValues)) { Identity = id };
var slot = new ExpectedPokemonSlot(0, pokemon.Species, pokemon.Nickname,
    pokemon.Level, pokemon.IsEgg, pokemon.IsShiny) { Details = details };
var expected = new ExpectedSaveProjection("SYNTHETIC", "SV", 9, "TEST", 1, 2, 1, 0, 0,
    [slot], []);
Must(SaveProjectionVerifier.Compare(actual, expected, requireIdentity: true).Count == 0,
    "Schema v10 synthetic matching reference rejected");

void Detect(PokemonIdentityReadOnly altered, string field)
{
    var baseline = expected with { Party = [slot with {
        Details = details with { Identity = altered } }] };
    var errors = SaveProjectionVerifier.Compare(actual, baseline, requireIdentity: true);
    Must(errors.Any(s => s.Contains(field, StringComparison.Ordinal)),
        "Schema v10 failed to reject " + field);
}
Detect(id with { PersonalityId = id.PersonalityId + 1 }, ".PersonalityId mismatch");
Detect(id with { EncryptionConstant = 0 }, ".EncryptionConstant mismatch");
Detect(id with { TrainerId32 = 0 }, ".TrainerId32 mismatch");
Detect(id with { ShinyXor = 0 }, "inconsistent ShinyXor");
Detect(id with { MetLocation = 244 }, ".MetLocation mismatch");
Detect(id with { EggLocation = 88 }, ".EggLocation mismatch");
Detect(id with { Ball = 1 }, ".Ball mismatch");
Detect(id with { MetLevel = 99 }, ".MetLevel mismatch");
Detect(id with { AbilityNumber = id.AbilityNumber + 1 }, ".AbilityNumber mismatch");
Detect(id with { PidAbility = 5 }, "invalid PidAbility");
Detect(id with { IsEgg = true, WasEgg = false }, "inconsistent egg history");
Detect(id with { WasEgg = !id.WasEgg }, ".WasEgg mismatch");
Detect(id with { IsShiny = !id.IsShiny }, ".IsShiny mismatch");
Detect(id with { MetDate = id.MetDate with { Month = 11 } }, "inconsistent ISO date");
Detect(id with { EggDate = id.EggDate with { IsoDate = null } }, ".EggDate.IsoDate mismatch");
Detect(id with { MetDate = null! }, ".MetDate missing raw date components");
Detect(id with { CoreFormat = 0 }, "unsupported CoreFormat");
Detect(id with { OriginGeneration = 10 }, "invalid OriginGeneration");

var missing = expected with { Party = [slot with {
    Details = details with { Identity = null } }] };
Must(SaveProjectionVerifier.Compare(actual, missing, requireIdentity: true)
    .Any(s => s.Contains("Identity missing from expectation")),
    "Schema v10 accepted missing identity");
Must(SaveProjectionVerifier.Compare(actual with { Party = [pokemon with {
    Details = d with { Identity = null } }] }, expected, requireIdentity: true)
    .Any(s => s.Contains("Identity missing from observed projection")),
    "Schema v10 accepted missing observed identity");
Must(SaveProjectionVerifier.Compare(actual, missing).Count == 0,
    "Schema v9 compatibility unexpectedly required identity");

try
{
    JsonSerializer.Deserialize<PokemonIdentityReadOnly>("""{"coreFormat":9}""",
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    throw new Exception("Missing required identity fields accepted");
}
catch (JsonException) { }
try
{
    JsonSerializer.Deserialize<PokemonDateReadOnly>("""{"year":26,"month":10,"day":28}""",
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    throw new Exception("Missing nullable ISO date field accepted");
}
catch (JsonException) { }

var display = ReadOnlyProjectionPresenter.FormatPartySlotDetails(new BoxCell(0, pokemon));
Must(display.Contains("Identity (raw Core getters, not legality)") &&
    display.Contains("Met location ID: 500; egg location ID: 600") &&
    display.Contains("2026-10-28") && display.Contains("2025-02-15") &&
    display.Contains("Dates/abilities may be unsupported virtual Core defaults"),
    "Android-independent identity details or safety caveat missing");
Console.WriteLine("Synthetic PK1/PK9 PID, EC, shiny XOR, 16-bit provenance, raw dates, snapshot and schema v10 comparisons passed.");
Console.WriteLine("No desktop-matched save, Android device, legality, ROM-hack layout or write/export path was tested.");
