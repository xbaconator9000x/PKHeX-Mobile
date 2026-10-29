using System.Text.Json;
using PKHeX.Core;
using PKHeXMobile.Domain;
using PKHeXMobile.ReadOnly;

static void Must(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

// Synthetic pinned-Core Gen 1 save only; not a desktop-matched fixture.
var save = new SAV1(LanguageID.English, GameVersion.RB)
{
    Money = 12345,
    PlayedHours = 12,
    PlayedMinutes = 34,
    PlayedSeconds = 56,
};
var bytesBefore = save.Data.ToArray();
var progress = TrainerProgressReadOnly.Project(save);
Must(progress.Money == 12345 && progress.PlayedHours == 12 &&
    progress.PlayedMinutes == 34 && progress.PlayedSeconds == 56,
    "Core trainer scalar getters lost data");
Must(save.Data.SequenceEqual(bytesBefore), "Projection modified source bytes");
save.Money = 67890;
Must(progress.Money == 12345, "Projection retained mutable Core trainer state");

var actual = new SaveReadOnlyResult("SYNTHETIC", "RB", 1, "TEST", 1, 2,
    0, 0, 0, [], [], new SaveFingerprint(1, "TEST"))
    { TrainerProgress = progress };
var expectedProgress = new ExpectedTrainerProgressReadOnly(
    progress.GenderCode, progress.LanguageCode, progress.Money,
    progress.PlayedHours, progress.PlayedMinutes, progress.PlayedSeconds,
    progress.CurrentBox);
var expected = new ExpectedSaveProjection("SYNTHETIC", "RB", 1, "TEST",
    1, 2, 0, 0, 0, [], []) { TrainerProgress = expectedProgress };
Must(SaveProjectionVerifier.Compare(actual, expected,
    requireTrainerProgress: true).Count == 0, "Schema v7 positive mismatch");

void MustDetect(ExpectedTrainerProgressReadOnly changed, string field)
{
    Must(SaveProjectionVerifier.Compare(actual,
        expected with { TrainerProgress = changed },
        requireTrainerProgress: true).Any(error => error.Contains(field,
            StringComparison.Ordinal)), "Schema v7 missed " + field);
}
MustDetect(expectedProgress with { GenderCode = (byte)(progress.GenderCode + 1) },
    ".GenderCode mismatch");
MustDetect(expectedProgress with { LanguageCode = progress.LanguageCode + 1 },
    ".LanguageCode mismatch");
MustDetect(expectedProgress with { Money = progress.Money + 1 }, ".Money mismatch");
MustDetect(expectedProgress with { PlayedHours = progress.PlayedHours + 1 },
    ".PlayedHours mismatch");
MustDetect(expectedProgress with { PlayedMinutes = progress.PlayedMinutes + 1 },
    ".PlayedMinutes mismatch");
MustDetect(expectedProgress with { PlayedSeconds = progress.PlayedSeconds + 1 },
    ".PlayedSeconds mismatch");
MustDetect(expectedProgress with { CurrentBox = progress.CurrentBox == 0 ? -1 : 0 },
    ".CurrentBox mismatch");
MustDetect(expectedProgress with { PlayedMinutes = 60 }, "expected scalar range invalid");
MustDetect(expectedProgress with { PlayedSeconds = -1 }, "expected scalar range invalid");
MustDetect(expectedProgress with { LanguageCode = 999 }, "expected scalar range invalid");
MustDetect(expectedProgress with { CurrentBox = 64 }, "expected scalar range invalid");
Must(SaveProjectionVerifier.Compare(actual, expected with { TrainerProgress = null },
    requireTrainerProgress: true).Contains("TrainerProgress missing from expectation"),
    "Schema v7 accepted absent expected progress");
Must(SaveProjectionVerifier.Compare(actual with { TrainerProgress = null }, expected,
    requireTrainerProgress: true).Contains("TrainerProgress missing from observed projection"),
    "Schema v7 accepted absent observed progress");
try
{
    JsonSerializer.Deserialize<ExpectedTrainerProgressReadOnly>(
        """{"genderCode":0,"languageCode":1,"money":0,"playedHours":0,"playedMinutes":0,"playedSeconds":0}""",
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    throw new Exception("Missing required currentBox accepted");
}
catch (JsonException) { }
var summary = ReadOnlyProjectionPresenter.FormatTrainer(actual, false);
Must(summary.Contains("raw Core getters; not record/event parity") &&
    summary.Contains("12:34:56") && summary.Contains("money 12345"),
    "Trainer summary omitted bounded raw Core values");
Console.WriteLine("Synthetic Core trainer scalar projection, detached state, schema v7 negatives and read-only presentation passed.");
Console.WriteLine("No desktop-matched trainer records, physical Android provider or save export was tested.");
