using System.Buffers.Binary;
using PKHeX.Core;
using PKHeXMobile.Domain;
using PKHeXMobile.ReadOnly;

static void Must(bool ok, string why) { if (!ok) throw new Exception(why); }
static void Reject<T>(Action f) where T : Exception
{
    try { f(); } catch (T) { return; }
    throw new Exception($"Expected {typeof(T).Name}");
}
static List<string> Diff(Gen5WorldReadOnly? actual, ExpectedGen5WorldReadOnly? expected)
{
    var errors = new List<string>();
    Gen5WorldProjectionVerifier.Compare(actual, expected, errors);
    return errors;
}

var save = new SAV5B2W2();
var medalBytes = save.Medals.Data;
var keyBytes = save.Keys.Data;
Must(medalBytes.Length >= Gen5WorldReadOnly.MedalBlockBytes &&
    keyBytes.Length >= Gen5WorldReadOnly.KeyBlockBytes, "Pinned Core B2W2 block layout drifted.");
// Raw packed invalid date (month 0), upper-nibble sentinel, unread bit and reserved byte.
BinaryPrimitives.WriteUInt16LittleEndian(medalBytes[..2], 0xF800);
medalBytes[2] = 0xFC;
medalBytes[3] = 0xE7;
medalBytes[254 * 4 + 2] = 0x8F;
medalBytes[0x3FC] = 0xFE;
medalBytes[0x3FD] = 0xFF;
medalBytes[0x3FE] = 0x7F;
medalBytes[0x3FF] = 0xF0;
medalBytes[0x400] = 0xFD;
medalBytes[0x400 + HabitatList5.SIZE - 1] = 0xEF;
BinaryPrimitives.WriteUInt32LittleEndian(keyBytes.Slice(0x28, 4), 0xDEADBEEF);
BinaryPrimitives.WriteUInt32LittleEndian(keyBytes.Slice(0x3C + 16, 4), 0x80000001);
BinaryPrimitives.WriteUInt32LittleEndian(keyBytes.Slice(0x50, 4), 0xFFFFFFFF);
BinaryPrimitives.WriteUInt32LittleEndian(keyBytes.Slice(0x54, 4), 0x12345678);
BinaryPrimitives.WriteUInt32LittleEndian(keyBytes.Slice(0x58, 4), 0x87654321);
BinaryPrimitives.WriteUInt32LittleEndian(keyBytes.Slice(0x5C, 4), 0xF0001234);
var originalMedals = medalBytes.ToArray();
var originalKeys = keyBytes.ToArray();

var view = Gen5WorldReadOnly.Project(save)!;
Must(view.Medals.Entries.Count == 255 && view.Medals.Entries[0].RawDate == 0xF800 &&
    view.Medals.Entries[0].RawState == 0xFC &&
    view.Medals.Entries[0].StateCode == 4 &&
    view.Medals.Entries[0].IsUnread &&
    !view.Medals.Entries[0].ValidDate &&
    view.Medals.Entries[0].ReservedByte == 0xE7 &&
    view.Medals.Entries[254].RawState == 0x8F &&
    view.Medals.PinnedMedalRaw == 0xFE &&
    view.Medals.RankRaw == 0xFF &&
    view.Medals.TutorialRaw == 0x7F &&
    view.Medals.UnusedRaw == 0xF0 &&
    view.Medals.HabitatRaw[0] == 0xFD &&
    view.Medals.HabitatRaw[^1] == 0xEF &&
    view.Keys.ObtainedWordsRaw[0] == 0xDEADBEEF &&
    view.Keys.UnlockedWordsRaw[4] == 0x80000001 &&
    view.Keys.DifficultyWordRaw == uint.MaxValue &&
    view.Keys.CityWordRaw == 0x12345678 &&
    view.Keys.ChamberWordRaw == 0x87654321 &&
    view.Keys.CryptoWordRaw == 0xF0001234,
    "B2W2 raw projection normalized packed bits, dates, habitats or XOR values.");
Must(medalBytes.SequenceEqual(originalMedals) && keyBytes.SequenceEqual(originalKeys),
    "Gen5 read-only inspection modified Core backing bytes.");
