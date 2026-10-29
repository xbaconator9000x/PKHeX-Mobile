using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using PKHeXMobile.ReadOnly;
using PKHeXMobile.Verify;

static void MustReject(string root, string path)
{
    try
    {
        _ = FixturePathPolicy.ResolveForRead(root, path);
        throw new Exception("Unsafe fixture path was accepted.");
    }
    catch (Exception error) when (error is InvalidDataException or IOException or ArgumentException) { }
}

var root = Path.Combine(Path.GetTempPath(), "pkhex-fixture-path-" + Guid.NewGuid().ToString("N"));
var outside = Path.Combine(Path.GetTempPath(), "pkhex-fixture-outside-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
Directory.CreateDirectory(outside);
try
{
    Directory.CreateDirectory(Path.Combine(root, "nested"));
    File.WriteAllBytes(Path.Combine(root, "nested", "fixture.bin"), [1, 2, 3, 4]);
    File.WriteAllBytes(Path.Combine(outside, "external.bin"), [5, 6, 7]);

    var allowed = FixturePathPolicy.ResolveForRead(root, "nested/fixture.bin");
    if (allowed != Path.GetFullPath(Path.Combine(root, "nested", "fixture.bin")))
        throw new Exception("Safe nested fixture path resolved incorrectly.");

    foreach (var path in new[]
    {
        "", " ", "../external.bin", "nested/../nested/fixture.bin",
        "nested/./fixture.bin", "nested//fixture.bin", "nested\\fixture.bin",
        "C:\\temp\\external.bin", "/tmp/external.bin", "nested",
        "missing.bin",
    })
        MustReject(root, path);

    // Linux CI permits unprivileged symlink creation. This checks both a final
    // file symlink and a symlink in an intermediate directory component.
    var linkedFile = Path.Combine(root, "linked.bin");
    var linkedDirectory = Path.Combine(root, "linked-directory");
    File.CreateSymbolicLink(linkedFile, Path.Combine(outside, "external.bin"));
    Directory.CreateSymbolicLink(linkedDirectory, outside);
    MustReject(root, "linked.bin");
    MustReject(root, "linked-directory/external.bin");
    File.Delete(linkedFile);
    Directory.Delete(linkedDirectory);
}
finally
{
    Directory.Delete(root, recursive: true);
    Directory.Delete(outside, recursive: true);
}
foreach (long bytes in new long[] { -1, 0, FixtureManifestLimits.MaxManifestBytes + 1 })
{
    try { FixtureManifestLimits.ValidateFileSize(bytes); throw new Exception("Invalid manifest size accepted."); }
    catch (InvalidDataException) { }
}
FixtureManifestLimits.ValidateFileSize(1);
FixtureManifestLimits.ValidateFileSize(FixtureManifestLimits.MaxManifestBytes);
foreach (int count in new[] { -1, 0, FixtureManifestLimits.MaxFixtures + 1 })
{
    try { FixtureManifestLimits.ValidateFixtureCount(count); throw new Exception("Invalid fixture count accepted."); }
    catch (InvalidDataException) { }
}
FixtureManifestLimits.ValidateFixtureCount(1);
FixtureManifestLimits.ValidateFixtureCount(FixtureManifestLimits.MaxFixtures);
Console.WriteLine("Fixture path policy and manifest bounds: traversal, symlinks, size and fixture count passed.");


static void CheckOutcome(int schema, string? declaration, bool projection, bool accepted, FixtureExpectedOutcome expected)
{
    if (FixtureOutcomePolicy.TryResolve(schema, declaration, projection, out var actual) != accepted ||
        (accepted && actual != expected))
        throw new Exception("Fixture outcome contract mismatch.");
}

// Legacy manifests remain positive-only and require their expected projection.
CheckOutcome(16, null, true, true, FixtureExpectedOutcome.Recognized);
CheckOutcome(16, "refused", false, false, FixtureExpectedOutcome.Recognized);
CheckOutcome(16, null, false, false, FixtureExpectedOutcome.Recognized);

// New private manifests must explicitly distinguish positive and negative cases.
CheckOutcome(17, "recognized", true, true, FixtureExpectedOutcome.Recognized);
CheckOutcome(17, "refused", false, true, FixtureExpectedOutcome.Refused);
CheckOutcome(17, null, true, false, FixtureExpectedOutcome.Recognized);
CheckOutcome(17, "refused", true, false, FixtureExpectedOutcome.Refused);
CheckOutcome(17, "recognized", false, false, FixtureExpectedOutcome.Recognized);
CheckOutcome(17, "Refused", false, false, FixtureExpectedOutcome.Refused);
CheckOutcome(17, "unknown", false, false, FixtureExpectedOutcome.Refused);

if (!FixtureOutcomePolicy.RefusalMatches(false, "Pinned Core rejected input.") ||
    FixtureOutcomePolicy.RefusalMatches(true, "Pinned Core rejected input.") ||
    FixtureOutcomePolicy.RefusalMatches(false, null) ||
    FixtureOutcomePolicy.RefusalMatches(false, "  "))
    throw new Exception("Refusal policy must fail closed for recognized or unexplained results.");

// Actual two-open Core-backed negative case: synthetic ZIP signature, no
// proprietary save, no file writes, and no positive desktop parity assertion.
byte[] invalidFixture = new byte[512];
invalidFixture[0] = 0x50;
invalidFixture[1] = 0x4B;
invalidFixture[2] = 0x03;
invalidFixture[3] = 0x04;
var untouched = invalidFixture.ToArray();
int openCount = 0;
var refusal = await ReadOnlyDocumentService.InspectAsync(ct =>
{
    ct.ThrowIfCancellationRequested();
    openCount++;
    return ValueTask.FromResult<Stream>(new MemoryStream(invalidFixture, writable: false));
});
if (openCount != 2 || !FixtureOutcomePolicy.RefusalMatches(refusal.IsRecognized, refusal.RejectionReason) ||
    refusal.Result is not null || !invalidFixture.SequenceEqual(untouched))
    throw new Exception("Two-open negative fixture was accepted, mutated or incompletely verified.");
Console.WriteLine("Fixture schema-17 negative/positive policy and synthetic two-open refusal passed.");


// Exercise the real CLI manifest parser and reader, not only the policy helper.
// All inputs are generated in a private temporary directory and removed.
static async Task<int> RunVerifierAsync(string project, string manifest)
{
    var psi = new ProcessStartInfo("dotnet")
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
    };
    foreach (var arg in new[] { "run", "--no-build", "--project", project,
        "--configuration", "Release", "--", manifest })
        psi.ArgumentList.Add(arg);
    using var process = Process.Start(psi) ?? throw new Exception("Could not start fixture verifier.");
    _ = await process.StandardOutput.ReadToEndAsync();
    _ = await process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    return process.ExitCode;
}
var projectPath = Path.Combine(Directory.GetCurrentDirectory(), "src",
    "PKHeXMobile.Verify", "PKHeXMobile.Verify.csproj");
