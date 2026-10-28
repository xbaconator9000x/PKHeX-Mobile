using System.Text.Json;
using PKHeX.Core;
using PKHeXMobile.ReadOnly;

// PKForge topic grouping adapted to official pinned Core. Synthetic check codes
// test classification, severity aggregation, immutability and no false "legal" status.
var checks = new[]
{
    CheckResult.Get(Severity.Valid, CheckIdentifier.Encounter, LegalityCheckResultCode.Valid),
    CheckResult.Get(Severity.Fishy, CheckIdentifier.Ball, LegalityCheckResultCode.Valid),
    CheckResult.Get(Severity.Invalid, CheckIdentifier.Ball, LegalityCheckResultCode.Valid),
    CheckResult.Get(Severity.Invalid, CheckIdentifier.CurrentMove, LegalityCheckResultCode.Valid),
    CheckResult.Get(Severity.Invalid, CheckIdentifier.Misc, LegalityCheckResultCode.Valid),
};
var groups = PortedLegalityChecks.GroupResults(checks);
if (groups.Count != 9 || groups[0].Name != "Encounter" || groups[0].Verdict != "Valid" ||
    groups.Single(g => g.Name == "Ball").Verdict != "Invalid" ||
    groups.Single(g => g.Name == "Moves").Verdict != "Invalid" ||
    groups[^1].Name != "Other" || groups[^1].Verdict != "Invalid")
    throw new Exception("PKForge-adapted legality topic grouping is incorrect.");
if (groups is IList<LegalityTopic> mutable)
{
    try { mutable.Clear(); throw new Exception("Mutable legality topic collection."); }
    catch (NotSupportedException) { }
}
var reasons = groups.Single(g => g.Name == "Ball").Reasons;
if (reasons is IList<string> mutableReasons)
{
    try { mutableReasons.Clear(); throw new Exception("Mutable legality reasons."); }
    catch (NotSupportedException) { }
}

// A raw PK1 is not an independently certified save or a valid retail fixture.
// Check that the official-Core analysis on a detached clone never edits its source.
var game = new SAV1(LanguageID.English, GameVersion.RB);
var pk = new PK1 { Species = 25, Move1 = 33, Move2 = 45 };
var original = pk.Data.ToArray();
var diagnostic = PokemonLegalityReadOnly.Project(pk, game.Personal);
if (!pk.Data.SequenceEqual(original))
    throw new Exception("Read-only legality mutated the source Pokémon.");
if (!diagnostic.Available && diagnostic.Valid)
    throw new Exception("Unavailable legality was labelled valid.");
if (diagnostic.Available && diagnostic.Topics.Count < 8)
    throw new Exception("A parsed Core result lost the PKForge topic categories.");
var view = ReadOnlySaveInspector.ProjectPokemon(0, pk, game.Personal);
if (view.Details?.Legality is null || !pk.Data.SequenceEqual(original))
    throw new Exception("Party legality projection missing or mutating.");
var withoutContext = ReadOnlySaveInspector.ProjectPokemon(0, pk);
if (withoutContext.Details?.Legality is not null)
    throw new Exception("An unauthenticated standalone Pokémon gained a legality verdict.");
var ui = ReadOnlyProjectionPresenter.FormatPartySlotDetails(new BoxCell(0, view));
if (!ui.Contains("Legality", StringComparison.Ordinal))
    throw new Exception("Android-independent slot detail lacks legality diagnostics.");
var json = JsonSerializer.Serialize(view.Details);
if (json.Contains("\"Legality\"", StringComparison.Ordinal))
    throw new Exception("Provisional legality was added to desktop parity schema without independent fixtures.");
if (PokemonLegalityReadOnly.Project(new PK1(), game.Personal).Available)
    throw new Exception("Empty species must not receive a legality verdict.");
Console.WriteLine("PKForge legality grouping adapted: synthetic category/severity/immutability gates and pinned-Core detached PK1 diagnostics passed.");
Console.WriteLine("Party-only provisional legality; no independent desktop fixture, box sweep, hack legality or save-write permission.");
