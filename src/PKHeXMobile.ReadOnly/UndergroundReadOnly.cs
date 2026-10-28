using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using PKHeX.Core;

namespace PKHeXMobile.ReadOnly;

/// <summary>
/// Detached raw Underground diagnostics; never authorizes save edits.
/// PKHeX.Core 26.10.18 exposes Gen4 pouches as writable spans and BDSP
/// Underground as a 999-entry table. Neither setter nor writer is called.
/// </summary>
public sealed record UndergroundScore(string Name, uint Value);
public sealed record UndergroundItem(int Index, byte Id, byte? Size);
public sealed record UndergroundPouch(string Name, int Length, string Sha256, IReadOnlyList<UndergroundItem> Items);
public sealed record UndergroundGen4(IReadOnlyList<UndergroundScore> Scores, IReadOnlyList<UndergroundPouch> Pouches);
public sealed record UndergroundBdspItem(int Index, int Count, uint HideNewRaw, uint FavoriteRaw);
public sealed record UndergroundBdsp(int Length, string Sha256, IReadOnlyList<UndergroundBdspItem> Items);
public sealed record UndergroundReadOnly(UndergroundGen4? Gen4, UndergroundBdsp? Bdsp)
{
    public const int Gen4Slots = 40;
    public const int BdspSlots = 999;
    public const int BdspRecordSize = 12;

    public static UndergroundReadOnly? Project(SaveFile save)
    {
        ArgumentNullException.ThrowIfNull(save);
        return save switch
        {
            SAV4Sinnoh sinnoh => new UndergroundReadOnly(ProjectGen4(sinnoh), null),
            SAV8BS bdsp => new UndergroundReadOnly(null, ProjectBdsp(bdsp.Underground.Data)),
            _ => null,
        };
    }

    public static UndergroundGen4 ProjectGen4(SAV4Sinnoh save)
    {
        ArgumentNullException.ThrowIfNull(save);
        // Do not clamp to Core's UG_MAX UI convention: raw fields are uint.
        UndergroundScore[] scores =
        [
            new("PeopleMet", save.UG_PeopleMet),
            new("GiftsGiven", save.UG_GiftsGiven),
            new("FlagsTaken", save.UG_FlagsTaken),
            new("Spheres", save.UG_Spheres),
            new("Fossils", save.UG_Fossils),
            new("TrapPlayers", save.UG_TrapPlayers),
            new("TrapSelf", save.UG_TrapSelf),
            new("HelpedOthers", save.UG_HelpedOthers),
            new("GiftsReceived", save.UG_GiftsReceived),
            new("FlagsFromMe", save.UG_FlagsFromMe),
            new("FlagsRecovered", save.UG_FlagsRecovered),
            new("MyBaseMoved", save.UG_MyBaseMoved),
            new("FlagsCaptured", save.UG_FlagsCaptured),
        ];
        UndergroundPouch[] pouches =
        [
            ProjectPouch("Traps", save.GetUGI_Traps(), false),
            ProjectPouch("Goods", save.GetUGI_Goods(), false),
            ProjectPouch("Treasures", save.GetUGI_Treasures(), false),
            ProjectPouch("Spheres", save.GetUGI_Spheres(), true),
        ];
        return new UndergroundGen4(Array.AsReadOnly(scores), Array.AsReadOnly(pouches));
    }

    public static UndergroundPouch ProjectPouch(string name, ReadOnlySpan<byte> source, bool hasSizes)
    {
        if (name is not ("Traps" or "Goods" or "Treasures" or "Spheres") ||
            (name == "Spheres") != hasSizes)
            throw new InvalidDataException("Unreviewed Underground pouch type.");
        int expected = hasSizes ? 80 : 40;
        if (source.Length != expected)
            throw new InvalidDataException("Unreviewed Underground pouch size.");
        var bytes = source.ToArray();
        var items = new UndergroundItem[Gen4Slots];
        for (int i = 0; i < items.Length; i++)
            items[i] = new UndergroundItem(i, bytes[i], hasSizes ? bytes[40 + i] : null);
        return new UndergroundPouch(name, expected,
            Convert.ToHexString(SHA256.HashData(bytes)), Array.AsReadOnly(items));
    }

    /// <summary>
    /// Preserve raw 32-bit flags, including 2 and uint.MaxValue. Core's
    /// ReadItems maps flags to bool; WriteItems serializes all 999 rows and
    /// can normalize untouched flags. This path uses neither method.
    /// </summary>
    public static UndergroundBdsp ProjectBdsp(ReadOnlySpan<byte> source)
    {
        if (source.Length != BdspSlots * BdspRecordSize)
            throw new InvalidDataException("Unreviewed BDSP Underground table length.");
        var bytes = source.ToArray();
        var items = new UndergroundBdspItem[BdspSlots];
        for (int i = 0; i < items.Length; i++)
        {
            var span = bytes.AsSpan(i * BdspRecordSize, BdspRecordSize);
            items[i] = new UndergroundBdspItem(i,
                BinaryPrimitives.ReadInt32LittleEndian(span),
                BinaryPrimitives.ReadUInt32LittleEndian(span[4..]),
                BinaryPrimitives.ReadUInt32LittleEndian(span[8..]));
        }
        return new UndergroundBdsp(bytes.Length,
            Convert.ToHexString(SHA256.HashData(bytes)), Array.AsReadOnly(items));
    }
}

/// <summary>Schema v13 strict expected Underground fields.</summary>
public sealed record ExpectedUndergroundScore(
    [property: JsonRequired] string Name, [property: JsonRequired] uint Value);
public sealed record ExpectedUndergroundItem(
    [property: JsonRequired] int Index, [property: JsonRequired] byte Id,
    [property: JsonRequired] byte? Size);
