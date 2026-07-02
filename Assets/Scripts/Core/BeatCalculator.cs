using UnityEngine;

public static class BeatCalculator
{
    private const int Numerator = 4; // 4/4박자 
    

    /// <summary>박자 수를 초 단위 시간으로 변환. GameManager, AttackTurn에서 호출.</summary>
    public static double BeatToSec(int beat)
    {
        return beat * RhythmClock.Instance.GetNoteDuration();
    }

    /// <summary>입력 시간을 가장 가까운 정박/반박 위치로 보정. AttackTurn에서 노트 생성 시 호출.</summary>
    public static double SnapToBeat(double gameTime)
    {
        return System.Math.Round(
            gameTime / RhythmClock.Instance.GetNoteDuration(),
            System.MidpointRounding.AwayFromZero
        ) * RhythmClock.Instance.GetNoteDuration();
    }

    /// <summary>입력 시간이 허용 범위 안에 있는지 확인. AttackTurn에서 공격 실패 판정 시 호출.</summary>
    public static bool IsOnGrid(double gameTime, double gridToleranceMs = 100.0) //기본 100ms
    {
        double snappedTime = SnapToBeat(gameTime); 
        double diffMs = System.Math.Abs(gameTime - snappedTime) * 1000.0;

        return diffMs <= gridToleranceMs;
    }

     /// <summary>현재 박자 기준 '한 마디'의 길이를 초 단위로 반환. 턴 길이 계산 시 GameManager에서 호출.</summary>
    public static double GetMeasureDuration()
    {
        int safeNumerator = System.Math.Max(1, Numerator);
        return RhythmClock.Instance.GetNoteDuration()* 2.0 * safeNumerator;
    }
}