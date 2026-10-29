namespace PKHeXMobile.Verify;

/// <summary>
/// Distinguishes a positive desktop-matched projection from an explicitly
/// expected parser refusal. A refusal is not proof of why the parser refused
/// or of the safety of a related ROM-hack layout.
/// </summary>
public enum FixtureExpectedOutcome { Recognized, Refused }

public static class FixtureOutcomePolicy
{
    public static bool TryResolve(
        int schemaVersion, string? declaredOutcome, bool hasExpectedProjection,
        out FixtureExpectedOutcome outcome)
    {
        outcome = FixtureExpectedOutcome.Recognized;
        if (schemaVersion < 17)
            return declaredOutcome is null && hasExpectedProjection;

        if (declaredOutcome == "recognized" && hasExpectedProjection)
            return true;
        if (declaredOutcome == "refused" && !hasExpectedProjection)
        {
            outcome = FixtureExpectedOutcome.Refused;
            return true;
        }
        return false;
    }

    public static bool RefusalMatches(bool isRecognized, string? rejectionReason) =>
        !isRecognized && !string.IsNullOrWhiteSpace(rejectionReason);
}
