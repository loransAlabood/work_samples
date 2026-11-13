using System.Collections.Generic;

/// <summary>
/// Holds the last confirmed value until a different value appears in consecutive samples.
/// A return to the confirmed value cancels a pending transition; useful for noisy gaze targets.
/// </summary>
public sealed class DebouncedValue<T>
{
    private readonly int requiredSamples;
    private readonly IEqualityComparer<T> comparer;
    private T candidate;
    private int candidateSamples;

    /// <summary>Last confirmed value; candidate observations are not exposed until committed.</summary>
    public T Value { get; private set; }

    /// <param name="initialValue">Value used before the first confirmed transition.</param>
    /// <param name="requiredSamples">Consecutive observations required to commit a change (minimum 1).</param>
    /// <param name="comparer">Optional comparison rule for deciding whether observations match.</param>
    public DebouncedValue(T initialValue, int requiredSamples, IEqualityComparer<T> comparer = null)
    {
        this.requiredSamples = requiredSamples < 1 ? 1 : requiredSamples;
        this.comparer = comparer ?? EqualityComparer<T>.Default;
        Value = initialValue;
        candidate = initialValue;
    }

    /// <summary>Processes one observation. Returns true only when Value changes.</summary>
    /// <remarks>Samples matching Value cancel any pending candidate; a different candidate starts a new count.</remarks>
    public bool Observe(T sample)
    {
        if (comparer.Equals(sample, Value))
        {
            candidate = Value;
            candidateSamples = 0;
            return false;
        }

        if (!comparer.Equals(sample, candidate))
        {
            candidate = sample;
            candidateSamples = 1;
        }
        else
        {
            candidateSamples++;
        }

        if (candidateSamples < requiredSamples)
            return false;

        Value = candidate;
        candidateSamples = 0;
        return true;
    }

    /// <summary>Immediately replaces the confirmed value and discards pending observations.</summary>
    public void Reset(T value)
    {
        Value = value;
        candidate = value;
        candidateSamples = 0;
    }
}
