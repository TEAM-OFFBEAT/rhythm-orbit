using UnityEngine;

public class JudgeSystem : SceneSingleton<JudgeSystem>
{
    [SerializeField] private double perfectWindowMs = 50.0;
    [SerializeField] private double goodWindowMs = 100.0;
    public double KeyInputOffsetMs { get; private set; } //키 입력 오프셋
    public double AudioOffsetMs { get; private set; } //오디오 출력 오프셋
    protected override void Awake()
    {
        base.Awake();
        KeyInputOffsetMs = PlayerPrefs.GetFloat("keyInputOffsetMs", 0f);
        AudioOffsetMs = PlayerPrefs.GetFloat("audioOffsetMs", 0f);
    }

    /// <summary>
    /// 입력 오프셋 보정 후 타이밍 오차를 계산해 Perfect/Good/Miss를 반환.
    /// </summary>
    public Judgment Judge(double inputTime, double judgeTime)
    {
        double adj = System.Math.Abs(CalcOffsetMs(inputTime, judgeTime));
        if (adj <= perfectWindowMs) return Judgment.PERFECT;
        if (adj <= goodWindowMs) return Judgment.GOOD;
        return Judgment.MISS;
    }

    /// <summary>
    /// KeyInputOffsetMs를 적용한 타이밍 오차(ms)를 반환.
    /// </summary>
    public double CalcOffsetMs(double inputTime, double judgeTime)
    {
        return (inputTime - judgeTime) * 1000.0 - KeyInputOffsetMs;
    }

    /// <summary>
    /// KeyInputOffsetMs를 PlayerPrefs에 저장하고 즉시 적용.
    /// </summary>
    public void SetKeyInputOffset(double offsetMs)
    {
        KeyInputOffsetMs = offsetMs;
        PlayerPrefs.SetFloat("keyInputOffsetMs", (float)offsetMs);
    }

    /// <summary>
    /// AudioOffsetMs를 PlayerPrefs에 저장하고 즉시 적용.
    /// </summary>
    public void SetAudioOffset(double offsetMs)
    {
        AudioOffsetMs = offsetMs;
        PlayerPrefs.SetFloat("audioOffsetMs", (float)offsetMs);
    }
}
