using System.Security.Cryptography;

namespace PKHeXMobile.Domain;

/// <summary>A zero-based half-open byte interval in a particular, independently authenticated save layout.</summary>
public readonly record struct SaveByteRange(int Offset, int Length)
{
    public int EndExclusive => checked(Offset + Length);
}

/// <summary>A contiguous run of bytes that differ from the original input.</summary>
public readonly record struct SaveChangedRun(int Offset, int Length);

/// <summary>
/// Pure, byte-exact candidate change audit. This is not a save parser, format authenticator,
/// checksum validator, or permission to publish a save. Ranges MUST originate from an
/// independently verified, version-specific layout; never from a filename or UI claim.
/// </summary>
public sealed class SaveCandidateScopeAudit
{
    public const int MaxApprovedRanges = 4096;
    public const int MaxReportedRuns = 4096;

    private SaveCandidateScopeAudit(
        SaveFingerprint original, SaveFingerprint candidate, int changedByteCount,
        IReadOnlyList<SaveByteRange> approvedRanges, IReadOnlyList<SaveChangedRun> changedRuns)
    {
        Original = original;
        Candidate = candidate;
        ChangedByteCount = changedByteCount;
        ApprovedRanges = approvedRanges;
        ChangedRuns = changedRuns;
    }

    public SaveFingerprint Original { get; }
    public SaveFingerprint Candidate { get; }
    public int ChangedByteCount { get; }
    public IReadOnlyList<SaveByteRange> ApprovedRanges { get; }
    public IReadOnlyList<SaveChangedRun> ChangedRuns { get; }

    /// <summary>
    /// Refuse size changes, invalid/overlapping scopes, unapproved byte changes, no-op
    /// edits (unless explicitly allowed), and unbounded diagnostic output.
    /// No original or candidate byte array is retained or exposed.
    /// </summary>
    public static SaveCandidateScopeAudit Analyze(
        SaveInputSnapshot original,
        ReadOnlySpan<byte> candidate,
        IEnumerable<SaveByteRange> approvedRanges,
        bool requireChange = true)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(approvedRanges);
        if (candidate.Length != original.Length)
            throw new InvalidOperationException("Candidate size differs from the original save.");

        var ranges = new List<SaveByteRange>();
        foreach (var range in approvedRanges)
        {
            if (ranges.Count == MaxApprovedRanges)
                throw new InvalidOperationException("Too many approved byte ranges.");
            if (range.Offset < 0 || range.Length <= 0 ||
                range.Offset > original.Length || range.Length > original.Length - range.Offset)
                throw new InvalidOperationException("An approved byte range is outside the original save.");
            ranges.Add(range);
        }
        if (ranges.Count == 0)
            throw new InvalidOperationException("No independently approved byte ranges were supplied.");

        ranges.Sort((a, b) => a.Offset.CompareTo(b.Offset));
        for (int i = 1; i < ranges.Count; i++)
        {
            if (ranges[i].Offset < ranges[i - 1].EndExclusive)
                throw new InvalidOperationException("Approved byte ranges overlap.");
        }

        var source = original.CopyBytes();
        var changedRuns = new List<SaveChangedRun>();
        int rangeIndex = 0;
        int runStart = -1;
        int changedCount = 0;
        for (int i = 0; i < source.Length; i++)
        {
            if (source[i] == candidate[i])
            {
                if (runStart >= 0)
                {
                    AddRun(changedRuns, runStart, i - runStart);
                    runStart = -1;
                }
                continue;
            }

            while (rangeIndex < ranges.Count && i >= ranges[rangeIndex].EndExclusive)
                rangeIndex++;
            if (rangeIndex == ranges.Count || i < ranges[rangeIndex].Offset)
                throw new InvalidOperationException($"Candidate changed unapproved byte at offset {i}.");

            if (runStart < 0)
                runStart = i;
            changedCount++;
        }
        if (runStart >= 0)
            AddRun(changedRuns, runStart, source.Length - runStart);
        if (requireChange && changedCount == 0)
            throw new InvalidOperationException("The candidate contains no changes.");

        var digest = SHA256.HashData(candidate);
        return new SaveCandidateScopeAudit(
            original.Fingerprint,
            new SaveFingerprint(candidate.Length, Convert.ToHexString(digest)),
            changedCount,
            ranges.AsReadOnly(),
            changedRuns.AsReadOnly());
    }

    private static void AddRun(List<SaveChangedRun> runs, int offset, int length)
    {
        if (runs.Count == MaxReportedRuns)
            throw new InvalidOperationException("Too many disjoint candidate changes.");
        runs.Add(new SaveChangedRun(offset, length));
    }
}
