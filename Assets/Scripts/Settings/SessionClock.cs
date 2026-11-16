using UnityEngine;

/// <summary>Restorable timestamps for the existing simulation; delta time and speed remain Unity's own.</summary>
public static class SessionClock
{
    private static float offset;
    public static float Time => UnityEngine.Time.time + offset;
    public static void Restore(float saved) { offset = saved - UnityEngine.Time.time; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void ResetClock() { offset = 0; }
}
