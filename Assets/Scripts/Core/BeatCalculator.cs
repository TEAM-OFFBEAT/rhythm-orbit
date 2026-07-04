using UnityEngine;

public static class BeatCalculator
{
    private const int Numerator = 4; // 4/4박자

    /// <summary>
    /// 박자 수를 초 단위 시간으로 변환.
    /// </summary>
    public static double BeatToSec(int beat, double noteDuration)
    {
        return beat * noteDuration;
    }

    /// <summary>
    /// 입력 시간을 가장 가까운 박자 위치로 반올림.
    /// </summary>
    public static double SnapToBeat(double gameTime, double noteDuration)
    {
        return System.Math.Round(
            gameTime / noteDuration,
            System.MidpointRounding.AwayFromZero
        ) * noteDuration;
    }

    /// <summary>
    /// 입력 시간이 격자 허용 범위 안에 있는지 확인.
    /// </summary>
    public static bool IsOnGrid(double gameTime, double noteDuration, double gridToleranceMs = 100.0)
    {
        double snappedTime = SnapToBeat(gameTime, noteDuration);
        double diffMs = System.Math.Abs(gameTime - snappedTime) * 1000.0;
        return diffMs <= gridToleranceMs;
    }

    /// <summary>
    /// 현재 박자 기준 한 마디(4/4박자)의 길이를 초 단위로 반환.
    /// </summary>
    public static double GetMeasureDuration(double noteDuration)
    {
        int safeNumerator = System.Math.Max(1, Numerator);
        return noteDuration * 2.0 * safeNumerator;
    }
}
