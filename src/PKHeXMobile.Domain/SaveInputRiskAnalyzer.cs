using System.Buffers.Binary;

namespace PKHeXMobile.Domain;

/// <summary>
/// Conservative raw-byte warnings, NOT game authentication. A matching marker can be
/// forged, and an unrecognized hack can have no marker at all. Never grants write access.
/// </summary>
public enum SaveInputRiskCode
{
    CompressedContainer,
    RetroArchRzipContainer,
    PossibleUnbound,
    PossibleGsChronicles,
    ConflictingGen3Signatures,
    PossibleLuminescentPlatinum,
    PossibleCfruSectorLayout,
}

public sealed record SaveInputRisk(SaveInputRiskCode Code, string Explanation);

public static class SaveInputRiskAnalyzer
{
    private const int Gen3SectorSize = 0x1000;
    private const int Gen3FullSize = 0x20000;
    private const int Gen3SignatureOffset = 0xFF8;
    private const uint UnboundSignature = 0x01121999;
    private const uint GsChroniclesSignature = 0x66290096;

    // Exact pinned PKForge-PKHeX/RadicalRedFormat.cs source tables (GPLv3).
    // https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/src/PKForge.Engine/RadicalRed/RadicalRedFormat.cs
    // Reimplemented as a negative-only risk screen, not a Radical Red parser.
    private static ReadOnlySpan<ushort> CfruWindows =>
    [
        0xF24, 0xFF0, 0xFF0, 0xFF0, 0xD98,
        0xFF0, 0xFF0, 0xFF0, 0xFF0, 0xFF0,
        0xFF0, 0xFF0, 0xFF0, 0x450,
    ];
    private static ReadOnlySpan<ushort> RetailFrlgWindows =>
    [
        0xF2C, 0xF80, 0xF80, 0xF80, 0xF08,
        0xF80, 0xF80, 0xF80, 0xF80, 0xF80,
        0xF80, 0xF80, 0xF80, 0x7D0,
    ];

    /// <summary>
    /// Check only documented high-risk envelopes before invoking the retail Core parser.
    /// This deliberately does not guess that an input is a supported official save.
    /// </summary>
    public static SaveInputRisk? Assess(ReadOnlySpan<byte> data)
    {
        // Pinned PKForge's RetroArchSaveContainer identifies all #RZIP prefixes,
        // including truncated/unsupported revisions. Never pass these to retail Core.
        // https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/src/PKForge.Engine/RetroArchSaveContainer.cs
        if (data.StartsWith("#RZIP"u8))
            return new(SaveInputRiskCode.RetroArchRzipContainer,
                "RetroArch RZIP compressed saves are unsupported; no decoder or write path is enabled.");

        if (IsArchive(data))
            return new(SaveInputRiskCode.CompressedContainer,
                "Compressed save containers are not enabled for inspection.");

        // PKForge's pinned Luminescent Platinum detector uses these two lengths
        // and the reserved high revision word. A marker alone does not verify a mod.
        if ((data.Length == 0xEDC20 || data.Length == 0xEF0A4) &&
            (BinaryPrimitives.ReadUInt32LittleEndian(data) & 0xFFFF0000) == 0xFFFF0000)
            return new(SaveInputRiskCode.PossibleLuminescentPlatinum,
                "A possible Luminescent Platinum save requires a separately verified mod profile.");

        if (data.Length < Gen3FullSize)
            return null;

        // Two independently documented CFRU-family footer signatures. Require at
        // least eight sector matches, as in pinned PKForge. This is a risk screen,
        // not a complete Radical Red, CFRU or pokeemerald-expansion detector.
        int unbound = 0;
        int chronicles = 0;
        for (int sector = 0; sector < 32; sector++)
        {
            int offset = sector * Gen3SectorSize + Gen3SignatureOffset;
            uint signature = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset, sizeof(uint)));
            if (signature == UnboundSignature)
                unbound++;
            else if (signature == GsChroniclesSignature)
                chronicles++;
        }

        if (unbound >= 8 && chronicles >= 8)
            return new(SaveInputRiskCode.ConflictingGen3Signatures,
                "Conflicting non-retail Gen 3 sector signatures require manual format identification.");
        if (unbound >= 8)
            return new(SaveInputRiskCode.PossibleUnbound,
                "Possible Pokémon Unbound sector signatures: the retail Gen 3 reader is not safe for this layout.");
        if (chronicles >= 8)
            return new(SaveInputRiskCode.PossibleGsChronicles,
                "Possible GS Chronicles sector signatures: the retail Gen 3 reader is not safe for this layout.");

        if (LooksLikeCfruWindowLayout(data))
            return new(SaveInputRiskCode.PossibleCfruSectorLayout,
                "The Gen 3 sector checksums match a CFRU-style window layout but differ from retail FireRed/LeafGreen; do not interpret this as a retail save.");

        return null;
    }

    private static bool LooksLikeCfruWindowLayout(ReadOnlySpan<byte> data)
    {
        // Scan only sectors with retail-family signatures; extras and raw CFRU
        // sector 30/31 payloads are not regular save fragments.
        Span<int> offsets = stackalloc int[14];
        Span<uint> indices = stackalloc uint[14];
        offsets.Fill(-1);
        for (int sector = 0; sector < 32; sector++)
        {
            int off = sector * Gen3SectorSize;
            uint signature = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(off + Gen3SignatureOffset, 4));
            if ((signature & 0xFFFFFF00u) != 0x08012000u)
                continue;
            int id = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(off + 0xFF4, 2));
            if (id >= 14)
                continue;
            uint index = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(off + 0xFFC, 4));
            if (offsets[id] < 0 || index >= indices[id])
            {
                indices[id] = index;
                offsets[id] = off;
            }
        }

        int retailMismatches = 0;
        for (int id = 0; id < 14; id++)
        {
            int off = offsets[id];
            if (off < 0)
                return false;
            ushort stored = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(off + 0xFF6, 2));
            if (Checksum(data.Slice(off, CfruWindows[id])) != stored)
                return false;
            if (Checksum(data.Slice(off, RetailFrlgWindows[id])) != stored)
                retailMismatches++;
        }

        if (retailMismatches == 0)
            return false;
        int partyOffset = offsets[1];
        uint count = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(partyOffset + 0x34, 4));
        return count <= 6;
    }

    private static ushort Checksum(ReadOnlySpan<byte> data)
    {
        uint total = 0;
        for (int i = 0; i < (data.Length & ~3); i += 4)
            total = unchecked(total + BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(i, 4)));
        return unchecked((ushort)(total + (total >> 16)));
    }

    private static bool IsArchive(ReadOnlySpan<byte> data) =>
        (data.Length >= 4 && data[0] == 0x50 && data[1] == 0x4B &&
         ((data[2] == 0x03 && data[3] == 0x04) ||
          (data[2] == 0x05 && data[3] == 0x06) ||
          (data[2] == 0x07 && data[3] == 0x08))) ||
        (data.Length >= 2 && data[0] == 0x1F && data[1] == 0x8B);
}
