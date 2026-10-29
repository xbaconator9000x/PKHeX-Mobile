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
// Party is a distinct slot namespace (-1), not another storage box.
var partyMon = save.BlankPKM;
partyMon.Species = 133;
partyMon.CurrentLevel = 8;
partyMon.SetNickname("PARTYONE");
partyMon.RefreshChecksum();
save.SetPartySlotAtIndex(partyMon, 0, EntityImportSettings.None);
// BlankSaveFile creates an editor template, not a recognized cartridge dump.
// The pinned SaveUtil.IsG6XY requires the BEEF footer at length - 0x1F0.
// Stamp this synthetic fixture BEFORE Write recomputes the block checksums.
BinaryPrimitives.WriteUInt32LittleEndian(save.Data[^0x1F0..], 0x42454546);
var original = save.Write().ToArray();
var untouched = original.ToArray();
var snapshot = SaveInputSnapshot.FromBytes(original);

var preview = DetachedRetailNicknameEditor.Preview(snapshot, 0, 0, "PREVIEW");
Check(original.AsSpan().SequenceEqual(untouched), "Original array mutated.");
Check(snapshot.CopyBytes().AsSpan().SequenceEqual(original), "Snapshot mutated.");
Check(preview.SourceFingerprint == snapshot.Fingerprint, "Source fingerprint changed.");
Check(preview.CandidateFingerprint != snapshot.Fingerprint, "Preview is a no-op.");
Check(preview.Box == 0 && preview.Slot == 0, "Preview lost its selected box coordinate.");
Check(preview.OriginalNickname == "ORIGINAL" && preview.PreviewNickname == "PREVIEW",
    "Preview nickname metadata disagrees with the reopened editor.");
preview.RequireMatchingSource(SaveInputSnapshot.FromBytes(original));
var candidate = preview.CopyCandidateBytes();
using (var reopened = new SaveEngineSession(candidate))
{
    Check(reopened.ReadEntity(0, 0).Nickname == "PREVIEW", "Edited nickname did not persist.");
    Check(reopened.ReadEntity(0, 1).IsEmpty, "Untouched slot unexpectedly populated.");
    Check(reopened.ReadEntity(-1, 0).Nickname == "PARTYONE",
        "Box preview unexpectedly changed the party.");
}
candidate[0] ^= 0xFF;
Check(preview.CopyCandidateBytes()[0] != candidate[0], "Candidate bytes leaked mutable state.");
Check(original.AsSpan().SequenceEqual(untouched), "Cancel/discard changed original bytes.");

// A second detached session edits the party while leaving box storage untouched.
// Neither preview may mutate the source array or the other preview's candidate.
var partyPreview = DetachedRetailNicknameEditor.Preview(snapshot, -1, 0, "NEWPARTY");
Check(partyPreview.Box == -1 && partyPreview.Slot == 0, "Party coordinates were not retained.");
Check(partyPreview.OriginalNickname == "PARTYONE" && partyPreview.PreviewNickname == "NEWPARTY",
    "Party nickname metadata was not captured.");
using (var reopenedParty = new SaveEngineSession(partyPreview.CopyCandidateBytes()))
{
    Check(reopenedParty.ReadEntity(-1, 0).Nickname == "NEWPARTY",
        "Party edit did not survive a separate reopen.");
    Check(reopenedParty.ReadEntity(0, 0).Nickname == "ORIGINAL",
        "Party edit unexpectedly changed the boxed Pokémon.");
    Check(reopenedParty.ReadEntity(-1, 1).IsEmpty, "Party edit populated another slot.");
}
Check(preview.CopyCandidateBytes().AsSpan().SequenceEqual(
    DetachedRetailNicknameEditor.Preview(snapshot, 0, 0, "PREVIEW").CopyCandidateBytes()),
    "Interleaved party preview contaminated the box preview.");
Check(original.AsSpan().SequenceEqual(untouched), "Party preview changed original bytes.");

// A content fingerprint alone is not provider identity, but a changed snapshot
// must invalidate any preview before a later independent document preflight.
var stale = original.ToArray();
stale[^1] ^= 0x01;
Refuses<InvalidOperationException>(() =>
    preview.RequireMatchingSource(SaveInputSnapshot.FromBytes(stale)), "stale preview source");
Check(original.AsSpan().SequenceEqual(untouched), "Stale-source refusal mutated original.");
Refuses<InvalidOperationException>(
    () => DetachedRetailNicknameEditor.Preview(snapshot, 0, 0, "ORIGINAL"), "no-op");
Refuses<InvalidOperationException>(
    () => DetachedRetailNicknameEditor.Preview(snapshot, 0, 1, "PREVIEW"), "empty slot");
Refuses<ArgumentException>(
    () => DetachedRetailNicknameEditor.Preview(snapshot, 0, 0, ""), "empty nickname");
Refuses<ArgumentException>(
    () => DetachedRetailNicknameEditor.Preview(snapshot, 0, 0, new string('A', 65)), "oversized nickname");
Refuses<ArgumentException>(
    () => DetachedRetailNicknameEditor.Preview(snapshot, 0, 0, "LINE\nBREAK"), "control-character nickname");
Refuses<ArgumentException>(
    () => DetachedRetailNicknameEditor.Preview(snapshot, 0, 0, "EMOJI\uD83D\uDE00"), "surrogate nickname");
Refuses<ArgumentException>(
    () => DetachedRetailNicknameEditor.Preview(snapshot, 0, 0, "BAD\uFFFD"), "replacement-glyph nickname");
Refuses<InvalidOperationException>(
    () => DetachedRetailNicknameEditor.Preview(snapshot, -1, 0, "PARTYONE"), "party no-op");
Refuses<InvalidOperationException>(
    () => DetachedRetailNicknameEditor.Preview(snapshot, -1, 1, "TEST"), "empty party slot");
foreach (var (box, slot) in new (int Box, int Slot)[]
{
    (-2, 0), (-1, -1), (-1, 6), (0, -1),
    (0, save.BoxSlotCount), (save.BoxCount, 0),
})
{
    Refuses<ArgumentOutOfRangeException>(
        () => DetachedRetailNicknameEditor.Preview(snapshot, box, slot, "TEST"),
        $"out-of-bounds {box}/{slot}");
}

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
