using UnityEngine;

public class RhythmClock : SceneSingleton<RhythmClock>
{
    [SerializeField] private float bpm = 120f;

    public float Bpm => bpm;

    public double GameStartDspTime { get; private set; }
    public bool IsRunning { get; private set; }


    /// <summary>
    /// 게임 시작 이후 흐른 시간을 초 단위로 반환.
    /// </summary>
    public double GetGameTime()
    {
        if (!IsRunning)
        {
            return 0.0;
        }

        double gameTime = AudioSettings.dspTime - GameStartDspTime;
        return System.Math.Max(0.0, gameTime);
    }

    /// <summary>
    /// 현재 bpm 기준 1박(4분음표) 길이를 초 단위로 반환.
    /// </summary>
    public double GetBeatDuration()
    {
        if (bpm <= 0f)
        {
            Debug.LogWarning("BPM은 0보다 커야 합니다. 기본값 120으로 계산합니다.");
            return 60.0 / 120.0;
        }
        return 60.0 / bpm;
    }

    /// <summary>
    /// 1박을 subdivisions로 나눈 최소 리듬 단위 길이를 초 단위로 반환.
    /// subdivisions=2이면 반박(8분음표), subdivisions=3이면 3연음부.
    /// </summary>
    public double GetNoteDuration(int subdivisions = 2)
    {
        int safeSubdivisions = System.Math.Max(1, subdivisions);
        return GetBeatDuration() / safeSubdivisions;
    }

    /// <summary>
    /// bpm 값을 변경.
    /// </summary>
    public void SetBpm(float newBpm)
    {
        if (newBpm <= 0f)
        {
            Debug.LogWarning("BPM은 0보다 커야 합니다.");
            return;
        }

        bpm = newBpm;
    }

    /// <summary>
    /// 지정한 dspTime을 게임 시작 시간으로 설정하고 시계를 시작.
    /// </summary>
    public void StartClock(double startDspTime)
    {
        GameStartDspTime = startDspTime;
        IsRunning = true;
    }

    /// <summary>
    /// 현재 AudioSettings.dspTime을 기준으로 즉시 시계를 시작.
    /// </summary>
    public void StartClockNow()
    {
        StartClock(AudioSettings.dspTime);
    }
    
    /// <summary>
    /// 시계 동작을 정지.
    /// </summary>
    public void StopClock()
    {
        IsRunning = false;
    }
}