Must(!Gen5WorldReadOnly.IsValidMedalDate(0) &&
    !Gen5WorldReadOnly.IsValidMedalDate(0xF800) &&
    Gen5WorldReadOnly.IsValidMedalDate((ushort)(24 | (2 << 7) | (29 << 11))) &&
    !Gen5WorldReadOnly.IsValidMedalDate((ushort)(23 | (2 << 7) | (29 << 11))),
    "Packed Gen5 medal date validation failed leap year / invalid date cases.");

var expected = new ExpectedGen5WorldReadOnly(
    view.Medals with { Entries = view.Medals.Entries.ToArray(), HabitatRaw = view.Medals.HabitatRaw.ToArray() },
    view.Keys with { ObtainedWordsRaw = view.Keys.ObtainedWordsRaw.ToArray(),
                     UnlockedWordsRaw = view.Keys.UnlockedWordsRaw.ToArray() });
Must(Diff(view, expected).Count == 0, "Exact B2W2 synthetic parity failed.");
Must(Diff(view, expected with { Medals = expected.Medals with { RankRaw = 0 } })
    .Contains("Gen5World.Medals raw metadata mismatch"), "Rank change not detected.");
Must(Diff(view, expected with { Keys = expected.Keys with { CryptoWordRaw = 0 } })
    .Contains("Gen5World.Keys mismatch"), "XOR key change not detected.");
Must(Diff(view, expected with { Medals = expected.Medals with { HabitatRaw = [] } })
    .Contains("Gen5World invalid shape or raw fields"), "Truncated habitat accepted.");
Must(Diff(view, expected with { Keys = expected.Keys with { UnlockedWordsRaw = [0u] } })
    .Contains("Gen5World invalid shape or raw fields"), "Truncated key table accepted.");
Must(Diff(view, expected with { Medals = expected.Medals with { RawSha256 = "bad" } })
    .Contains("Gen5World invalid shape or raw fields"), "Malformed hash accepted.");
var forged = expected.Medals.Entries.ToArray();
forged[0] = forged[0] with { ValidDate = true };
Must(Diff(view, expected with { Medals = expected.Medals with { Entries = forged } })
    .Contains("Gen5World invalid shape or raw fields"), "Inconsistent date oracle accepted.");

Reject<InvalidDataException>(() => Gen5WorldReadOnly.ProjectMedals(new byte[0x497]));
Reject<InvalidDataException>(() => Gen5WorldReadOnly.ProjectKeys(new byte[0x5F]));
if (view.Medals.Entries is IList<Gen5MedalReadOnly> writable)
    Reject<NotSupportedException>(() => writable[0] = writable[1]);
if (view.Keys.ObtainedWordsRaw is IList<uint> keyWritable)
    Reject<NotSupportedException>(() => keyWritable[0] = 0);

medalBytes[2] ^= 0xFF;
keyBytes[0x5C] ^= 0xFF;
Must(view.Medals.Entries[0].RawState == 0xFC &&
    view.Keys.CryptoWordRaw == 0xF0001234 &&
    Gen5WorldReadOnly.Project(save)!.Keys.RawSha256 != view.Keys.RawSha256,
    "B2W2 projection retained mutable Core memory.");

// Reproduce pinned Core's no-op Medal5.State setter dropping reserved upper bits.
// This is a synthetic upstream defect reproduction, NOT a production editor.
var rawMedal = new byte[] { 0, 0, 0xFC, 0xE7 };
var coreMedal = new Medal5(rawMedal);
coreMedal.State = coreMedal.State;
Must(rawMedal[2] != 0xFC && view.Medals.Entries[0].RawState == 0xFC,
    "Core medal reserved-bit rewrite behavior changed; revisit guard.");

Must(Gen5WorldReadOnly.Project(new SAV5BW()) is null &&
    Gen5WorldReadOnly.Project(new SAV4HGSS()) is null,
    "B2W2-only data exposed on unrelated games.");
var sample = new SaveReadOnlyResult("SAV5B2W2", "B2", 5, "TEST", 0, 0,
    0, 0, 0, [], [], SaveInputSnapshot.FromBytes([1]).Fingerprint) { Gen5World = view };
var display = ReadOnlyProjectionPresenter.FormatTrainer(sample, false);
Must(display.Contains("B2W2 medals") && display.Contains("Key System") &&
    display.Contains("no edit authorization") && display.Contains("No editing, export"),
    "Gen5 presentation omitted diagnostics or implied write support.");
Console.WriteLine("Synthetic B2W2 medal, habitat and Key System raw projection/parity tests passed; pinned Core reserved-bit defect reproduced. No desktop/device certification.");
