// Adapted from PKForge.Engine/LegalityChecks.cs at
// https://github.com/sofianeelhor/PKForge/blob/2df04430e532b78c247f45b936f3068d8f824987/src/PKForge.Engine/LegalityChecks.cs
// Copyright the PKForge contributors. GPL-3.0. See THIRD_PARTY.md.
// Adaptation: no PKForge fork or MAUI dependency; immutable bounded diagnostic results.
// This is NOT a hack-layout verifier or a save-write authorization.
using PKHeX.Core;

namespace PKHeXMobile.ReadOnly;

public sealed record LegalityTopic(string Name, string Verdict, IReadOnlyList<string> Reasons);

/// <summary>
/// PKForge's topic grouping adapted to the official pinned PKHeX.Core 26.10.18.
/// No suggestion, legalizer, slot setter, or mutation API is reachable here.
/// </summary>
public static class PortedLegalityChecks
{
    private const string Other = "Other";
    private const int MaxReasonsPerTopic = 6;
    private const int MaxReasonLength = 256;
    private static readonly (string Name, CheckIdentifier[] Ids)[] Topics =
    [
        ("Encounter", [CheckIdentifier.Encounter, CheckIdentifier.SlotType, CheckIdentifier.GameOrigin,
            CheckIdentifier.Fateful, CheckIdentifier.Egg, CheckIdentifier.Level, CheckIdentifier.Evolution]),
        ("PID / IVs", [CheckIdentifier.PID, CheckIdentifier.EC, CheckIdentifier.IVs,
            CheckIdentifier.EVs, CheckIdentifier.Shiny, CheckIdentifier.Nature, CheckIdentifier.Gender]),
        ("Moves", [CheckIdentifier.CurrentMove, CheckIdentifier.RelearnMove]),
        ("Ability", [CheckIdentifier.Ability]),
        ("Ball", [CheckIdentifier.Ball]),
        ("Ribbons", [CheckIdentifier.Ribbon, CheckIdentifier.RibbonMark]),
        ("Trainer", [CheckIdentifier.Trainer, CheckIdentifier.Language, CheckIdentifier.Handler,
            CheckIdentifier.Memory, CheckIdentifier.Geography]),
        ("Nickname", [CheckIdentifier.Nickname]),
    ];

    private readonly record struct Finding(string Topic, Severity Severity, string? Reason);

    /// <summary>Deterministic grouping of Core check codes, independently testable without a save.</summary>
    public static IReadOnlyList<LegalityTopic> GroupResults(IEnumerable<CheckResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        var findings = new List<Finding>();
        foreach (var check in results)
            findings.Add(new Finding(TopicOf(check.Identifier), check.Judgement,
                check.Judgement == Severity.Valid ? null : check.Result.ToString()));
        return Summarize(findings);
    }

    /// <summary>Include the move and relearn arrays, which Core's Valid verdict also checks.</summary>
    public static IReadOnlyList<LegalityTopic> Group(LegalityAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        var context = LegalityLocalizationContext.Create(analysis);
        var findings = new List<Finding>();
        foreach (var check in analysis.Results)
        {
            string? reason = null;
            if (check.Judgement != Severity.Valid)
            {
                try { reason = context.Humanize(check); }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    reason = check.Result.ToString();
                }
            }
            findings.Add(new Finding(TopicOf(check.Identifier), check.Judgement, reason));
        }

        for (int i = 0; i < analysis.Info.Moves.Length; i++)
        {
            var move = analysis.Info.Moves[i];
            if (move.IsParsed && !move.Valid)
                findings.Add(new Finding("Moves", Severity.Invalid,
                    "Move " + (i + 1) + ": " + move.Summary(context)));
        }
        for (int i = 0; i < analysis.Info.Relearn.Length; i++)
        {
            var move = analysis.Info.Relearn[i];
            if (move.IsParsed && !move.Valid)
                findings.Add(new Finding("Moves", Severity.Invalid,
                    "Relearn " + (i + 1) + ": " + move.Summary(context)));
        }
        return Summarize(findings);
    }

    private static IReadOnlyList<LegalityTopic> Summarize(List<Finding> findings)
    {
        var topics = new List<LegalityTopic>();
        foreach (var (name, _) in Topics)
            topics.Add(One(name));
        if (findings.Any(f => f.Topic == Other && f.Severity != Severity.Valid))
            topics.Add(One(Other));
        return topics.AsReadOnly();

        LegalityTopic One(string name)
        {
            var matches = findings.Where(f => f.Topic == name).ToArray();
            var worst = matches.Any(f => f.Severity == Severity.Invalid) ? "Invalid"
                : matches.Any(f => f.Severity == Severity.Fishy) ? "Fishy" : "Valid";
            var reasons = matches.Where(f => f.Reason is not null)
                .Select(f => Bounded(f.Reason!))
                .Distinct(StringComparer.Ordinal)
                .Take(MaxReasonsPerTopic).ToList();
            return new LegalityTopic(name, worst, reasons.AsReadOnly());
        }
    }

    private static string TopicOf(CheckIdentifier identifier)
    {
        foreach (var (name, ids) in Topics)
            if (Array.IndexOf(ids, identifier) >= 0) return name;
        return Other;
    }

    private static string Bounded(string value)
    {
        // The presentation layer also filters controls and bidi formatting.
        var firstLine = value.Replace('\r', ' ').Replace('\n', ' ');
        return firstLine.Length <= MaxReasonLength ? firstLine : firstLine[..MaxReasonLength];
    }
}
