using System;
using System.IO;
using NUnit.Framework;

/// <summary>Unity EditMode tests for the file-writer queue and start/stop flushing.</summary>
public class BufferedCsvWriterTests
{
    private string path;

    [SetUp]
    public void SetUp() => path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "csv_test_" + Guid.NewGuid().ToString("N") + ".csv");

    [TearDown]
    public void TearDown() { if (File.Exists(path)) File.Delete(path); }

    [Test]
    public void WritesHeaderAndRowsInOrder()
    {
        var writer = new BufferedCsvWriter(path, "a,b");
        writer.Enqueue("one,1");
        writer.Enqueue("two,2");
        Assert.IsTrue(writer.Flush());
        CollectionAssert.AreEqual(new[] { "a,b", "one,1", "two,2" }, File.ReadAllLines(path));
        Assert.AreEqual(0, writer.PendingCount);
    }

    [Test]
    public void RepeatedFlushDoesNotRepeatRecords()
    {
        var writer = new BufferedCsvWriter(path, "a");
        writer.Enqueue("first");
        Assert.IsTrue(writer.Flush());
        Assert.IsTrue(writer.Flush());
        CollectionAssert.AreEqual(new[] { "a", "first" }, File.ReadAllLines(path));
    }

    [Test]
    public void DoesNotOverwriteExistingSessionFile()
    {
        File.WriteAllText(path, "original");
        Assert.Throws<IOException>(() => new BufferedCsvWriter(path, "header"));
        Assert.AreEqual("original", File.ReadAllText(path));
    }
}
