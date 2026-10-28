using System.Globalization;
using System.Text.Json.Serialization;
using PKHeX.Core;

namespace PKHeXMobile.ReadOnly;

/// <summary>
/// Raw pinned-Core date components plus Core's nullable parsed date.
/// Null is not evidence of an absent field: Core may expose virtual defaults,
/// an unsupported format, or invalid raw calendar bytes.
/// </summary>
public sealed record PokemonDateReadOnly(
    [property: JsonRequired] byte Year,
    [property: JsonRequired] byte Month,
    [property: JsonRequired] byte Day,
    [property: JsonRequired] string? IsoDate)
{
    public static PokemonDateReadOnly Project(byte year, byte month, byte day, DateOnly? parsed) =>
        new(year, month, day, parsed?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
}

/// <summary>
/// Immutable identity and egg provenance diagnostics. Numeric IDs are raw Core
/// getters, not legality or save-layout certification. Do not call PID, egg,
/// shiny, form, or ability mutators when creating this snapshot.
/// Every field is mandatory in schema v10 manifests, including nullable dates.
/// </summary>
public sealed record PokemonIdentityReadOnly(
    [property: JsonRequired] int CoreFormat,
    [property: JsonRequired] int OriginGeneration,
    [property: JsonRequired] uint PersonalityId,
    [property: JsonRequired] uint EncryptionConstant,
    [property: JsonRequired] uint TrainerId32,
    [property: JsonRequired] ushort ShinyXor,
    [property: JsonRequired] int AbilityNumber,
    [property: JsonRequired] int PidAbility,
    [property: JsonRequired] ushort MetLocation,
    [property: JsonRequired] ushort EggLocation,
    [property: JsonRequired] byte Ball,
    [property: JsonRequired] byte MetLevel,
    [property: JsonRequired] byte OriginalTrainerFriendship,
    [property: JsonRequired] bool IsEgg,
    [property: JsonRequired] bool WasEgg,
    [property: JsonRequired] bool IsShiny,
    [property: JsonRequired] PokemonDateReadOnly MetDate,
    [property: JsonRequired] PokemonDateReadOnly EggDate)
{
    public static PokemonIdentityReadOnly Project(PKM pokemon)
    {
        ArgumentNullException.ThrowIfNull(pokemon);
        return new PokemonIdentityReadOnly(
            pokemon.Format, pokemon.Generation, pokemon.PID,
            pokemon.EncryptionConstant, pokemon.ID32, pokemon.ShinyXor,
            pokemon.AbilityNumber, pokemon.PIDAbility, pokemon.MetLocation,
            pokemon.EggLocation, pokemon.Ball, pokemon.MetLevel,
            pokemon.OriginalTrainerFriendship, pokemon.IsEgg,
            pokemon.WasEgg, pokemon.IsShiny,
            PokemonDateReadOnly.Project(pokemon.MetYear, pokemon.MetMonth,
                pokemon.MetDay, pokemon.MetDate),
            PokemonDateReadOnly.Project(pokemon.EggYear, pokemon.EggMonth,
                pokemon.EggDay, pokemon.EggMetDate));
    }
}

/// <summary>
/// Compares schema v10 observations to separately authored desktop expectations.
/// Both sides are validated, including equal-but-malformed inputs. A match is
/// never evidence of independent fixture provenance or permission to write.
/// </summary>
public static class PokemonIdentityProjectionVerifier
{
    public static void Compare(PokemonIdentityReadOnly? actual,
        PokemonIdentityReadOnly? expected, string path, List<string> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (actual is null || expected is null)
        {
            errors.Add(path + (actual is null
                ? " missing from observed projection" : " missing from expectation"));
            return;
        }
        Validate(actual, path + " observed", errors);
        Validate(expected, path + " expected", errors);
        Check(actual.CoreFormat == expected.CoreFormat, ".CoreFormat");
        Check(actual.OriginGeneration == expected.OriginGeneration, ".OriginGeneration");
        Check(actual.PersonalityId == expected.PersonalityId, ".PersonalityId");
        Check(actual.EncryptionConstant == expected.EncryptionConstant, ".EncryptionConstant");
        Check(actual.TrainerId32 == expected.TrainerId32, ".TrainerId32");
        Check(actual.ShinyXor == expected.ShinyXor, ".ShinyXor");
        Check(actual.AbilityNumber == expected.AbilityNumber, ".AbilityNumber");
        Check(actual.PidAbility == expected.PidAbility, ".PidAbility");
        Check(actual.MetLocation == expected.MetLocation, ".MetLocation");
        Check(actual.EggLocation == expected.EggLocation, ".EggLocation");
        Check(actual.Ball == expected.Ball, ".Ball");
        Check(actual.MetLevel == expected.MetLevel, ".MetLevel");
        Check(actual.OriginalTrainerFriendship == expected.OriginalTrainerFriendship,
            ".OriginalTrainerFriendship");
        Check(actual.IsEgg == expected.IsEgg, ".IsEgg");
        Check(actual.WasEgg == expected.WasEgg, ".WasEgg");
        Check(actual.IsShiny == expected.IsShiny, ".IsShiny");
        CompareDate(actual.MetDate, expected.MetDate, ".MetDate");
        CompareDate(actual.EggDate, expected.EggDate, ".EggDate");

        void Check(bool condition, string field)
        {
            if (!condition) errors.Add(path + field + " mismatch");
        }
        void CompareDate(PokemonDateReadOnly? a, PokemonDateReadOnly? e, string field)
        {
            if (a is null || e is null) return; // Both nulls already rejected by Validate.
            Check(a.Year == e.Year, field + ".Year");
            Check(a.Month == e.Month, field + ".Month");
            Check(a.Day == e.Day, field + ".Day");
            Check(a.IsoDate == e.IsoDate, field + ".IsoDate");
        }
    }

    private static void Validate(PokemonIdentityReadOnly value, string path,
        List<string> errors)
    {
        if (value.CoreFormat is < 1 or > 9)
            errors.Add(path + " unsupported CoreFormat");
        if (value.OriginGeneration is < 0 or > 9)
            errors.Add(path + " invalid OriginGeneration");
        if (value.PidAbility is < -1 or > 1)
            errors.Add(path + " invalid PidAbility");
        uint xor = value.TrainerId32 ^ value.PersonalityId;
        if (value.ShinyXor != (ushort)(xor ^ (xor >> 16)))
            errors.Add(path + " inconsistent ShinyXor");
        if (value.IsEgg && !value.WasEgg)
            errors.Add(path + " inconsistent egg history");
        ValidateDate(value.MetDate, path + ".MetDate", errors);
        ValidateDate(value.EggDate, path + ".EggDate", errors);
    }

    private static void ValidateDate(PokemonDateReadOnly? date, string path,
        List<string> errors)
    {
        if (date is null)
        {
            errors.Add(path + " missing raw date components");
            return;
        }
        // Do not clamp unusual raw date bytes. Null is legitimate for invalid
        // or unsupported dates. A non-null ISO date must match all raw fields.
        if (date.IsoDate is null) return;
        if (!DateOnly.TryParseExact(date.IsoDate, "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) ||
            parsed.Year != 2000 + date.Year ||
            parsed.Month != date.Month || parsed.Day != date.Day)
            errors.Add(path + " inconsistent ISO date");
    }
}
