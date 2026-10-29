using System.Buffers.Binary;
using System.Security.Cryptography;
using PKHeX.Core;

namespace PKHeXMobile.ReadOnly;

/// <summary>
/// Source-grounded, immutable diagnostics. Never call Core setters or expose
/// mutable Roamer3 / HoneyTreeValue buffers. This is not layout authentication.
/// </summary>
public sealed record RoamerReadOnly(
    uint PID, ushort Species, ushort CurrentHp, byte Level, byte Status,
    bool IsActive, bool IsGlitched, uint RawIv32,
    IReadOnlyList<int> IVs, IReadOnlyList<int> EncounterGlitchIVs,
    string RawSha256);

public sealed record HoneyTreeEntryReadOnly(
    int Index, uint Time, int Slot, int SubTable, int Group, int Shake,
    string RawSha256);

public sealed record WorldStateReadOnly(
    RoamerReadOnly? Roamer, IReadOnlyList<HoneyTreeEntryReadOnly> HoneyTrees)
{
    public const int SinnohHoneyTreeCount = 21;

    public static WorldStateReadOnly? Project(SaveFile save)
    {
        ArgumentNullException.ThrowIfNull(save);
        return save switch
        {
            SAV3 gen3 => new WorldStateReadOnly(
                ProjectRoamer(new Roamer3(gen3.LargeBlock)),
                Array.Empty<HoneyTreeEntryReadOnly>()),
            SAV4Sinnoh sinnoh => new WorldStateReadOnly(
                null, ProjectHoneyTrees(sinnoh)),
            _ => null,
        };
    }

    /// <summary>
    /// A private copy is mandatory: Core's IVsGlitch getter temporarily writes
    /// the live IV32 property, and negative IV setters can contaminate other bits.
    /// The encounter glitch is calculated purely from a detached packed value.
    /// </summary>
    public static RoamerReadOnly ProjectRoamer(Roamer3 roamer)
    {
        ArgumentNullException.ThrowIfNull(roamer);
        if (roamer.Raw.Length < Roamer3.SIZE)
            throw new InvalidDataException("Truncated Gen3 roamer record.");
        var detached = roamer.Raw[..Roamer3.SIZE].ToArray();
        var clone = new Roamer3(detached, roamer.IsGlitched);
        uint rawIv = clone.IV32;
        var glitchIv = new int[6];
        uint lowByte = rawIv & 0xFF;
        for (int i = 0; i < glitchIv.Length; i++)
            glitchIv[i] = (int)((lowByte >> (5 * i)) & 31);
        return new RoamerReadOnly(clone.PID, clone.Species, clone.HP_Current,
            clone.CurrentLevel, clone.Status, clone.IsActive, clone.IsGlitched,
            rawIv, Array.AsReadOnly(clone.IVs), Array.AsReadOnly(glitchIv),
            Convert.ToHexString(SHA256.HashData(detached)));
    }

    public static IReadOnlyList<HoneyTreeEntryReadOnly> ProjectHoneyTrees(SAV4Sinnoh save)
    {
        ArgumentNullException.ThrowIfNull(save);
        var trees = new HoneyTreeEntryReadOnly[SinnohHoneyTreeCount];
        for (int i = 0; i < trees.Length; i++)
            trees[i] = ProjectHoneyTree(save, i);
        return Array.AsReadOnly(trees);
    }

    /// <summary>
    /// Official Core accepts index 21 even though only 0–20 are trees.
    /// Check bounds BEFORE calling its getter, and never normalize Group.
    /// </summary>
    public static HoneyTreeEntryReadOnly ProjectHoneyTree(SAV4Sinnoh save, int index)
    {
        ArgumentNullException.ThrowIfNull(save);
        if ((uint)index >= SinnohHoneyTreeCount)
            throw new ArgumentOutOfRangeException(nameof(index), index, "Expected tree index 0–20.");
        var raw = save.GetHoneyTree(index).Data.ToArray();
        if (raw.Length != HoneyTreeValue.Size)
            throw new InvalidDataException("Unexpected Sinnoh honey-tree record length.");
        return new HoneyTreeEntryReadOnly(index,
            BinaryPrimitives.ReadUInt32LittleEndian(raw),
            raw[4], raw[5], raw[6], raw[7],
            Convert.ToHexString(SHA256.HashData(raw)));
    }
}
