using UnityEngine;

public class JudgeSystem : SceneSingleton<JudgeSystem>
{
    public double perfectWindowMs = 50.0;
    public double goodWindowMs = 100.0;
    public double AudioOffsetMs { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        AudioOffsetMs = PlayerPrefs.GetFloat("audioOffsetMs", 0f);
    }

    /// <summary>오디오 오프셋 보정 후 타이밍 오차를 계산해 Perfect/Good/Miss를 반환. DefenseTurn에서 호출.</summary>
    public Judgment Judge(double inputTime, double judgeTime)
    {
        double adj = System.Math.Abs(CalcOffsetMs(inputTime, judgeTime));
        if (adj <= perfectWindowMs) return Judgment.PERFECT;
        if (adj <= goodWindowMs) return Judgment.GOOD;
        return Judgment.MISS;
    }

    /// <summary>AudioOffsetMs를 적용한 조정 타이밍 오차(ms)를 반환. DefenseTurn, JudgeSystem에서 호출.</summary>
    public double CalcOffsetMs(double inputTime, double judgeTime)
    {
        return (inputTime - judgeTime) * 1000.0 - AudioOffsetMs;
    }

    /// <summary>audioOffsetMs를 PlayerPrefs에 저장하고 즉시 적용. HUD에서 호출.</summary>
    public void SetAudioOffset(double offsetMs)
    {
        AudioOffsetMs = offsetMs;
        PlayerPrefs.SetFloat("audioOffsetMs", (float)offsetMs);
    }
}
