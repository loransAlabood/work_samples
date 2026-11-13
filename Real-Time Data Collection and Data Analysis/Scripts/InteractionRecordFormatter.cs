using System;

/// <summary>Pure formatting and event-label rules shared by the interaction logger and tests.</summary>
public static class InteractionRecordFormatter
{
    /// <summary>
    /// Encodes one CSV field, including embedded quotes, line breaks, and commas.
    /// The payload occupies the second field of the local recording files.
    /// </summary>
    public static string EscapeCsv(string value)
    {
        return "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
    }

    /// <summary>
    /// Splits labels such as "Snap - Correct" or "Snap:Incorrect" into an action and outcome.
    /// Unknown snap formats are kept intact rather than silently classified as failures.
    /// </summary>
    public static void Classify(string objectName, string eventType, string expectedLetter,
        out string action, out string correctness)
    {
        action = (eventType ?? string.Empty).Trim();
        correctness = "NA";
        if (action.Length == 0) return;

        if (action.StartsWith("Snap", StringComparison.OrdinalIgnoreCase))
        {
            string suffix = action.Substring(4).Trim(' ', ':', '-', '_');
            action = "Snap";
            if (suffix.Equals("Correct", StringComparison.OrdinalIgnoreCase)) correctness = "Correct";
            else if (suffix.Equals("Incorrect", StringComparison.OrdinalIgnoreCase)) correctness = "Incorrect";
            return;
        }

        if (action.Equals("Release", StringComparison.OrdinalIgnoreCase))
        {
            action = "Release";
            return;
        }
        if (action.Equals("Grab", StringComparison.OrdinalIgnoreCase))
        {
            action = "Grab";
            correctness = string.Equals(objectName, expectedLetter, StringComparison.OrdinalIgnoreCase)
                ? "Correct" : "Incorrect";
        }
    }
}
