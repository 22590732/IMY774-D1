using System;
using UnityEngine;

/// <summary>
/// Tutorial gate: the player can't spawn balls until the ball container has been picked up.
/// Call BallSpawnGate.Unlock() from the container's pickup logic (also the place to start the voiceover).
/// </summary>
public static class BallSpawnGate
{
    public static bool IsUnlocked { get; private set; }
    public static event Action Unlocked;

    public static void Unlock()
    {
        if (IsUnlocked) return;
        IsUnlocked = true;
        Unlocked?.Invoke();
    }

    public static void Lock()
    {
        IsUnlocked = false;
    }

    // Statics survive between Play sessions when Domain Reload is disabled, so reset explicitly.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState()
    {
        IsUnlocked = false;
        Unlocked = null;
    }
}
