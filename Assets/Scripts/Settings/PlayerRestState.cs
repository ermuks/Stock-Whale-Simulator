using System;

/// <summary>Game-hour accounting, independent of rendering and the wall clock.</summary>
[Serializable]
public sealed class PlayerRestState
{
    public double fatigue, lastGameMinute = -1;
    public int sleeplessNights;
    public bool dead;
    public void Advance(double minute, double fatiguePerHour)
    {
        if (lastGameMinute >= 0 && !dead)
        { fatigue = Math.Clamp(fatigue + Math.Max(0, minute - lastGameMinute) / 60d * fatiguePerHour, 0d, 100d); }
        lastGameMinute = minute;
    }
    public void Sleep(double hours, double wakingMinute)
    {
        if (hours <= 0 || dead) { return; }
        fatigue = Math.Clamp(fatigue - hours * 15d, 0d, 100d);
        sleeplessNights = 0; lastGameMinute = wakingMinute;
    }
    /// <summary>밤을 새웠다. 나흘째에는 죽는 대신 피로가 끝까지 차서 다음 날 쓰러진다.</summary>
    public void MissNight()
    { if (++sleeplessNights >= 4) { fatigue = 100d; } }
}
