namespace PKHeXMobile.Verify;

/// <summary>Bound untrusted offline manifests; this does not authenticate a desktop oracle.</summary>
public static class FixtureManifestLimits
{
    public const long MaxManifestBytes = 4L * 1024 * 1024;
    public const int MaxFixtures = 128;

    public static void ValidateFileSize(long bytes)
    {
        if (bytes is <= 0 or > MaxManifestBytes)
            throw new InvalidDataException("Fixture manifest exceeds the read-only size limit.");
    }

    public static void ValidateFixtureCount(int count)
    {
        if (count is <= 0 or > MaxFixtures)
            throw new InvalidDataException("Fixture count exceeds the read-only comparison limit.");
    }
}
