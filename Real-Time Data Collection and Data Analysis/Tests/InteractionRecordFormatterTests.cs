using NUnit.Framework;

public class InteractionRecordFormatterTests
{
    [Test]
    public void EscapesQuotesCommasAndNewlines()
    {
        Assert.AreEqual("\"a,\"\"b\"\"\\nc\"", InteractionRecordFormatter.EscapeCsv("a,\"b\"\nc"));
    }

    [Test]
    public void ReleaseDoesNotClaimCorrectness()
    {
        InteractionRecordFormatter.Classify("B", "Release", "B", out string action, out string result);
        Assert.AreEqual("Release", action);
        Assert.AreEqual("NA", result);
    }

    [Test]
    public void GrabComparesAgainstExpectedLetter()
    {
        InteractionRecordFormatter.Classify("B", "Grab", "B", out _, out string result);
        Assert.AreEqual("Correct", result);
    }

    [Test]
    public void SnapWithUnknownSuffixIsNotAssumedIncorrect()
    {
        InteractionRecordFormatter.Classify("B", "SnapCompleted", "B", out string action, out string result);
        Assert.AreEqual("Snap", action);
        Assert.AreEqual("NA", result);
    }

    [Test]
    public void SnapParsesExplicitOutcome()
    {
        InteractionRecordFormatter.Classify("B", "Snap:Incorrect", "B", out string action, out string result);
        Assert.AreEqual("Snap", action);
        Assert.AreEqual("Incorrect", result);
    }
}
