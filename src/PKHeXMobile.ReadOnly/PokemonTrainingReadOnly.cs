using System.Text.Json.Serialization;
using PKHeX.Core;

namespace PKHeXMobile.ReadOnly;

/// <summary>
/// Diagnostic interpretation of immutable Core IV/DV and EV/stat-experience
/// getters. The limits describe conventional game mechanics, NOT save-layout
/// authentication, legality, or permission to normalize unusual values.
/// </summary>
public sealed record PokemonTrainingReadOnly(
    int CoreFormat,
    long EffectiveEffortTotal,
    int ConventionalPerStatEffortCeiling,
    long ConventionalTotalEffortCeiling,
    int? DerivedHpDv,
    bool? CoreSpecialGettersAgree,
    bool ExceedsConventionalEffortLimits)
{
    public static PokemonTrainingReadOnly Project(PKM entity, PokemonStatSpread iv, PokemonStatSpread ev)
    {
        ArgumentNullException.ThrowIfNull(entity);
        int format = entity.Format;
        // Game Boy has five stat-experience buckets; the Special getter can
        // appear in both modern Special fields but must never be double-counted.
        bool classic = entity is PK1 or PK2;
        long total = (long)ev.HP + ev.Attack + ev.Defense + ev.SpecialAttack + ev.Speed +
            (classic ? 0L : ev.SpecialDefense);
        int perStat = classic ? 65535 : format is >= 3 and <= 5 ? 255 :
            format is >= 6 and <= 9 ? 252 : -1;
        long totalLimit = classic ? 327675 : format is >= 3 and <= 9 ? 510 : -1;
        bool outOfBounds = false;
        if (perStat >= 0)
        {
            var values = classic
                ? new[] { ev.HP, ev.Attack, ev.Defense, ev.SpecialAttack, ev.Speed }
                : new[] { ev.HP, ev.Attack, ev.Defense, ev.SpecialAttack, ev.SpecialDefense, ev.Speed };
            outOfBounds = values.Any(v => v < 0 || v > perStat) || total > totalLimit;
        }
        int? hpDv = classic
            ? ((iv.Attack & 1) << 3) | ((iv.Defense & 1) << 2) |
              ((iv.Speed & 1) << 1) | (iv.SpecialAttack & 1)
            : null;
        bool? specialAgree = classic
            ? iv.SpecialAttack == iv.SpecialDefense && ev.SpecialAttack == ev.SpecialDefense
            : null;
        return new PokemonTrainingReadOnly(format, total, perStat, totalLimit,
            hpDv, specialAgree, outOfBounds);
    }
}

/// <summary>Schema v8. All fields required, including explicit null classic-only values.</summary>
public sealed record ExpectedPokemonTrainingReadOnly(
    [property: JsonRequired] int CoreFormat,
    [property: JsonRequired] long EffectiveEffortTotal,
    [property: JsonRequired] int ConventionalPerStatEffortCeiling,
    [property: JsonRequired] long ConventionalTotalEffortCeiling,
    [property: JsonRequired] int? DerivedHpDv,
    [property: JsonRequired] bool? CoreSpecialGettersAgree,
    [property: JsonRequired] bool ExceedsConventionalEffortLimits);

public static class PokemonTrainingProjectionVerifier
{
    public static void Compare(PokemonTrainingReadOnly? observed,
        ExpectedPokemonTrainingReadOnly? expected, PokemonStatSpread iv,
        PokemonStatSpread ev, string path, List<string> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (observed is null || expected is null)
        {
            errors.Add(path + (observed is null
                ? " missing from observed projection" : " missing from expectation"));
            return;
        }
        // Validate both sides independently of field equality; malformed
        // matching manifests must never be accepted as desktop parity evidence.
        Validate(observed, iv, ev, path + " observed", errors);
        Validate(new PokemonTrainingReadOnly(expected.CoreFormat,
            expected.EffectiveEffortTotal, expected.ConventionalPerStatEffortCeiling,
            expected.ConventionalTotalEffortCeiling, expected.DerivedHpDv,
            expected.CoreSpecialGettersAgree, expected.ExceedsConventionalEffortLimits),
            iv, ev, path + " expected", errors);
        if (observed.CoreFormat != expected.CoreFormat) errors.Add(path + ".CoreFormat mismatch");
        if (observed.EffectiveEffortTotal != expected.EffectiveEffortTotal) errors.Add(path + ".EffectiveEffortTotal mismatch");
        if (observed.ConventionalPerStatEffortCeiling != expected.ConventionalPerStatEffortCeiling) errors.Add(path + ".ConventionalPerStatEffortCeiling mismatch");
        if (observed.ConventionalTotalEffortCeiling != expected.ConventionalTotalEffortCeiling) errors.Add(path + ".ConventionalTotalEffortCeiling mismatch");
        if (observed.DerivedHpDv != expected.DerivedHpDv) errors.Add(path + ".DerivedHpDv mismatch");
        if (observed.CoreSpecialGettersAgree != expected.CoreSpecialGettersAgree) errors.Add(path + ".CoreSpecialGettersAgree mismatch");
        if (observed.ExceedsConventionalEffortLimits != expected.ExceedsConventionalEffortLimits) errors.Add(path + ".ExceedsConventionalEffortLimits mismatch");
    }

    private static void Validate(PokemonTrainingReadOnly t, PokemonStatSpread iv,
        PokemonStatSpread ev, string path, List<string> errors)
    {
        if (t.CoreFormat is < 1 or > 9)
        {
            errors.Add(path + " unsupported CoreFormat");
            return;
        }
        bool classic = t.CoreFormat is 1 or 2;
        long sum = (long)ev.HP + ev.Attack + ev.Defense + ev.SpecialAttack + ev.Speed +
            (classic ? 0L : ev.SpecialDefense);
        int perStat = classic ? 65535 : t.CoreFormat <= 5 ? 255 : 252;
        long totalLimit = classic ? 327675 : 510;
        int? hpDv = classic ? ((iv.Attack & 1) << 3) | ((iv.Defense & 1) << 2) |
            ((iv.Speed & 1) << 1) | (iv.SpecialAttack & 1) : null;
        bool? special = classic
            ? iv.SpecialAttack == iv.SpecialDefense && ev.SpecialAttack == ev.SpecialDefense : null;
        var values = classic
            ? new[] { ev.HP, ev.Attack, ev.Defense, ev.SpecialAttack, ev.Speed }
            : new[] { ev.HP, ev.Attack, ev.Defense, ev.SpecialAttack, ev.SpecialDefense, ev.Speed };
        bool outside = values.Any(v => v < 0 || v > perStat) || sum > totalLimit;
        if (t.EffectiveEffortTotal != sum || t.ConventionalPerStatEffortCeiling != perStat ||
            t.ConventionalTotalEffortCeiling != totalLimit || t.DerivedHpDv != hpDv ||
            t.CoreSpecialGettersAgree != special || t.ExceedsConventionalEffortLimits != outside)
            errors.Add(path + " inconsistent derived training values");
    }
}
