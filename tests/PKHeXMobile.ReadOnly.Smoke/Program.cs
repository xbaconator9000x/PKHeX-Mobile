using PKHeXMobile.Domain;
using PKHeXMobile.ReadOnly;

static void MustReject(byte[] bytes)
{
    var snapshot = SaveInputSnapshot.FromBytes(bytes);
    if (ReadOnlySaveInspector.TryInspect(snapshot, out var result, out var reason))
        throw new Exception($"Unexpectedly accepted synthetic garbage: {result?.CoreSaveType}");
    if (result is not null || string.IsNullOrWhiteSpace(reason))
        throw new Exception("Rejection must not return a save or a blank reason.");
}

MustReject(new byte[512]);
MustReject(new byte[0x20000]); // A Gen 3-sized all-zero buffer must not count as a valid save.
MustReject(new byte[0x8000]);  // A Gen 1-sized all-zero buffer must not count as a valid save.
MustReject([0x50, 0x4B, 0x03, 0x04, 0x00, 0x00]); // Archive must be rejected before Core.
MustReject([0x1F, 0x8B, 0x08, 0x00]); // GZip must be rejected before Core.

var snapshot = SaveInputSnapshot.FromBytes(new byte[512]);
if (ReadOnlySaveInspector.TryInspect(snapshot, out var accepted, out _))
    throw new Exception("Unexpected positive detection.");
if (accepted is not null)
    throw new Exception("A rejected save must have no projection.");

Console.WriteLine("Pinned PKHeX.Core read-only adapter compiled; 5 synthetic negative cases passed.");
Console.WriteLine("No genuine save fixture, supported game claim, Android UI or export has been tested.");
