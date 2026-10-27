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
Console.WriteLine("Fixture path policy: nested relative path, traversal, cross-platform absolute paths, missing files and symlink escapes passed.");
