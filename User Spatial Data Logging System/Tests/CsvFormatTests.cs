using NUnit.Framework;

/// <summary>Unity EditMode tests for the dependency-free CSV formatter.</summary>
public class CsvFormatTests
{
    [Test]
    public void QuotesCommaAndQuotes() =>
        Assert.AreEqual("\"Hello, \"\"world\"\"\"", CsvFormat.Field("Hello, \"world\""));

    [Test]
    public void QuotesNewline() =>
        Assert.AreEqual("\"line1\nline2\"", CsvFormat.Field("line1\nline2"));

    [Test]
    public void UsesInvariantDecimalSeparator() =>
        Assert.AreEqual("1.25", CsvFormat.Number(1.25f, "F2"));
}
