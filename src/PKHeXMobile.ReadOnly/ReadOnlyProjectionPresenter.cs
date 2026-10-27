using System.Text;
using System.Globalization;

namespace PKHeXMobile.ReadOnly;

/// <summary>
/// Pure, Android-independent text projection for one selected box at a time.
/// Never retains a mutable PKHeX SaveFile or any source stream. No save editing.
/// The UI must not log or persist the private strings returned by this class.
/// </summary>
/// <summary>One stable source slot; an empty slot is never silently compacted.</summary>
public sealed record BoxCell(int SlotIndex, PokemonSlot? Pokemon);

public static class ReadOnlyProjectionPresenter
{
    public static string FormatTrainer(SaveReadOnlyResult save, bool sourceIdentityConfirmed)
    {
        ArgumentNullException.ThrowIfNull(save);
        var text = new StringBuilder();
        text.AppendLine("Experimental read-only projection; no game format is certified.");
        text.AppendLine("Document ID hint stable: " +
            (sourceIdentityConfirmed ? "yes (not a provider revision)" : "unavailable"));
        text.Append("Core type: ").AppendLine(Clean(save.CoreSaveType));
        text.Append("Game: ").Append(Clean(save.GameVersion))
            .Append(" | Generation: ").AppendLine(save.Generation.ToString());
        text.Append("Trainer: ").AppendLine(Clean(save.TrainerName));
        text.Append("Trainer ID: ").Append(save.TrainerId)
            .Append(" / ").AppendLine(save.SecretId.ToString());
        text.Append("Party slots: ").Append(save.PartyCount)
            .Append(" | Boxes: ").Append(save.BoxCount)
            .Append(" x ").AppendLine(save.BoxSlotCount.ToString());
        text.AppendLine("No editing, export, legality or ROM-hack compatibility is enabled.");
        return text.ToString();
    }

    public static string FormatParty(SaveReadOnlyResult save)
    {
        ArgumentNullException.ThrowIfNull(save);
        return FormatSlots("Party", save.Party, save.PartyCount);
    }

    public static IReadOnlyList<string> BoxLabels(SaveReadOnlyResult save)
    {
        ArgumentNullException.ThrowIfNull(save);
        return save.Boxes.Select(box =>
            "Box " + (box.BoxIndex + 1) + " (" + box.OccupiedSlots.Count + " occupied)")
            .ToArray();
    }

    /// <summary>
    /// Build a bounded, index-stable view including empty slots. Duplicate and
    /// out-of-range slot indices fail closed rather than creating phantom cells.
    /// </summary>
    public static IReadOnlyList<BoxCell> BoxCells(SaveReadOnlyResult save, int selectedIndex)
    {
        ArgumentNullException.ThrowIfNull(save);
        if ((uint)selectedIndex >= (uint)save.Boxes.Count)
            throw new ArgumentOutOfRangeException(nameof(selectedIndex));
        if (save.BoxSlotCount is < 0 or > 64)
            throw new InvalidDataException("Box capacity exceeds the read-only display limit.");

        var occupied = new PokemonSlot?[save.BoxSlotCount];
        foreach (var pokemon in save.Boxes[selectedIndex].OccupiedSlots)
        {
            if ((uint)pokemon.SlotIndex >= (uint)occupied.Length ||
                occupied[pokemon.SlotIndex] is not null)
                throw new InvalidDataException("Invalid or repeated source slot index.");
            occupied[pokemon.SlotIndex] = pokemon;
        }
        return Enumerable.Range(0, occupied.Length)
            .Select(index => new BoxCell(index, occupied[index])).ToArray();
    }

    /// <summary>Safe, read-only detail for a single original source slot.</summary>
    public static string FormatCellDetails(BoxCell cell)
    {
        ArgumentNullException.ThrowIfNull(cell);
        var pokemon = cell.Pokemon;
        if (pokemon is null)
            return "Slot " + (cell.SlotIndex + 1) + ": empty";
        var detail = "Slot " + (cell.SlotIndex + 1) + ": #" + pokemon.Species +
            " " + SanitizeLabel(pokemon.Nickname) + " Lv." + pokemon.Level;
        if (pokemon.IsEgg) detail += " (Egg)";
        if (pokemon.IsShiny) detail += " (Shiny)";
        return detail;
    }

    /// <summary>Render only the requested box, not all 64 x 64 potential slots.</summary>
    public static string FormatBox(SaveReadOnlyResult save, int selectedIndex)
    {
        ArgumentNullException.ThrowIfNull(save);
        if ((uint)selectedIndex >= (uint)save.Boxes.Count)
            throw new ArgumentOutOfRangeException(nameof(selectedIndex));
        _ = BoxCells(save, selectedIndex); // validate sparse source indices first
        var box = save.Boxes[selectedIndex];
        return FormatSlots("Box " + (box.BoxIndex + 1), box.OccupiedSlots, save.BoxSlotCount);
    }

    private static string FormatSlots(string label, IReadOnlyList<PokemonSlot> slots, int capacity)
    {
        var text = new StringBuilder();
        text.Append(label).Append(" — ").Append(slots.Count)
            .Append(" occupied / ").Append(capacity).AppendLine(" slots");
        if (slots.Count == 0)
            return text.AppendLine("No occupied slots.").ToString();
        foreach (var slot in slots)
        {
            text.Append("Slot ").Append(slot.SlotIndex + 1)
                .Append(": #").Append(slot.Species).Append(' ')
                .Append(Clean(slot.Nickname))
                .Append(" Lv.").Append(slot.Level);
            if (slot.IsEgg) text.Append(" (Egg)");
            if (slot.IsShiny) text.Append(" (Shiny)");
            text.AppendLine();
        }
        return text.ToString();
    }

    // Text elements are kept intact so truncation cannot split a surrogate pair
    // or combining sequence. Formatting controls (including bidi overrides and
    // isolates), newlines, malformed surrogates and unassigned code points are
    // replaced, not passed through to Android's labels or accessibility text.
    public static string SanitizeLabel(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "(empty)";
        const int MaxTextElements = 80;
        const int MaxUtf16Units = 160;
        var text = new StringBuilder();
        var elements = StringInfo.GetTextElementEnumerator(value);
        int count = 0;
        while (elements.MoveNext())
        {
            var element = (string)elements.Current!;
            if (count >= MaxTextElements || text.Length + element.Length > MaxUtf16Units)
            {
                text.Append('…');
                break;
            }
            bool unsafeElement = false;
            foreach (var rune in element.EnumerateRunes())
            {
                var category = Rune.GetUnicodeCategory(rune);
                if (category is UnicodeCategory.Control or UnicodeCategory.Format or
                    UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator or
                    UnicodeCategory.Surrogate or UnicodeCategory.OtherNotAssigned ||
                    rune.Value == 0xFFFD)
                {
                    unsafeElement = true;
                    break;
                }
            }
            text.Append(unsafeElement ? " " : element);
            count++;
        }
        return text.ToString();
    }

    private static string Clean(string? value) => SanitizeLabel(value);
}
