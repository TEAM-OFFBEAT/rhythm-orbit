using UnityEngine;

public class JudgeSystem : SceneSingleton<JudgeSystem>
{
    public const string KeyInputOffsetPrefsKey = "keyInputOffsetMs";
    public const string AudioOffsetPrefsKey = "audioOffsetMs";

    [SerializeField, Range(0f, 0.5f)] private float perfectWindowRatio = 0.20f;
    [SerializeField, Range(0f, 0.5f)] private float goodWindowRatio = 0.25f;
    [SerializeField, Min(0f)]
    private float noteActivationLeadTimeMs = 300f;

    public double KeyInputOffsetMs { get; private set; } // 키 입력 오프셋
    public double AudioOffsetMs { get; private set; }    // 오디오 출력 오프셋

    protected override void Awake()
    {
        base.Awake();

        KeyInputOffsetMs = PlayerPrefs.GetFloat(KeyInputOffsetPrefsKey, 0f);
        AudioOffsetMs = PlayerPrefs.GetFloat(AudioOffsetPrefsKey, 0f);
    }

    /// <summary>
    /// 입력 오프셋 보정 후 타이밍 오차를 현재 BPM 반박 기준 비율로 판정해 Perfect/Good/Miss를 반환한다.
    /// noteDurationSeconds는 RhythmClock.GetNoteDuration(subdivisions) 값을 전달한다.
    /// </summary>
    public Judgment Judge(double inputTime, double judgeTime, double noteDurationSeconds)
    {
        double adj = System.Math.Abs(CalcOffsetMs(inputTime, judgeTime));
        double noteDurationMs = noteDurationSeconds * 1000.0;

        if (adj <= noteDurationMs * perfectWindowRatio)
        {
            return Judgment.PERFECT;
        }

        if (adj <= GetGoodWindowSeconds(noteDurationSeconds) * 1000.0)
        {
            return Judgment.GOOD;
        }

        return Judgment.MISS;
    }

    /// <summary>
    /// KeyInputOffsetMs를 적용한 타이밍 오차(ms)를 반환한다.
    /// </summary>
    public double CalcOffsetMs(double inputTime, double judgeTime)
    {
        return (inputTime - judgeTime) * 1000.0 - KeyInputOffsetMs;
    }

    /// <summary>
    /// KeyInputOffsetMs를 PlayerPrefs에 저장하고 즉시 적용한다.
    /// </summary>
    public void SetKeyInputOffset(double offsetMs)
    {
        KeyInputOffsetMs = offsetMs;
        PlayerPrefs.SetFloat(KeyInputOffsetPrefsKey, (float)offsetMs);
    }

    /// <summary>
    /// AudioOffsetMs를 PlayerPrefs에 저장하고 즉시 적용한다.
    /// </summary>
    public void SetAudioOffset(double offsetMs)
    {
        AudioOffsetMs = offsetMs;
        PlayerPrefs.SetFloat(AudioOffsetPrefsKey, (float)offsetMs);
    }

    /// <summary>
    /// 키 입력/오디오 출력 오프셋을 모두 기본값으로 되돌린다.
    /// </summary>
    public void ResetOffsets()
    {
        SetKeyInputOffset(0.0);
        SetAudioOffset(0.0);
    }

    // GOOD 판정의 최대 허용 오차를 초 단위로 반환한다.
    public double GetGoodWindowSeconds(double noteDurationSeconds)
    {
        return noteDurationSeconds * goodWindowRatio;
    }

    // 입력 오프셋을 적용했을 때 입력 후보가 된 노트인지 확인한다.
    public bool IsNoteActive(double inputTime, double judgeTime)
    {
        return CalcOffsetMs(inputTime, judgeTime)
            >= -noteActivationLeadTimeMs;
    }

    // 입력 오프셋을 적용했을 때 GOOD의 늦은 경계를 지났는지 확인한다.
    public bool HasPassedHitWindow(
        double inputTime,
        double judgeTime,
        double noteDurationSeconds)
    {
        return CalcOffsetMs(inputTime, judgeTime)
            > GetGoodWindowSeconds(noteDurationSeconds) * 1000.0;
    }
}