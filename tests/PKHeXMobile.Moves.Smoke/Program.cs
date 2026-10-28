using System.Text.Json;
using PKHeX.Core;
using PKHeXMobile.Domain;
using PKHeXMobile.ReadOnly;

static void Must(bool ok, string message)
{
    if (!ok) throw new Exception(message);
}
static ExpectedPokemonMoveIds ExpectedIds(PokemonMoveIds m) =>
    new(m.First, m.Second, m.Third, m.Fourth);
static ExpectedPokemonMoveStateReadOnly ExpectedState(PokemonMoveStateReadOnly m) =>
    new(m.CoreFormat, m.Slots.Select(s =>
        new ExpectedMoveSlotReadOnly(s.SlotIndex, s.MoveId, s.CurrentPp, s.PpUps)).ToArray(),
        ExpectedIds(m.RelearnMoveIds));

// Synthetic entities only: the expected baseline below is NOT independent desktop evidence.
var classic = new PK1 { Species = 25, Move1 = 33, Move1_PP = 1,
    Move1_PPUps = 2, Move2 = 45, Move2_PP = 0 };
var classicBytes = classic.Data.ToArray();
var classicState = ReadOnlySaveInspector.ProjectPokemon(0, classic).Details!.MoveState!;
Must(classic.Data.SequenceEqual(classicBytes), "PK1 move inspection mutated bytes");
Must(classicState.Slots.Count == 4 && classicState.Slots[0].CurrentPp == classic.Move1_PP &&
    classicState.Slots[0].PpUps == classic.Move1_PPUps &&
    classicState.Slots[1].CurrentPp == 0 && classicState.CoreFormat == 1,
    "Gen1 stored PP or PP Ups was normalized");
Must(classicState.RelearnMoveIds == new PokemonMoveIds(0, 0, 0, 0),
    "Gen1 unsupported relearn getters must not be invented");

var modern = new PK9 { Species = 25, Move1 = 85, Move1_PP = 0,
    Move1_PPUps = 3, Move2 = 98, Move2_PP = 2, RelearnMove1 = 85,
    RelearnMove2 = 98 };
var before = modern.Data.ToArray();
var pokemon = ReadOnlySaveInspector.ProjectPokemon(0, modern);
var state = pokemon.Details!.MoveState!;
Must(modern.Data.SequenceEqual(before), "PK9 move inspection mutated bytes");
Must(state.Slots.Count == 4 && state.Slots[0].MoveId == 85 &&
    state.Slots[0].CurrentPp == 0 && state.Slots[0].PpUps == 3 &&
    state.RelearnMoveIds.First == 85 && state.RelearnMoveIds.Second == 98,
    "Depleted PP was healed or relearn getters were lost");
Must(state.Slots is IList<MoveSlotReadOnly>, "Projection should expose a read-only list");
try
{
    ((IList<MoveSlotReadOnly>)state.Slots).Add(new MoveSlotReadOnly(4, 1, 1, 1));
    throw new Exception("Move snapshot list was mutable");
}
catch (NotSupportedException) { }
modern.Move1_PP = 5;
modern.RelearnMove1 = 1;
Must(state.Slots[0].CurrentPp == 0 && state.RelearnMoveIds.First == 85,
    "Core entity changes leaked into detached move snapshot");

var d = pokemon.Details!;
var expectedState = ExpectedState(state);
var expectedDetails = new ExpectedPokemonReadOnlyDetails(d.Nature, d.AbilityId,
    d.HeldItemId, d.Gender, d.Form,
    ExpectedIds(d.Moves),
    new ExpectedPokemonStatSpread(d.IndividualValues.HP, d.IndividualValues.Attack,
        d.IndividualValues.Defense, d.IndividualValues.SpecialAttack,
        d.IndividualValues.SpecialDefense, d.IndividualValues.Speed),
    new ExpectedPokemonStatSpread(d.EffortValues.HP, d.EffortValues.Attack,
        d.EffortValues.Defense, d.EffortValues.SpecialAttack,
        d.EffortValues.SpecialDefense, d.EffortValues.Speed))
    { MoveState = expectedState };
var expectedPokemon = new ExpectedPokemonSlot(0, pokemon.Species, pokemon.Nickname,
    pokemon.Level, pokemon.IsEgg, pokemon.IsShiny) { Details = expectedDetails };