if (!File.Exists(projectPath))
    throw new Exception("Run fixture smoke from the repository root.");
var fixtureRoot = Path.Combine(Path.GetTempPath(), "pkhex-negative-fixture-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(fixtureRoot);
try
{
    var binaryPath = Path.Combine(fixtureRoot, "synthetic.bin");
    var manifestPath = Path.Combine(fixtureRoot, "manifest.json");
    await File.WriteAllBytesAsync(binaryPath, invalidFixture);
    var digest = Convert.ToHexString(SHA256.HashData(invalidFixture));
    async Task<int> VerifyAsync(int schema, string hash, string? outcome)
    {
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(new
        {
            schemaVersion = schema,
            coreVersion = "26.10.18",
            fixtures = new[] { new { path = "synthetic.bin", sha256 = hash,
                expectedOutcome = outcome } },
        }));
        return await RunVerifierAsync(projectPath, manifestPath);
    }
    if (await VerifyAsync(17, digest, "refused") != 0 ||
        await VerifyAsync(17, new string('0', 64), "refused") == 0 ||
        await VerifyAsync(17, digest, "recognized") == 0 ||
        await VerifyAsync(16, digest, null) == 0)
        throw new Exception("CLI negative fixture success/refusal/hash/legacy gates failed.");
    var diskBytes = await File.ReadAllBytesAsync(binaryPath);
    if (!invalidFixture.SequenceEqual(diskBytes))
        throw new Exception("Verifier modified the synthetic fixture on disk.");
}
finally
{
    Directory.Delete(fixtureRoot, recursive: true);
}
Console.WriteLine("Fixture schema-17 CLI positive/negative contract and input-hash refusal passed.");
