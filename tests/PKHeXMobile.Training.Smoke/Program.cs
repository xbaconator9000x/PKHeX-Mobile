using System.Text.Json;
using PKHeX.Core;
using PKHeXMobile.Domain;
using PKHeXMobile.ReadOnly;

static void Must(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

static ExpectedPokemonStatSpread ExpectedSpread(PokemonStatSpread s) =>
    new(s.HP, s.Attack, s.Defense, s.SpecialAttack, s.SpecialDefense, s.Speed);
static ExpectedPokemonTrainingReadOnly ExpectedTraining(PokemonTrainingReadOnly t) =>
    new(t.CoreFormat, t.EffectiveEffortTotal, t.ConventionalPerStatEffortCeiling,
        t.ConventionalTotalEffortCeiling, t.DerivedHpDv,
        t.CoreSpecialGettersAgree, t.ExceedsConventionalEffortLimits);

var classic = new PK1 { Species = 25, IV_ATK = 15, IV_DEF = 14,
    IV_SPE = 13, IV_SPA = 12, EV_HP = 65535, EV_ATK = 300 };
var before = classic.Data.ToArray();
var classicSlot = ReadOnlySaveInspector.ProjectPokemon(0, classic);
var c = classicSlot.Details!.Training!;
Must(classic.Data.SequenceEqual(before), "Read-only projection changed PK1 bytes");
Must(c.CoreFormat == 1 && c.DerivedHpDv == 10, "Gen1 HP DV derived bit order incorrect");
Must(c.ConventionalPerStatEffortCeiling == 65535 &&
    c.ConventionalTotalEffortCeiling == 327675 && !c.ExceedsConventionalEffortLimits,
    "Gen1 stat experience incorrectly subjected to modern EV caps");
Must(c.EffectiveEffortTotal == (long)classic.EV_HP + classic.EV_ATK +
    classic.EV_DEF + classic.EV_SPA + classic.EV_SPE,
    "Classic Special stat experience was double-counted");
classic.EV_HP = 0;
Must(c.EffectiveEffortTotal >= 65535, "Detached projection changed with Core entity");

var modern = new PK9 { Species = 25, EV_HP = 252, EV_ATK = 252, EV_SPE = 6 };
var modernBefore = modern.Data.ToArray();
var modernSlot = ReadOnlySaveInspector.ProjectPokemon(0, modern);
var m = modernSlot.Details!.Training!;
Must(modern.Data.SequenceEqual(modernBefore), "Read-only projection changed PK9 bytes");
Must(m.CoreFormat == 9 && m.EffectiveEffortTotal == 510 &&
    m.ConventionalPerStatEffortCeiling == 252 && !m.ExceedsConventionalEffortLimits &&
    m.DerivedHpDv is null && m.CoreSpecialGettersAgree is null,
    "Gen9 effort bounds or classic-only sentinels incorrect");

var unusual = new PK9 { Species = 25, EV_HP = 253, EV_ATK = 252, EV_SPE = 6 };
var u = ReadOnlySaveInspector.ProjectPokemon(0, unusual).Details!.Training!;
Must(u.ExceedsConventionalEffortLimits && u.EffectiveEffortTotal == 511,
    "Unusual raw EV values must remain visible without normalization");

var actual = new SaveReadOnlyResult("SYNTHETIC", "SV", 9, "TEST", 1, 2,
    1, 0, 0, [modernSlot], [], new SaveFingerprint(1, "TEST"));
var d = modernSlot.Details!;
var expectedDetails = new ExpectedPokemonReadOnlyDetails(d.Nature, d.AbilityId,
    d.HeldItemId, d.Gender, d.Form,
    new ExpectedPokemonMoveIds(d.Moves.First, d.Moves.Second, d.Moves.Third, d.Moves.Fourth),
    ExpectedSpread(d.IndividualValues), ExpectedSpread(d.EffortValues))
    { Training = ExpectedTraining(m) };
var expectedSlot = new ExpectedPokemonSlot(0, modernSlot.Species, modernSlot.Nickname,
    modernSlot.Level, modernSlot.IsEgg, modernSlot.IsShiny) { Details = expectedDetails };
var expected = new ExpectedSaveProjection("SYNTHETIC", "SV", 9, "TEST", 1, 2,
    1, 0, 0, [expectedSlot], []);
Must(SaveProjectionVerifier.Compare(actual, expected, requireTraining: true).Count == 0,
    "Schema v8 synthetic positive comparison failed");

void Detect(ExpectedPokemonTrainingReadOnly changed, string field)
{
    var e = expected with { Party = [expectedSlot with {
        Details = expectedDetails with { Training = changed } }] };
    Must(SaveProjectionVerifier.Compare(actual, e, requireTraining: true)
        .Any(error => error.Contains(field, StringComparison.Ordinal)),
        "Schema v8 failed to reject " + field);
}
Detect(ExpectedTraining(m) with { CoreFormat = 8 }, ".CoreFormat mismatch");
Detect(ExpectedTraining(m) with { EffectiveEffortTotal = 509 }, ".EffectiveEffortTotal mismatch");
Detect(ExpectedTraining(m) with { ConventionalPerStatEffortCeiling = 255 },
    ".ConventionalPerStatEffortCeiling mismatch");
Detect(ExpectedTraining(m) with { ConventionalTotalEffortCeiling = 511 },
    ".ConventionalTotalEffortCeiling mismatch");
Detect(ExpectedTraining(m) with { DerivedHpDv = 0 }, ".DerivedHpDv mismatch");
Detect(ExpectedTraining(m) with { CoreSpecialGettersAgree = true },
    ".CoreSpecialGettersAgree mismatch");
Detect(ExpectedTraining(m) with { ExceedsConventionalEffortLimits = true },
    ".ExceedsConventionalEffortLimits mismatch");
var missing = expected with { Party = [expectedSlot with {
    Details = expectedDetails with { Training = null } }] };
Must(SaveProjectionVerifier.Compare(actual, missing, requireTraining: true)
    .Any(error => error.Contains("Training missing from expectation")),
    "Schema v8 accepted missing training projection");
try
{
    JsonSerializer.Deserialize<ExpectedPokemonTrainingReadOnly>(
        """{"coreFormat":9,"effectiveEffortTotal":510,"conventionalPerStatEffortCeiling":252,"conventionalTotalEffortCeiling":510,"derivedHpDv":null,"coreSpecialGettersAgree":null}""",
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    throw new Exception("Missing required training boolean accepted");
}
catch (JsonException) { }
var text = ReadOnlyProjectionPresenter.FormatPartySlotDetails(new BoxCell(0, modernSlot));
Must(text.Contains("effective effort total 510") &&
    text.Contains("diagnostic only"), "Android-independent training presentation missing");
var flaggedText = ReadOnlyProjectionPresenter.FormatPartySlotDetails(
    new BoxCell(0, ReadOnlySaveInspector.ProjectPokemon(0, unusual)));
Must(flaggedText.Contains("Unusual raw effort values"), "Unusual effort values were hidden");
Console.WriteLine("Synthetic Gen1/Gen9 training diagnostics, raw preservation, schema v8 positive/negative and presentation passed.");
Console.WriteLine("No independently matched desktop fixtures, legality results or save writes were tested.");
