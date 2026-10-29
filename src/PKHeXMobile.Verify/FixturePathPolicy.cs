namespace PKHeXMobile.Verify;

/// <summary>
/// Restricts private fixture manifest entries to regular files below the
/// manifest directory. Rejects traversal, platform-dependent separators,
/// and symlinks/reparse points in the relative path. This is a read-only
/// hygiene boundary, NOT an atomic defence against hostile concurrent
/// filesystem replacement; use private, trusted fixture directories.
/// </summary>
public static class FixturePathPolicy
{
    public static string ResolveForRead(string manifestDirectory, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestDirectory);
        if (string.IsNullOrWhiteSpace(relativePath) ||
            Path.IsPathRooted(relativePath) ||
            relativePath.Contains('\\') ||
            relativePath.Contains(':'))
            throw new InvalidDataException("Fixture path must be a portable relative path.");

        var segments = relativePath.Split('/');
        if (segments.Any(segment => segment.Length == 0 || segment is "." or ".."))
            throw new InvalidDataException("Fixture path contains an invalid component.");

        string root = Path.GetFullPath(manifestDirectory);
        string current = root;
        foreach (var segment in segments)
        {
            current = Path.Combine(current, segment);
            var attributes = File.GetAttributes(current);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Fixture paths cannot traverse symlinks or reparse points.");
        }

        if ((File.GetAttributes(current) & FileAttributes.Directory) != 0)
            throw new InvalidDataException("Fixture path must identify a regular file.");

        string full = Path.GetFullPath(current);
        string prefix = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!full.StartsWith(prefix, comparison))
            throw new InvalidDataException("Fixture path escapes its manifest directory.");
        return full;
    }
}
