// ─────────────────────────────────────────────────────────────
// 개별 기습 이벤트들이 반드시 따라야 하는 약속
// ─────────────────────────────────────────────────────────────

/// <summary>
// 핸들러의 틀을 정의한다.
/// </summary>
public interface ISurpriseEventHandler
{
    /// <summary>
    /// 이 핸들러가 담당하는 이벤트 ID.
    /// Manager가 "어떤 이벤트 핸들러를 호출해야 하는지" 찾을 때 사용한다.
    /// </summary>
    SurpriseEventId EventId { get; }

    /// <summary>
    /// 이벤트 진입 시 호출된다.
    /// 여기서는:
    /// - 이벤트 발생 토스트
    /// - 이벤트 고유 효과음
    /// - 사전 연출
    /// 등을 처리한다.
    /// </summary>
    void EnterEvent(SurpriseEventContext context);

    /// <summary>
    /// 이벤트가 적용되는 실제 공격/방어 4박 페이즈가 시작될 때 호출된다.
    /// 
    /// 여기서는:
    /// - 입력 규칙 변경
    /// - 시야 방해 연출 시작
    /// - 노트 변조 시작
    /// 등을 처리한다.
    /// </summary>
    void BeginEventPhase(SurpriseEventContext context);

    /// <summary>
    /// 이벤트 적용 페이즈가 끝났을 때 호출된다.
    /// 
    /// 여기서는:
    /// - 입력 규칙 원상복구
    /// - 전용 연출 제거
    /// - BGM 덕킹 해제
    /// 등을 처리한다.
    /// </summary>
    void EndEvent(SurpriseEventContext context);
}