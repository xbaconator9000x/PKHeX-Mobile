using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using PKHeX.Core;

namespace PKHeXMobile.ReadOnly;

/// <summary>
/// Raw, detached Generation 6 save-level diagnostics. These are not per-Pokémon
/// Super Training regimens, verified retail identities, or permission to write.
/// Avoid Core's mutable holder views and its unsafe AddBag allocator.
/// </summary>
public sealed record Gen6TrainingHolder(
    [property: JsonRequired] ushort Species,
    [property: JsonRequired] byte Form,
    [property: JsonRequired] byte Gender);

public sealed record Gen6SuperTrainingReadOnly(
    [property: JsonRequired] IReadOnlyList<byte> RegimenFlagsRaw,
    [property: JsonRequired] byte DistributionFlagsRaw,
    [property: JsonRequired] byte CounterRaw,
    [property: JsonRequired] IReadOnlyList<uint> RecordTime1Bits,
    [property: JsonRequired] IReadOnlyList<uint> RecordTime2Bits,
    [property: JsonRequired] IReadOnlyList<Gen6TrainingHolder> Holders1,
    [property: JsonRequired] IReadOnlyList<Gen6TrainingHolder> Holders2,
    [property: JsonRequired] IReadOnlyList<byte> BagsRaw,
    [property: JsonRequired] uint TutorialRaw,
    [property: JsonRequired] string RawSha256);

public sealed record Gen6OPowerReadOnly(
    [property: JsonRequired] IReadOnlyList<byte> FlagsRaw,
    [property: JsonRequired] byte PointsRaw,
    [property: JsonRequired] IReadOnlyList<byte> LevelsRaw,
    [property: JsonRequired] string RawSha256);

public sealed record Gen6SystemsReadOnly(
    [property: JsonRequired] Gen6SuperTrainingReadOnly SuperTraining,
    [property: JsonRequired] Gen6OPowerReadOnly OPower)
{
    public const int SuperTrainingBytes = 0x318;
    public const int PowerBytes = OPower6.Size;
    public const int TrainingRecords = 48;
    public const int TrainingBags = 12;
    public const int PowerFlags = 65;
    public const int PowerLevels = 34;

    public static Gen6SystemsReadOnly? Project(SaveFile save)
    {
        ArgumentNullException.ThrowIfNull(save);
        return save switch
        {
            SAV6XY xy => new(ProjectTraining(xy.SuperTrain.Data), ProjectPower(xy.OPower.Data)),
            SAV6AO ao => new(ProjectTraining(ao.SuperTrain.Data), ProjectPower(ao.OPower.Data)),
            _ => null,
        };
    }

    public static Gen6SuperTrainingReadOnly ProjectTraining(ReadOnlySpan<byte> input)
    {
        if (input.Length != SuperTrainingBytes)
            throw new InvalidDataException("Unreviewed Generation 6 Super Training block size.");
        var raw = input.ToArray();
        var time1 = new uint[TrainingRecords];
        var time2 = new uint[TrainingRecords];
        var holders1 = new Gen6TrainingHolder[TrainingRecords];
        var holders2 = new Gen6TrainingHolder[TrainingRecords];
        for (int i = 0; i < TrainingRecords; i++)
        {
            // Preserve the exact IEEE-754 bits, including NaN payloads and infinity.
            time1[i] = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(0x08 + i * 4, 4));
            time2[i] = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(0xC8 + i * 4, 4));
            holders1[i] = Holder(raw.AsSpan(0x188 + i * 4, 4));
            holders2[i] = Holder(raw.AsSpan(0x248 + i * 4, 4));
        }
        return new Gen6SuperTrainingReadOnly(
            Array.AsReadOnly(raw.AsSpan(0, 6).ToArray()),
            raw[6], raw[7],
            Array.AsReadOnly(time1), Array.AsReadOnly(time2),
            Array.AsReadOnly(holders1), Array.AsReadOnly(holders2),
            Array.AsReadOnly(raw.AsSpan(0x308, TrainingBags).ToArray()),
            BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(0x314, 4)),
            Convert.ToHexString(SHA256.HashData(raw)));
    }

    public static Gen6OPowerReadOnly ProjectPower(ReadOnlySpan<byte> input)
    {
        if (input.Length != PowerBytes)
            throw new InvalidDataException("Unreviewed Generation 6 O-Power block size.");
        var raw = input.ToArray();
        return new Gen6OPowerReadOnly(
            Array.AsReadOnly(raw.AsSpan(0, PowerFlags).ToArray()),
            raw[PowerFlags],
            Array.AsReadOnly(raw.AsSpan(PowerFlags + 1, PowerLevels).ToArray()),
            Convert.ToHexString(SHA256.HashData(raw)));
    }

    private static Gen6TrainingHolder Holder(ReadOnlySpan<byte> raw) =>
        new(BinaryPrimitives.ReadUInt16LittleEndian(raw), raw[2], raw[3]);
}

