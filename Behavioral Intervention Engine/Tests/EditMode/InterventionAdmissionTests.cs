using NUnit.Framework;

public class InterventionAdmissionTests
{
    [TestCase(InterventionEngineStatus.Enabled, true)]
    [TestCase(InterventionEngineStatus.Resume, true)]
    [TestCase(InterventionEngineStatus.Paused, false)]
    [TestCase(InterventionEngineStatus.Disabled, false)]
    public void OnlyActiveEngineStatesAdmitInterventions(InterventionEngineStatus status, bool expected)
    {
        Assert.AreEqual(expected, InterventionAdmission.CanStart(status));
    }
}
