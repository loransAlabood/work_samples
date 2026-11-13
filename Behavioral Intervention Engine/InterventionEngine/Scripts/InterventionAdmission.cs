/// <summary>Admission rule for new intervention attempts and deferred retries.</summary>
public static class InterventionAdmission
{
    // Resume is treated as an active state for compatibility with the existing control layer.
    public static bool CanStart(InterventionEngineStatus status) =>
        status == InterventionEngineStatus.Enabled || status == InterventionEngineStatus.Resume;
}
