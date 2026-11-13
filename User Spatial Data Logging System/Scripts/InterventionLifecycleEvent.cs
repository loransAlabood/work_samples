using System;

/// <summary>
/// Snapshot of one intervention lifecycle transition. The event logger serializes fields
/// independently, so commas in Action or Comments do not alter the CSV schema.
/// </summary>
public class InterventionLifecycleEvent
{
    public DateTime Timestamp;
    public UserScenarioType Scenario;
    public int EscalationCount;
    public InterventionStage Stage;
    public InterventionOutcome Outcome;
    public string Action;
    public string Comments;

    /// <summary>Human-readable event summary; use CsvFormat for CSV serialization.</summary>
    public override string ToString() =>
        $"{Timestamp:O} | {Scenario} | escalation={EscalationCount} | {Stage} | {Outcome} | {Action} | {Comments}";
}
