using System;
using UnityEngine;

/// <summary>Records a task interaction along with the spelling context at the time it occurred.</summary>
[Serializable]
public sealed class InteractionRecord
{
    public string type;
    public string objectName;
    public string timeUtc;
    public long timestampMs;
    public int phase;
    public string word;
    public string targetLetter;
    public string correctness;
}

/// <summary>One tracked transform. A missing joint is retained as an explicit unavailable sample.</summary>
[Serializable]
public sealed class TrackedPose
{
    public string name;
    public bool available;
    public Vector3 position;
    public Quaternion rotation;
}

/// <summary>A group of poses captured at a single fixed-timestep observation.</summary>
[Serializable]
public sealed class PoseFrame
{
    public string timeUtc;
    public long timestampMs;
    public TrackedPose[] poses;
}

/// <summary>Stable representation of tracked letter, base, and platform transforms.</summary>
[Serializable]
public sealed class ObjectPoseFrame
{
    public string timeUtc;
    public long timestampMs;
    public TrackedPose[] objects;
}

/// <summary>Named event entry for local or remote diagnostic logging.</summary>
[Serializable]
public sealed class SessionLogRecord
{
    public string timeUtc;
    public long timestampMs;
    public string message;
}
