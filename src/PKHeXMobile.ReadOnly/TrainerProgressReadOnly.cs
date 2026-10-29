using System.Text.Json.Serialization;
using PKHeX.Core;

namespace PKHeXMobile.ReadOnly;

/// <summary>
/// Detached, raw Core scalar getters only. A zero or default value may mean
/// that a game does not implement the virtual getter. This does NOT establish
/// trainer-record, event-work, money-limit or save-layout parity.
/// </summary>
public sealed record TrainerProgressReadOnly(
    byte GenderCode, int LanguageCode, uint Money,
    int PlayedHours, int PlayedMinutes, int PlayedSeconds, int CurrentBox)
{
    public static TrainerProgressReadOnly Project(SaveFile save)
    {
        ArgumentNullException.ThrowIfNull(save);
        var progress = new TrainerProgressReadOnly(
            save.Gender, save.Language, save.Money,
            save.PlayedHours, save.PlayedMinutes, save.PlayedSeconds, save.CurrentBox);
        if (!TrainerProgressProjectionVerifier.IsPlausible(progress, save.BoxCount))
            throw new InvalidDataException("Core trainer progress contains out-of-range scalar values.");
        return progress;
    }
}

/// <summary>Schema v7: explicit values, including zero and -1 sentinels.</summary>
public sealed record ExpectedTrainerProgressReadOnly(
    [property: JsonRequired] byte GenderCode,
    [property: JsonRequired] int LanguageCode,
    [property: JsonRequired] uint Money,
    [property: JsonRequired] int PlayedHours,
    [property: JsonRequired] int PlayedMinutes,
    [property: JsonRequired] int PlayedSeconds,
    [property: JsonRequired] int CurrentBox);

/// <summary>Field-only errors; never print private trainer data or fixture values.</summary>
public static class TrainerProgressProjectionVerifier
{
    public static bool IsPlausible(TrainerProgressReadOnly value, int boxCount) =>
        value.LanguageCode is >= -1 and <= 255 &&
        value.PlayedHours is >= 0 and <= 1_000_000 &&
        value.PlayedMinutes is >= 0 and < 60 &&
        value.PlayedSeconds is >= 0 and < 60 &&
        boxCount is >= 0 and <= 64 &&
        value.CurrentBox >= -1 &&
        value.CurrentBox < Math.Max(1, boxCount);

    public static void Compare(TrainerProgressReadOnly? observed,
        ExpectedTrainerProgressReadOnly? expected, int boxCount, List<string> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (observed is null || expected is null)
        {
            errors.Add(observed is null
                ? "TrainerProgress missing from observed projection"
                : "TrainerProgress missing from expectation");
            return;
        }
        if (!IsPlausible(observed, boxCount))
            errors.Add("TrainerProgress observed scalar range invalid");
        var reference = new TrainerProgressReadOnly(expected.GenderCode,
            expected.LanguageCode, expected.Money, expected.PlayedHours,
            expected.PlayedMinutes, expected.PlayedSeconds, expected.CurrentBox);
        if (!IsPlausible(reference, boxCount))
            errors.Add("TrainerProgress expected scalar range invalid");
        if (observed.GenderCode != expected.GenderCode) errors.Add("TrainerProgress.GenderCode mismatch");
        if (observed.LanguageCode != expected.LanguageCode) errors.Add("TrainerProgress.LanguageCode mismatch");
        if (observed.Money != expected.Money) errors.Add("TrainerProgress.Money mismatch");
        if (observed.PlayedHours != expected.PlayedHours) errors.Add("TrainerProgress.PlayedHours mismatch");
        if (observed.PlayedMinutes != expected.PlayedMinutes) errors.Add("TrainerProgress.PlayedMinutes mismatch");
        if (observed.PlayedSeconds != expected.PlayedSeconds) errors.Add("TrainerProgress.PlayedSeconds mismatch");
        if (observed.CurrentBox != expected.CurrentBox) errors.Add("TrainerProgress.CurrentBox mismatch");
    }
}
