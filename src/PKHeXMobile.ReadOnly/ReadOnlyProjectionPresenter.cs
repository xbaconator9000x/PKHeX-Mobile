using System.Text;

namespace PKHeXMobile.ReadOnly;

/// <summary>
/// Pure, Android-independent text projection for one selected box at a time.
/// Never retains a mutable PKHeX SaveFile or any source stream. No save editing.
/// The UI must not log or persist the private strings returned by this class.
/// </summary>
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

    /// <summary>Render only the requested box, not all 64 x 64 potential slots.</summary>
    public static string FormatBox(SaveReadOnlyResult save, int selectedIndex)
    {
        ArgumentNullException.ThrowIfNull(save);
        if ((uint)selectedIndex >= (uint)save.Boxes.Count)
            throw new ArgumentOutOfRangeException(nameof(selectedIndex));
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

    // Prevent control characters/newlines in untrusted save strings from
    // impersonating UI labels or generating unbounded multiline content.
    private static string Clean(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "(empty)";
        var text = new StringBuilder();
        foreach (var c in value)
        {
            if (text.Length >= 80)
            {
                text.Append('…');
                break;
            }
            text.Append(char.IsControl(c) ? ' ' : c);
        }
        return text.ToString();
    }
}
