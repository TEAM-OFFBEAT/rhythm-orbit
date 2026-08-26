using System;

// ─────────────────────────────────────────────────────────────
// 실제로 이번에 발생한 이벤트의 실행 정보
// ─────────────────────────────────────────────────────────────

/// <summary>
/// 이번에 실제로 발생한 기습 이벤트 정보를 담는다.
/// Context는 '상황 정보'라고 이해하면 된다.
/// Context는 "이번 턴에 실제로 걸린 이벤트 정보"다.
/// </summary>
[Serializable]
public class SurpriseEventContext
{
    /// <summary>
    /// 어떤 이벤트가 뽑혔는지.
    /// 예: EVT_DEF_01_GhostSignal
    /// </summary>
    public SurpriseEventId eventId;

    /// <summary>
    /// 이번 이벤트가 공격/방어 중 어느 페이즈에 적용되는지.
    /// </summary>
    public SurpriseEventPhase phase;

    /// <summary>
    /// 이벤트 영향을 받는 플레이어 ID.
    /// 공격 이벤트라면 다음 공격자,
    /// 방어 이벤트라면 다음 방어자가 대상이 된다.
    /// </summary>
    public int targetPlayerId;

    /// <summary>
    /// 이벤트가 실제로 적용될 페이즈의 시작 DSP 시각.
    /// </summary>
    public double phaseStartDspTime;

    /// <summary>
    /// 이 Context가 실제 사용 가능한 이벤트 정보인지 확인한다.
    /// </summary>
    public bool IsValid =>
        eventId != SurpriseEventId.None &&
        phase != SurpriseEventPhase.None &&
        targetPlayerId > 0;
}