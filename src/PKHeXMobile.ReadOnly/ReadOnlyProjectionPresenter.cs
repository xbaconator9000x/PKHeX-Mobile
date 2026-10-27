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

    /// <summary>Index-stable party view, including empty positions.</summary>
    public static IReadOnlyList<BoxCell> PartyCells(SaveReadOnlyResult save)
    {
        ArgumentNullException.ThrowIfNull(save);
        if (save.PartyCount is < 0 or > 6)
            throw new InvalidDataException("Party capacity exceeds the read-only display limit.");
        var slots = new PokemonSlot?[save.PartyCount];
        foreach (var pokemon in save.Party)
        {
            if ((uint)pokemon.SlotIndex >= (uint)slots.Length ||
                slots[pokemon.SlotIndex] is not null)
                throw new InvalidDataException("Invalid or repeated party slot index.");
            slots[pokemon.SlotIndex] = pokemon;
        }
        return Enumerable.Range(0, slots.Length)
            .Select(index => new BoxCell(index, slots[index])).ToArray();
    }

    /// <summary>Compact, safe label for the Android party slot selector.</summary>
    public static string FormatPartySlotLabel(BoxCell cell)
    {
        ArgumentNullException.ThrowIfNull(cell);
        return cell.Pokemon is { } pokemon
            ? "Party " + (cell.SlotIndex + 1) + ": #" + pokemon.Species + " " +
                SanitizeLabel(pokemon.Nickname)
            : "Party " + (cell.SlotIndex + 1) + ": empty";
    }

    /// <summary>Safe, read-only detail for a single original source slot.</summary>
    public static string FormatCellDetails(BoxCell cell) => FormatSlotDetails(cell, "Slot");

    public static string FormatPartySlotDetails(BoxCell cell) =>
        FormatSlotDetails(cell, "Party slot");

    private static string FormatSlotDetails(BoxCell cell, string label)
    {
        ArgumentNullException.ThrowIfNull(cell);
        var pokemon = cell.Pokemon;
        if (pokemon is null)
            return label + " " + (cell.SlotIndex + 1) + ": empty";
        var text = new StringBuilder();
        text.Append(label).Append(' ').Append(cell.SlotIndex + 1)
            .Append(": #").Append(pokemon.Species).Append(' ')
            .Append(SanitizeLabel(pokemon.Nickname)).Append(" Lv.").Append(pokemon.Level);
        if (pokemon.IsEgg) text.Append(" (Egg)");
        if (pokemon.IsShiny) text.Append(" (Shiny)");
        if (pokemon.Details is { } details)
        {
            if (details.MechanicsGeneration is 1 or 2)
            {
                // Core exposes placeholder nature/ability and duplicated Special
                // properties for Game Boy entities. Do not imply modern mechanics.
                text.AppendLine().Append("Nature: N/A (introduced in Gen 3)");
                text.Append(" | Ability: N/A (introduced in Gen 3)");
                text.Append(" | Held item: ");
                if (details.MechanicsGeneration == 1)
                    text.Append("N/A (Gen 1)");
                else
                    text.Append("Core ID ").Append(details.HeldItemId);
                text.AppendLine().Append("Gender: ");
                if (details.MechanicsGeneration == 1)
                    text.Append("N/A (Gen 1)");
                else
                    text.Append("Core code ").Append(details.Gender);
                text.Append(" | Form (Core): ").Append(details.Form);
            }
            else
            {
                text.AppendLine().Append("Nature (Core): ").Append(SanitizeLabel(details.Nature));
                text.Append(" | Ability ID: ").Append(details.AbilityId);
                text.Append(" | Held item ID: ").Append(details.HeldItemId);
                text.AppendLine().Append("Gender code: ").Append(details.Gender);
                text.Append(" | Form: ").Append(details.Form);
            }
            text.AppendLine().Append("Move IDs: ")
                .Append(details.Moves.First).Append(", ")
                .Append(details.Moves.Second).Append(", ")
                .Append(details.Moves.Third).Append(", ")
                .Append(details.Moves.Fourth);
            if (details.MechanicsGeneration is 1 or 2)
            {
                text.AppendLine().Append("DVs (0-15; HP derived, Special shared): ")
                    .Append(FormatClassicSpread(details.IndividualValues));
                text.AppendLine().Append("Stat experience (0-65535; Special shared): ")
                    .Append(FormatClassicSpread(details.EffortValues));
            }
            else
            {
                text.AppendLine().Append("Core IV/DV values (game-dependent): ")
                    .Append(FormatSpread(details.IndividualValues));
                text.AppendLine().Append("Core EV/stat-exp values (game-dependent): ")
                    .Append(FormatSpread(details.EffortValues));
            }
            AppendTypedAwards(text, details.Awards);
            text.AppendLine().Append("Values are provisional; legality is not evaluated.");
        }
        return text.ToString();
    }

    private static void AppendTypedAwards(StringBuilder text, PokemonAwardsReadOnly awards)
    {
        // A typed subset only; missing interfaces do not mean missing ribbons.
        if (awards.Gen9Flags.Count != 0)
        {
            var owned = awards.Gen9Flags.Where(flag => flag.Owned).Select(flag => flag.Name).ToArray();
            text.AppendLine().Append("Gen 9 typed ribbon/mark subset (not full inventory or legality): ")
                .Append(owned.Length).Append('/').Append(awards.Gen9Flags.Count).Append(" owned");
            if (owned.Length != 0)
                text.Append(" [").Append(string.Join(", ", owned)).Append(']');
        }
        if (awards.ContestMemoryCount is { } contest && awards.BattleMemoryCount is { } battle)
            text.AppendLine().Append("Memory ribbon counts (raw): contest ").Append(contest)
                .Append(", battle ").Append(battle)
                .Append("; presence flags ").Append(awards.HasContestMemoryRibbon)
                .Append('/').Append(awards.HasBattleMemoryRibbon);
        if (awards.AffixedRibbon is { } affixed)
            text.AppendLine().Append("Affixed ribbon index (raw; -1 means none): ").Append(affixed);
        if (awards.OriginalTrainerMemory is { } ot)
            AppendMemory(text, "OT memory (raw)", ot);
        if (awards.HandlingTrainerMemory is { } ht)
            AppendMemory(text, "HT memory (raw)", ht);
    }

    private static void AppendMemory(StringBuilder text, string label, PokemonMemoryReadOnly memory) =>
        text.AppendLine().Append(label).Append(": ID ").Append(memory.Memory)
            .Append(", intensity ").Append(memory.Intensity)
            .Append(", feeling ").Append(memory.Feeling)
            .Append(", variable ").Append(memory.Variable);

    private static string FormatClassicSpread(PokemonStatSpread s) =>
        "HP " + s.HP + ", Atk " + s.Attack + ", Def " + s.Defense +
        ", Special " + s.SpecialAttack + ", Spe " + s.Speed;

    private static string FormatSpread(PokemonStatSpread s) =>
        "HP " + s.HP + ", Atk " + s.Attack + ", Def " + s.Defense +
        ", SpA " + s.SpecialAttack + ", SpD " + s.SpecialDefense +
        ", Spe " + s.Speed;

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
