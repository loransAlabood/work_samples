using NUnit.Framework;

public class InterventionPolicyResolverTests
{
    private InterventionPolicyResolver resolver;

    [SetUp]
    public void Setup() => resolver = new InterventionPolicyResolver();

    [Test]
    public void InitialTimeoutRepeatsTheInstruction()
    {
        Intervention result = resolver.Resolve(UserScenarioType.DidNotMoveInTime, 0,
            default, default, TaskContextType.WaitingForSelection);
        Assert.AreEqual(InterventionNature.Verbal, result.Nature);
        Assert.AreEqual(VerbalInterventionType.RepeatInstruction, result.VerbalInterventionType);
    }

    [Test]
    public void RepeatedMissedPortalsSelectsRetraining()
    {
        Intervention result = resolver.Resolve(UserScenarioType.Missed_2_Or_3_Portals_For_2_Round, 0,
            default, default, TaskContextType.WaitingForSelection);
        Assert.AreEqual(InterventionNature.ReTraining, result.Nature);
        Assert.AreEqual(ReTrainingInterventionType.GazeTraining, result.ReTrainingInterventionType);
    }

    [Test]
    public void OneRoundWithMissedPortalsSelectsVerbalReminder()
    {
        Intervention result = resolver.Resolve(UserScenarioType.Missed_2_Or_3_PortalsThisRound, 0,
            default, default, TaskContextType.WaitingForSelection);
        Assert.AreEqual(InterventionNature.Verbal, result.Nature);
        Assert.AreEqual(VerbalInterventionType.RemindScanAllPortals, result.VerbalInterventionType);
    }
}
