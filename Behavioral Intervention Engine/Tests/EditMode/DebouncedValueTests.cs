using NUnit.Framework;

public class DebouncedValueTests
{
    [Test]
    public void AShortGlanceDoesNotChangeTheConfirmedTarget()
    {
        var state = new DebouncedValue<string>(null, 3);
        Assert.IsFalse(state.Observe("A"));
        Assert.IsFalse(state.Observe("A"));
        Assert.IsNull(state.Value);
        Assert.IsTrue(state.Observe("A"));
        Assert.AreEqual("A", state.Value);
    }

    [Test]
    public void SwitchingCandidatesRestartsTheCounter()
    {
        var state = new DebouncedValue<string>("A", 3);
        state.Observe("B");
        state.Observe("C");
        state.Observe("C");
        Assert.AreEqual("A", state.Value);
        Assert.IsTrue(state.Observe("C"));
        Assert.AreEqual("C", state.Value);
    }

    [Test]
    public void ReturningToConfirmedValueCancelsPendingTransition()
    {
        var state = new DebouncedValue<string>("A", 3);
        state.Observe(null);
        state.Observe(null);
        state.Observe("A");
        Assert.IsFalse(state.Observe(null));
        Assert.AreEqual("A", state.Value);
    }

    [Test]
    public void ResetClearsPendingSamples()
    {
        var state = new DebouncedValue<string>("A", 3);
        state.Observe("B");
        state.Observe("B");
        state.Reset(null);
        Assert.IsFalse(state.Observe("B"));
        Assert.IsNull(state.Value);
    }

    [Test]
    public void StableBooleanOnlyReportsEdges()
    {
        var state = new DebouncedValue<bool>(false, 2);
        Assert.IsFalse(state.Observe(true));
        Assert.IsTrue(state.Observe(true));
        Assert.IsFalse(state.Observe(true));
        Assert.IsFalse(state.Observe(false));
        Assert.IsTrue(state.Observe(false));
    }
}
