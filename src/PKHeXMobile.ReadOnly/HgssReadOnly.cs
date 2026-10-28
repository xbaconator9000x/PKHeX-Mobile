using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using PKHeX.Core;

namespace PKHeXMobile.ReadOnly;

/// <summary>
/// HGSS-only, detached raw diagnostics from the pinned PKHeX.Core 26.10.18.
/// No mutating Core APIs, save serialization, language-dependent flag normalization,
/// or Safari API (which does not exist in this Core release) are used.
/// </summary>
public sealed record HgssGadgetsReadOnly(
    [property: JsonRequired] uint WalkerSteps,
    [property: JsonRequired] uint WalkerWatts,
    [property: JsonRequired] uint WalkerFlagsRaw,
    [property: JsonRequired] IReadOnlyList<byte> ApricornCounts,
    [property: JsonRequired] IReadOnlyList<byte> PokegearCallerIds,
    [property: JsonRequired] string PokegearSha256);

public sealed record HgssPokeathlonReadOnly(
    [property: JsonRequired] uint PointsRaw,
    [property: JsonRequired] uint DataCardFlagsRaw,
    [property: JsonRequired] ushort DailyShopFlagsRaw,
    [property: JsonRequired] uint ModeledMedalCount,
    [property: JsonRequired] int MedalEntriesWithReservedBits,
    [property: JsonRequired] string MedalSha256,
    [property: JsonRequired] IReadOnlyList<ushort> CourseBestScores,
    [property: JsonRequired] IReadOnlyList<ushort> EventBestScores,
    [property: JsonRequired] IReadOnlyList<uint> GlobalCountersRaw,
    [property: JsonRequired] string RawSha256);

public sealed record HgssReadOnly(
    [property: JsonRequired] HgssGadgetsReadOnly Gadgets,
    [property: JsonRequired] HgssPokeathlonReadOnly Pokeathlon)
{
    private const int WalkerOffset = 0xE704;
    private const int ApricornOffset = 0xE558;
    private const int GearOffset = 0xC0EC;
    public const int ApricornCount = 7;
    public const int MedalCount = 493;
    public const int CourseCount = 5;
    public const int EventCount = 10;
    public const int GlobalCounterCount = 0x74 / 4;

    public static HgssReadOnly? Project(SaveFile save)
    {
        ArgumentNullException.ThrowIfNull(save);
        if (save is not SAV4HGSS hgss)
            return null;
        // Core's public roster span supplies the actual pinned cardinality.
        // Read from a separate copy; never retain its live Span.
        int callerCount = hgss.GetPokeGearRoloDex().Length;
        return new HgssReadOnly(
            ProjectGadgets(hgss.General, callerCount),
            ProjectPokeathlon(hgss.Pokeathlon.Data));
    }

    public static HgssGadgetsReadOnly ProjectGadgets(ReadOnlySpan<byte> general, int callerCount)
    {
        if (general.Length != SAV4HGSS.GeneralSize || callerCount is < 1 or > 128 ||
            GearOffset + callerCount > general.Length)
            throw new InvalidDataException("Unreviewed HGSS general block or caller roster.");
        // Use raw 32-bit flags rather than Core's language-dependent UnlockAll
        // or MAUI's bool rebinding that clears reserved bits 27–31.
        var walker = general.Slice(WalkerOffset, 12).ToArray();
        var apricorns = general.Slice(ApricornOffset, ApricornCount).ToArray();
        var callers = general.Slice(GearOffset, callerCount).ToArray();
        return new HgssGadgetsReadOnly(
            BinaryPrimitives.ReadUInt32LittleEndian(walker),
            BinaryPrimitives.ReadUInt32LittleEndian(walker.AsSpan(4)),
            BinaryPrimitives.ReadUInt32LittleEndian(walker.AsSpan(8)),
            Array.AsReadOnly(apricorns),
            Array.AsReadOnly(callers),
            Convert.ToHexString(SHA256.HashData(callers)));
    }

    public static HgssPokeathlonReadOnly ProjectPokeathlon(ReadOnlySpan<byte> source)
    {
        if (source.Length != Pokeathlon4.SIZE)
            throw new InvalidDataException("Unreviewed HGSS Pokéathlon block length.");
        var raw = source.ToArray();
        var medals = raw.AsSpan(0xDC, MedalCount);
        uint modeledMedals = 0;
        int reserved = 0;
        foreach (byte value in medals)
        {
            modeledMedals += (uint)BitOperations.PopCount((uint)(value & 0x1F));
            if ((value & 0xE0) != 0) reserved++;
        }

        var courses = new ushort[CourseCount];
        for (int i = 0; i < courses.Length; i++)
            courses[i] = BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(i * 0x2C + 6, 2));
        var events = new ushort[EventCount];
        for (int i = 0; i < events.Length; i++)
            events[i] = BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(0xAEC + i * 2, 2));
        var counters = new uint[GlobalCounterCount];
        for (int i = 0; i < counters.Length; i++)
            counters[i] = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(0xB00 + i * 4, 4));

        return new HgssPokeathlonReadOnly(
            BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(0xB74, 4)),
            BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(0xB78, 4)),
            BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(0xB7C, 2)),
            modeledMedals, reserved,
            Convert.ToHexString(SHA256.HashData(medals)),
            Array.AsReadOnly(courses), Array.AsReadOnly(events),
            Array.AsReadOnly(counters), Convert.ToHexString(SHA256.HashData(raw)));
    }
}

