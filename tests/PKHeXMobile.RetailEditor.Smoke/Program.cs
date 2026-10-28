// Synthetic-only executable integration checks. This is NOT write certification.
using System.Buffers.Binary;
using PKHeX.Core;
using PKForge.Engine;
using PKHeXMobile.Domain;
using PKHeXMobile.PortConsolidation;

var checks = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    checks++;
}
void Refuses<T>(Action action, string label) where T : Exception
{
    try { action(); }
    catch (T) { checks++; return; }
    throw new Exception(label + ": expected " + typeof(T).Name);
}

// Generate synthetic retail save bytes with official pinned PKHeX.Core. No ROM,
// user save, upstream test-data file or proprietary asset is read or committed.
var save = BlankSaveFile.Get(GameVersion.X, "TEST", LanguageID.English);
var mon = save.BlankPKM;
mon.Species = 25;
mon.CurrentLevel = 12;
mon.SetNickname("ORIGINAL");
mon.RefreshChecksum();
save.SetBoxSlotAtIndex(mon, 0, 0, EntityImportSettings.None);
var original = save.Write().ToArray();
var untouched = original.ToArray();
var snapshot = SaveInputSnapshot.FromBytes(original);

var preview = DetachedRetailNicknameEditor.Preview(snapshot, 0, 0, "PREVIEW");
Check(original.AsSpan().SequenceEqual(untouched), "Original array mutated.");
Check(snapshot.CopyBytes().AsSpan().SequenceEqual(original), "Snapshot mutated.");
Check(preview.SourceFingerprint == snapshot.Fingerprint, "Source fingerprint changed.");
Check(preview.CandidateFingerprint != snapshot.Fingerprint, "Preview is a no-op.");
var candidate = preview.CopyCandidateBytes();
using (var reopened = new SaveEngineSession(candidate))
{
    Check(reopened.ReadEntity(0, 0).Nickname == "PREVIEW", "Edited nickname did not persist.");
    Check(reopened.ReadEntity(0, 1).IsEmpty, "Untouched slot unexpectedly populated.");
}
candidate[0] ^= 0xFF;
Check(preview.CopyCandidateBytes()[0] != candidate[0], "Candidate bytes leaked mutable state.");
Check(original.AsSpan().SequenceEqual(untouched), "Cancel/discard changed original bytes.");
Refuses<InvalidOperationException>(
    () => DetachedRetailNicknameEditor.Preview(snapshot, 0, 0, "ORIGINAL"), "no-op");
Refuses<InvalidOperationException>(
    () => DetachedRetailNicknameEditor.Preview(snapshot, 0, 1, "PREVIEW"), "empty slot");
Refuses<ArgumentException>(
    () => DetachedRetailNicknameEditor.Preview(snapshot, 0, 0, ""), "empty nickname");
Refuses<ArgumentException>(
    () => DetachedRetailNicknameEditor.Preview(snapshot, 0, 0, new string('A', 65)), "oversized nickname");

// The same retail-only adapter must refuse known hack signatures before preview.
var hack = new byte[0x20000];
for (var sector = 0; sector < 8; sector++)
    BinaryPrimitives.WriteUInt32LittleEndian(hack.AsSpan(sector * 0x1000 + 0xFF8, 4), 0x01121999);
Refuses<InvalidDataException>(
    () => DetachedRetailNicknameEditor.Preview(SaveInputSnapshot.FromBytes(hack), 0, 0, "TEST"),
    "Unbound-as-retail");
Check(original.AsSpan().SequenceEqual(untouched), "Negative tests mutated original.");
Console.WriteLine($"Detached official-Core PKForge nickname preview: {checks} synthetic checks passed.");
Console.WriteLine("No provider write, Save As, desktop parity or physical device certification performed.");
