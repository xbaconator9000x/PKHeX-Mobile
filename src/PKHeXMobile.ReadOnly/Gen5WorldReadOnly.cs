using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using PKHeX.Core;

namespace PKHeXMobile.ReadOnly;

/// <summary>One detached B2W2 medal. Invalid packed dates are represented, never parsed through Core's throwing Date getter.</summary>
public sealed record Gen5MedalReadOnly(
    [property: JsonRequired] int Index,
    [property: JsonRequired] ushort RawDate,
    [property: JsonRequired] byte RawState,
    [property: JsonRequired] byte ReservedByte,
    [property: JsonRequired] byte StateCode,
    [property: JsonRequired] bool IsUnread,
    [property: JsonRequired] bool ValidDate);

public sealed record Gen5MedalsReadOnly(
    [property: JsonRequired] IReadOnlyList<Gen5MedalReadOnly> Entries,
    [property: JsonRequired] IReadOnlyList<byte> HabitatRaw,
    [property: JsonRequired] byte PinnedMedalRaw,
    [property: JsonRequired] byte RankRaw,
    [property: JsonRequired] byte TutorialRaw,
    [property: JsonRequired] byte UnusedRaw,
    [property: JsonRequired] string RawSha256);

public sealed record Gen5KeySystemReadOnly(
    [property: JsonRequired] IReadOnlyList<uint> ObtainedWordsRaw,
    [property: JsonRequired] IReadOnlyList<uint> UnlockedWordsRaw,
    [property: JsonRequired] uint DifficultyWordRaw,
    [property: JsonRequired] uint CityWordRaw,
    [property: JsonRequired] uint ChamberWordRaw,
    [property: JsonRequired] uint CryptoWordRaw,
    [property: JsonRequired] string RawSha256);

public sealed record Gen5WorldReadOnly(
    [property: JsonRequired] Gen5MedalsReadOnly Medals,
    [property: JsonRequired] Gen5KeySystemReadOnly Keys)
{
    public const int MedalCount = 255;
    public const int HabitatBytes = HabitatList5.SIZE;
    public const int MedalBlockBytes = 0x498;
    public const int KeyBlockBytes = 0x60;

    public static Gen5WorldReadOnly? Project(SaveFile save)
    {
        ArgumentNullException.ThrowIfNull(save);
        return save is SAV5B2W2 b2w2
            ? new(ProjectMedals(b2w2.Medals.Data), ProjectKeys(b2w2.Keys.Data))
            : null;
    }

    public static bool IsValidMedalDate(ushort raw)
    {
        if (raw == 0) return false;
        int year = 2000 + (raw & 0x7F);
        int month = (raw >> 7) & 15;
        int day = raw >> 11;
        return month is >= 1 and <= 12 && day >= 1 && day <= DateTime.DaysInMonth(year, month);
    }

    /// <summary>Only raw reads from detached bytes. Never invoke Medal5.State setter, Date getter or Obtain.</summary>
    public static Gen5MedalsReadOnly ProjectMedals(ReadOnlySpan<byte> data)
    {
        if (data.Length < MedalBlockBytes)
            throw new InvalidDataException("Truncated B2W2 medal/habitat block.");
        var raw = data[..MedalBlockBytes].ToArray();
        var medals = new Gen5MedalReadOnly[MedalCount];
        for (int i = 0; i < medals.Length; i++)
        {
            int offset = i * Medal5.SIZE;
            ushort date = BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(offset, 2));
            byte state = raw[offset + 2];
            medals[i] = new(i, date, state, raw[offset + 3],
                (byte)(state & 7), (state & 8) != 0, IsValidMedalDate(date));
        }
        return new(Array.AsReadOnly(medals),
            Array.AsReadOnly(raw.AsSpan(0x400, HabitatBytes).ToArray()),
            raw[0x3FC], raw[0x3FD], raw[0x3FE], raw[0x3FF],
            Convert.ToHexString(SHA256.HashData(raw)));
    }

    /// <summary>Retain all five 32-bit words per key table and all three XOR-encoded selections.</summary>
    public static Gen5KeySystemReadOnly ProjectKeys(ReadOnlySpan<byte> data)
    {
        if (data.Length < KeyBlockBytes)
            throw new InvalidDataException("Truncated B2W2 Key System block.");
        var raw = data[..KeyBlockBytes].ToArray();
        var obtained = new uint[5];
        var unlocked = new uint[5];
        for (int i = 0; i < 5; i++)
        {
            obtained[i] = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(0x28 + 4 * i, 4));
            unlocked[i] = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(0x3C + 4 * i, 4));
        }
        return new(Array.AsReadOnly(obtained), Array.AsReadOnly(unlocked),
            BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(0x50, 4)),
            BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(0x54, 4)),
            BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(0x58, 4)),
            BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(0x5C, 4)),
            Convert.ToHexString(SHA256.HashData(raw)));
    }
}