var actual = new SaveReadOnlyResult("SYNTHETIC", "SV", 9, "TEST", 1, 2, 1, 0, 0,
    [pokemon], [], new SaveFingerprint(1, "TEST"));
var baseline = new ExpectedSaveProjection("SYNTHETIC", "SV", 9, "TEST", 1, 2, 1, 0, 0,
    [expectedPokemon], []);
Must(SaveProjectionVerifier.Compare(actual, baseline, requireMoveState: true).Count == 0,
    "Schema v9 matching synthetic move state rejected");

void Detect(ExpectedPokemonMoveStateReadOnly altered, string field)
{
    var reference = baseline with { Party = [expectedPokemon with {
        Details = expectedDetails with { MoveState = altered } }] };
    var errors = SaveProjectionVerifier.Compare(actual, reference, requireMoveState: true);
    Must(errors.Any(s => s.Contains(field, StringComparison.Ordinal)),
        "Schema v9 failed to reject " + field);
}
ExpectedPokemonMoveStateReadOnly ChangeSlot(int index, Func<ExpectedMoveSlotReadOnly,
    ExpectedMoveSlotReadOnly> change) =>
    expectedState with { Slots = expectedState.Slots.Select((s, i) =>
        i == index ? change(s) : s).ToArray() };
Detect(expectedState with { CoreFormat = 8 }, ".CoreFormat mismatch");
Detect(ChangeSlot(0, s => s with { MoveId = 86 }), ".Slots[0].MoveId mismatch");
Detect(ChangeSlot(0, s => s with { CurrentPp = 1 }), ".Slots[0].CurrentPp mismatch");
Detect(ChangeSlot(0, s => s with { PpUps = 2 }), ".Slots[0].PpUps mismatch");
Detect(ChangeSlot(3, s => s with { SlotIndex = 2 }), "slot 3 out of order");
Detect(ChangeSlot(0, s => s with { CurrentPp = -1 }), "PP outside raw bound");
Detect(ChangeSlot(0, s => s with { PpUps = 4 }), "PP Ups outside raw bound");
Detect(expectedState with { RelearnMoveIds = expectedState.RelearnMoveIds with { First = 1 } },
    ".RelearnMoveIds.First mismatch");
Detect(expectedState with { RelearnMoveIds = null! }, "missing relearn move IDs");
Detect(expectedState with { Slots = [] }, "requires exactly four physical slots");
Detect(expectedState with { Slots = [null!, .. expectedState.Slots.Skip(1)] }, "null slot 0");

var missing = baseline with { Party = [expectedPokemon with {
    Details = expectedDetails with { MoveState = null } }] };
Must(SaveProjectionVerifier.Compare(actual, missing, requireMoveState: true)
    .Any(s => s.Contains("MoveState missing from expectation")),
    "Schema v9 accepted missing move state");
Must(SaveProjectionVerifier.Compare(actual with { Party = [pokemon with {
    Details = d with { MoveState = null } }] }, baseline, requireMoveState: true)
    .Any(s => s.Contains("MoveState missing from observed projection")),
    "Schema v9 accepted missing observed move state");
try
{
    JsonSerializer.Deserialize<ExpectedPokemonMoveStateReadOnly>(
        """{"coreFormat":9,"slots":[] }""",
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    throw new Exception("Missing required relearn move IDs accepted");
}
catch (JsonException) { }
try
{
    JsonSerializer.Deserialize<ExpectedMoveSlotReadOnly>(
        """{"slotIndex":0,"moveId":85,"currentPp":0}""",
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    throw new Exception("Missing required PP Ups accepted");
}
catch (JsonException) { }
var display = ReadOnlyProjectionPresenter.FormatPartySlotDetails(new BoxCell(0, pokemon));
Must(display.Contains("Stored PP (Core, never healed)") &&
    display.Contains("ID 85, PP 0, PP Ups 3") &&
    display.Contains("Relearn move IDs (Core getters; support varies)"),
    "Read-only Android move presentation missing raw PP or relearn caveat");
Console.WriteLine("Synthetic PK1/PK9 raw PP, PP Ups, relearn getters, immutable snapshots, schema v9 positive/negative and presentation passed.");
Console.WriteLine("No independently matched desktop fixture, actual save, device test, legality or write/export operation was tested.");