public sealed record ExpectedUndergroundPouch(
    [property: JsonRequired] string Name, [property: JsonRequired] int Length,
    [property: JsonRequired] string Sha256,
    [property: JsonRequired] IReadOnlyList<ExpectedUndergroundItem> Items);
public sealed record ExpectedUndergroundGen4(
    [property: JsonRequired] IReadOnlyList<ExpectedUndergroundScore> Scores,
    [property: JsonRequired] IReadOnlyList<ExpectedUndergroundPouch> Pouches);
public sealed record ExpectedUndergroundBdspItem(
    [property: JsonRequired] int Index, [property: JsonRequired] int Count,
    [property: JsonRequired] uint HideNewRaw, [property: JsonRequired] uint FavoriteRaw);
public sealed record ExpectedUndergroundBdsp(
    [property: JsonRequired] int Length, [property: JsonRequired] string Sha256,
    [property: JsonRequired] IReadOnlyList<ExpectedUndergroundBdspItem> Items);
public sealed record ExpectedUndergroundReadOnly(
    [property: JsonRequired] ExpectedUndergroundGen4? Gen4,
    [property: JsonRequired] ExpectedUndergroundBdsp? Bdsp);

public static class UndergroundProjectionVerifier
{
    private static readonly string[] Names = ["Traps", "Goods", "Treasures", "Spheres"];
    private static readonly string[] ScoreNames =
    [
        "PeopleMet", "GiftsGiven", "FlagsTaken", "Spheres", "Fossils",
        "TrapPlayers", "TrapSelf", "HelpedOthers", "GiftsReceived",
        "FlagsFromMe", "FlagsRecovered", "MyBaseMoved", "FlagsCaptured",
    ];

    public static void Compare(UndergroundReadOnly? a, ExpectedUndergroundReadOnly? e, List<string> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (a is null || e is null ||
            (a.Gen4 is null) == (a.Bdsp is null) ||
            (e.Gen4 is null) == (e.Bdsp is null) ||
            (a.Gen4 is null) != (e.Gen4 is null))
        {
            errors.Add("Underground missing or conflicting format");
            return;
        }
        if (a.Gen4 is { } g && e.Gen4 is { } eg)
        {
            if (g.Scores is null || eg.Scores is null || g.Pouches is null || eg.Pouches is null ||
                g.Scores.Count != 13 || eg.Scores.Count != 13 ||
                g.Pouches.Count != 4 || eg.Pouches.Count != 4)
            {
                errors.Add("Underground.Gen4 invalid score/pouch shape");
                return;
            }
            for (int i = 0; i < 13; i++)
            {
                if (g.Scores[i] is null || eg.Scores[i] is null ||
                    g.Scores[i].Name != ScoreNames[i] || eg.Scores[i].Name != ScoreNames[i])
                    errors.Add($"Underground.Gen4.Scores[{i}] invalid name");
                else if (g.Scores[i].Value != eg.Scores[i].Value)
                    errors.Add($"Underground.Gen4.Scores[{i}].Value mismatch");
            }
            for (int i = 0; i < 4; i++)
            {
                var p = g.Pouches[i];
                var ep = eg.Pouches[i];
                int size = i == 3 ? 80 : 40;
                if (p is null || ep is null || p.Name != Names[i] || ep.Name != Names[i] ||
                    p.Length != size || ep.Length != size ||
                    !Digest(p.Sha256) || !Digest(ep.Sha256) ||
                    p.Items is null || ep.Items is null ||
                    p.Items.Count != 40 || ep.Items.Count != 40)
                {
                    errors.Add($"Underground.Gen4.Pouches[{i}] invalid shape");
                    continue;
                }
                if (!string.Equals(p.Sha256, ep.Sha256, StringComparison.OrdinalIgnoreCase))
                    errors.Add($"Underground.Gen4.Pouches[{i}].Sha256 mismatch");
                for (int j = 0; j < 40; j++)
                {
                    var item = p.Items[j];
                    var expected = ep.Items[j];
                    if (item is null || expected is null || item.Index != j || expected.Index != j ||
                        item.Size.HasValue != (i == 3) || expected.Size.HasValue != (i == 3))
                        errors.Add($"Underground.Gen4.Pouches[{i}].Items[{j}] invalid");
                    else if (item.Id != expected.Id || item.Size != expected.Size)
                        errors.Add($"Underground.Gen4.Pouches[{i}].Items[{j}] mismatch");
                }
            }
            return;
        }
        if (a.Bdsp is not { } b || e.Bdsp is not { } eb ||
            b.Length != 999 * 12 || eb.Length != 999 * 12 ||
            !Digest(b.Sha256) || !Digest(eb.Sha256) ||
            b.Items is null || eb.Items is null ||
            b.Items.Count != 999 || eb.Items.Count != 999)
        {
            errors.Add("Underground.Bdsp invalid shape");
            return;
        }
        if (!string.Equals(b.Sha256, eb.Sha256, StringComparison.OrdinalIgnoreCase))
            errors.Add("Underground.Bdsp.Sha256 mismatch");
        for (int i = 0; i < 999; i++)
        {
            var item = b.Items[i];
            var expected = eb.Items[i];
            if (item is null || expected is null || item.Index != i || expected.Index != i)
                errors.Add($"Underground.Bdsp.Items[{i}] invalid");
            else if (item.Count != expected.Count ||
                item.HideNewRaw != expected.HideNewRaw || item.FavoriteRaw != expected.FavoriteRaw)
                errors.Add($"Underground.Bdsp.Items[{i}] mismatch");
        }
    }

    private static bool Digest(string? hash) =>
        hash is { Length: 64 } && hash.All(Uri.IsHexDigit);
}
