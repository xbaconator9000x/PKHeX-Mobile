using System.Text.Json;
using System.Text.Json.Serialization;
using PKHeXMobile.ReadOnly;
using PKHeXMobile.Verify;

if (args.Length == 1 && args[0] is "--help" or "-h")
{
    Console.WriteLine("Usage: PKHeXMobile.Verify <local-fixture-manifest.json>");
    Console.WriteLine("Schema 1: basic slots; schema 2: details; schema 3: awards; schema 4: national dex flags; schema 5: Core-enumerated inventory; schema 6: physical storage slot flags; schema 7: Core trainer progress scalars; schema 8: format-aware training diagnostics; schema 9: raw move PP, PP Ups and relearn getters; schema 10: raw identity and egg provenance; schema 11: bounded nursery metadata and parent-byte digests; schema 12: separate Gen2 egg-buffer fingerprint; schema 13: raw Gen4/BDSP Underground; schema 14: raw HGSS gadgets and Pokéathlon; schema 15: B2W2 medals, habitats and Key System raw diagnostics.");
    Console.WriteLine("Compares consented local save projections to separately supplied expected values.");
    Console.WriteLine("Never modifies or exports saves. Never commit real save files or private manifests.");
    return 0;
}
if (args.Length != 1)
{
    Console.Error.WriteLine("Expected one local fixture manifest. Use --help.");
    return 64;
}

try
{
    string manifestPath = Path.GetFullPath(args[0]);
    FixtureManifestLimits.ValidateFileSize(new FileInfo(manifestPath).Length);
    var manifest = JsonSerializer.Deserialize<FixtureManifest>(
        await File.ReadAllTextAsync(manifestPath),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    if (manifest is null || manifest.SchemaVersion is not (1 or 2 or 3 or 4 or 5 or 6 or 7 or 8 or 9 or 10 or 11 or 12 or 13 or 14 or 15) || manifest.CoreVersion != "26.10.18" ||
        manifest.Fixtures is null)
    {
        Console.Error.WriteLine("Manifest must declare schemaVersion=1–15, coreVersion=26.10.18 and nonempty fixtures.");
        return 64;
    }

    FixtureManifestLimits.ValidateFixtureCount(manifest.Fixtures.Count);
    var root = Path.GetDirectoryName(manifestPath)!;
    int passed = 0;
    int failed = 0;
    for (int i = 0; i < manifest.Fixtures.Count; i++)
    {
        var fixture = manifest.Fixtures[i];
        if (fixture is null || string.IsNullOrWhiteSpace(fixture.Path) ||
            Path.IsPathRooted(fixture.Path) || fixture.Sha256 is null ||
            fixture.Sha256.Length != 64 || !fixture.Sha256.All(Uri.IsHexDigit) ||
            fixture.Expected is null)
        {
            Console.Error.WriteLine($"Fixture {i + 1}: invalid path, SHA-256 or expected projection.");
            failed++;
            continue;
        }
        try
        {
            var path = FixturePathPolicy.ResolveForRead(root, fixture.Path);
            var inspected = await ReadOnlyDocumentService.InspectAsync(cancellationToken =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete, 64 * 1024,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                return ValueTask.FromResult(stream);
            });

            if (!string.Equals(inspected.Fingerprint.Sha256Hex, fixture.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine($"Fixture {i + 1}: input SHA-256 differs from manifest.");
                failed++;
                continue;
            }
            if (!inspected.IsRecognized || inspected.Result is null)
            {
                Console.Error.WriteLine($"Fixture {i + 1}: reader refused the input ({inspected.RejectionReason}).");
                failed++;
                continue;
            }

            var differences = SaveProjectionVerifier.Compare(inspected.Result, fixture.Expected,
                requireDetails: manifest.SchemaVersion >= 2,
                requireAwards: manifest.SchemaVersion >= 3,
                requirePokedex: manifest.SchemaVersion >= 4,
                requireInventory: manifest.SchemaVersion >= 5,
                requireStorage: manifest.SchemaVersion >= 6,
                requireTrainerProgress: manifest.SchemaVersion >= 7,
                requireTraining: manifest.SchemaVersion >= 8,
                requireMoveState: manifest.SchemaVersion >= 9,
                requireIdentity: manifest.SchemaVersion >= 10,
                requireDaycare: manifest.SchemaVersion >= 11,
                requireGen2Egg: manifest.SchemaVersion >= 12,
                requireUnderground: manifest.SchemaVersion >= 13 &&
                    inspected.Result.Underground is not null,
                requireHgss: manifest.SchemaVersion >= 14 &&
                    inspected.Result.Hgss is not null,
                requireGen5World: manifest.SchemaVersion >= 15 &&
                    inspected.Result.Gen5World is not null);
            if (differences.Count != 0)
            {
                Console.Error.WriteLine($"Fixture {i + 1}: {differences.Count} projection differences: {string.Join(", ", differences)}");
                failed++;
                continue;
            }
            passed++;
            Console.WriteLine($"Fixture {i + 1}: PASS (read-only projection matches supplied baseline).");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException or ArgumentException)
        {
            Console.Error.WriteLine($"Fixture {i + 1}: cannot safely read or compare source.");
            failed++;
        }
    }

    Console.WriteLine($"Result: {passed} passed, {failed} failed. Baseline provenance remains externally verifiable.");
    return failed == 0 ? 0 : 1;
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidDataException)
{
    Console.Error.WriteLine("Cannot read or parse the local manifest.");
    return 64;
}

internal sealed record FixtureManifest(
    [property: JsonRequired] int SchemaVersion,
    [property: JsonRequired] string CoreVersion,
    [property: JsonRequired] IReadOnlyList<FixtureCase> Fixtures);

internal sealed record FixtureCase(
    [property: JsonRequired] string Path,
    [property: JsonRequired] string Sha256,
    [property: JsonRequired] ExpectedSaveProjection Expected);