/// <summary>Schema v16 expectation supplied independently of the inspected document.</summary>
public sealed record ExpectedGen6SystemsReadOnly(
    [property: JsonRequired] Gen6SuperTrainingReadOnly SuperTraining,
    [property: JsonRequired] Gen6OPowerReadOnly OPower);

public static class Gen6SystemsProjectionVerifier
{
    public static void Compare(Gen6SystemsReadOnly? actual, ExpectedGen6SystemsReadOnly? expected,
        List<string> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (actual?.SuperTraining is not { } a || actual.OPower is not { } p ||
            expected?.SuperTraining is not { } b || expected.OPower is not { } q)
        {
            errors.Add("Gen6Systems missing Super Training or O-Power");
            return;
        }
        if (!Valid(a) || !Valid(b) || !Valid(p) || !Valid(q))
        {
            errors.Add("Gen6Systems invalid shape or raw fields");
            return;
        }
        if (!a.RegimenFlagsRaw.SequenceEqual(b.RegimenFlagsRaw) ||
            a.DistributionFlagsRaw != b.DistributionFlagsRaw ||
            a.CounterRaw != b.CounterRaw ||
            !a.RecordTime1Bits.SequenceEqual(b.RecordTime1Bits) ||
            !a.RecordTime2Bits.SequenceEqual(b.RecordTime2Bits) ||
            !a.Holders1.SequenceEqual(b.Holders1) ||
            !a.Holders2.SequenceEqual(b.Holders2) ||
            !a.BagsRaw.SequenceEqual(b.BagsRaw) ||
            a.TutorialRaw != b.TutorialRaw ||
            !string.Equals(a.RawSha256, b.RawSha256, StringComparison.OrdinalIgnoreCase))
            errors.Add("Gen6Systems.SuperTraining mismatch");

        if (!p.FlagsRaw.SequenceEqual(q.FlagsRaw) || p.PointsRaw != q.PointsRaw ||
            !p.LevelsRaw.SequenceEqual(q.LevelsRaw) ||
            !string.Equals(p.RawSha256, q.RawSha256, StringComparison.OrdinalIgnoreCase))
            errors.Add("Gen6Systems.OPower mismatch");
    }

    private static bool Digest(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static bool Valid(Gen6SuperTrainingReadOnly x) =>
        x.RegimenFlagsRaw is { Count: 6 } &&
        x.RecordTime1Bits is { Count: Gen6SystemsReadOnly.TrainingRecords } &&
        x.RecordTime2Bits is { Count: Gen6SystemsReadOnly.TrainingRecords } &&
        x.Holders1 is { Count: Gen6SystemsReadOnly.TrainingRecords } &&
        x.Holders2 is { Count: Gen6SystemsReadOnly.TrainingRecords } &&
        !x.Holders1.Any(h => h is null) && !x.Holders2.Any(h => h is null) &&
        x.BagsRaw is { Count: Gen6SystemsReadOnly.TrainingBags } &&
        Digest(x.RawSha256);

    private static bool Valid(Gen6OPowerReadOnly x) =>
        x.FlagsRaw is { Count: Gen6SystemsReadOnly.PowerFlags } &&
        x.LevelsRaw is { Count: Gen6SystemsReadOnly.PowerLevels } &&
        Digest(x.RawSha256);
}