/// <summary>Schema v14 independently supplied HGSS oracle; required JSON members.</summary>
public sealed record ExpectedHgssReadOnly(
    [property: JsonRequired] HgssGadgetsReadOnly Gadgets,
    [property: JsonRequired] HgssPokeathlonReadOnly Pokeathlon);

public static class HgssProjectionVerifier
{
    public static void Compare(HgssReadOnly? actual, ExpectedHgssReadOnly? expected, List<string> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (actual?.Gadgets is not { } a || expected?.Gadgets is not { } e ||
            actual.Pokeathlon is not { } p || expected.Pokeathlon is not { } q)
        {
            errors.Add("HGSS missing gadgets or Pokéathlon");
            return;
        }

        if (a.ApricornCounts is null || e.ApricornCounts is null ||
            a.ApricornCounts.Count != HgssReadOnly.ApricornCount ||
            e.ApricornCounts.Count != HgssReadOnly.ApricornCount ||
            a.PokegearCallerIds is null || e.PokegearCallerIds is null ||
            a.PokegearCallerIds.Count is < 1 or > 128 ||
            a.PokegearCallerIds.Count != e.PokegearCallerIds.Count ||
            !Digest(a.PokegearSha256) || !Digest(e.PokegearSha256))
        {
            errors.Add("HGSS.Gadgets invalid shape");
            return;
        }

        if (a.WalkerSteps != e.WalkerSteps || a.WalkerWatts != e.WalkerWatts ||
            a.WalkerFlagsRaw != e.WalkerFlagsRaw ||
            !a.ApricornCounts.SequenceEqual(e.ApricornCounts) ||
            !a.PokegearCallerIds.SequenceEqual(e.PokegearCallerIds) ||
            !string.Equals(a.PokegearSha256, e.PokegearSha256, StringComparison.OrdinalIgnoreCase))
            errors.Add("HGSS.Gadgets mismatch");

        if (p.CourseBestScores is null || q.CourseBestScores is null ||
            p.EventBestScores is null || q.EventBestScores is null ||
            p.GlobalCountersRaw is null || q.GlobalCountersRaw is null ||
            p.CourseBestScores.Count != HgssReadOnly.CourseCount ||
            q.CourseBestScores.Count != HgssReadOnly.CourseCount ||
            p.EventBestScores.Count != HgssReadOnly.EventCount ||
            q.EventBestScores.Count != HgssReadOnly.EventCount ||
            p.GlobalCountersRaw.Count != HgssReadOnly.GlobalCounterCount ||
            q.GlobalCountersRaw.Count != HgssReadOnly.GlobalCounterCount ||
            p.ModeledMedalCount > HgssReadOnly.MedalCount * 5u ||
            q.ModeledMedalCount > HgssReadOnly.MedalCount * 5u ||
            p.MedalEntriesWithReservedBits is < 0 or > HgssReadOnly.MedalCount ||
            q.MedalEntriesWithReservedBits is < 0 or > HgssReadOnly.MedalCount ||
            !Digest(p.MedalSha256) || !Digest(q.MedalSha256) ||
            !Digest(p.RawSha256) || !Digest(q.RawSha256))
        {
            errors.Add("HGSS.Pokeathlon invalid shape");
            return;
        }
        if (p.PointsRaw != q.PointsRaw || p.DataCardFlagsRaw != q.DataCardFlagsRaw ||
            p.DailyShopFlagsRaw != q.DailyShopFlagsRaw ||
            p.ModeledMedalCount != q.ModeledMedalCount ||
            p.MedalEntriesWithReservedBits != q.MedalEntriesWithReservedBits ||
            !string.Equals(p.MedalSha256, q.MedalSha256, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(p.RawSha256, q.RawSha256, StringComparison.OrdinalIgnoreCase) ||
            !p.CourseBestScores.SequenceEqual(q.CourseBestScores) ||
            !p.EventBestScores.SequenceEqual(q.EventBestScores) ||
            !p.GlobalCountersRaw.SequenceEqual(q.GlobalCountersRaw))
            errors.Add("HGSS.Pokeathlon mismatch");
    }

    private static bool Digest(string? hash) =>
        hash is { Length: 64 } && hash.All(Uri.IsHexDigit);
}