/// <summary>Schema v15 baseline supplied separately from the inspected save; not independent parity by itself.</summary>
public sealed record ExpectedGen5WorldReadOnly(
    [property: JsonRequired] Gen5MedalsReadOnly Medals,
    [property: JsonRequired] Gen5KeySystemReadOnly Keys);

public static class Gen5WorldProjectionVerifier
{
    public static void Compare(Gen5WorldReadOnly? actual, ExpectedGen5WorldReadOnly? expected, List<string> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (actual?.Medals is not { } a || expected?.Medals is not { } e ||
            actual.Keys is not { } k || expected.Keys is not { } q)
        {
            errors.Add("Gen5World missing B2W2 medals or keys");
            return;
        }
        if (!ValidMedals(a) || !ValidMedals(e) || !ValidKeys(k) || !ValidKeys(q))
        {
            errors.Add("Gen5World invalid shape or raw fields");
            return;
        }
        for (int i = 0; i < Gen5WorldReadOnly.MedalCount; i++)
            if (a.Entries[i] != e.Entries[i])
                errors.Add($"Gen5World.Medals[{i}] mismatch");
        if (!a.HabitatRaw.SequenceEqual(e.HabitatRaw) ||
            a.PinnedMedalRaw != e.PinnedMedalRaw || a.RankRaw != e.RankRaw ||
            a.TutorialRaw != e.TutorialRaw || a.UnusedRaw != e.UnusedRaw ||
            !a.RawSha256.Equals(e.RawSha256, StringComparison.OrdinalIgnoreCase))
            errors.Add("Gen5World.Medals raw metadata mismatch");
        if (!k.ObtainedWordsRaw.SequenceEqual(q.ObtainedWordsRaw) ||
            !k.UnlockedWordsRaw.SequenceEqual(q.UnlockedWordsRaw) ||
            k.DifficultyWordRaw != q.DifficultyWordRaw ||
            k.CityWordRaw != q.CityWordRaw || k.ChamberWordRaw != q.ChamberWordRaw ||
            k.CryptoWordRaw != q.CryptoWordRaw ||
            !k.RawSha256.Equals(q.RawSha256, StringComparison.OrdinalIgnoreCase))
            errors.Add("Gen5World.Keys mismatch");
    }

    private static bool Digest(string? hash) =>
        hash is { Length: 64 } && hash.All(Uri.IsHexDigit);

    private static bool ValidMedals(Gen5MedalsReadOnly m)
    {
        if (m.Entries is null || m.Entries.Count != Gen5WorldReadOnly.MedalCount ||
            m.HabitatRaw is null || m.HabitatRaw.Count != Gen5WorldReadOnly.HabitatBytes ||
            !Digest(m.RawSha256)) return false;
        for (int i = 0; i < m.Entries.Count; i++)
        {
            var entry = m.Entries[i];
            if (entry is null || entry.Index != i ||
                entry.StateCode != (entry.RawState & 7) ||
                entry.IsUnread != ((entry.RawState & 8) != 0) ||
                entry.ValidDate != Gen5WorldReadOnly.IsValidMedalDate(entry.RawDate))
                return false;
        }
        return true;
    }

    private static bool ValidKeys(Gen5KeySystemReadOnly k) =>
        k.ObtainedWordsRaw is { Count: 5 } && k.UnlockedWordsRaw is { Count: 5 } &&
        Digest(k.RawSha256);
}
