using UnityEngine;

// ─────────────────────────────────────────────────────────────
// 실제 이벤트 구현 전 테스트용 핸들러
// ─────────────────────────────────────────────────────────────

/// <summary>
/// 개별 이벤트 구현 전, 시스템 흐름만 확인하기 위한 테스트용 핸들러.
/// 
/// 실제 입력 규칙은 바꾸지 않고,
/// 이벤트 진입/적용/복귀 시 로그만 찍는다.
/// </summary>
public class DebugSurpriseEventHandler : MonoBehaviour, ISurpriseEventHandler
{
    [Tooltip("이 핸들러가 담당할 이벤트 ID.")]
    [SerializeField] private SurpriseEventId eventId;

    public SurpriseEventId EventId => eventId;

    public void EnterEvent(SurpriseEventContext context)
    {
        Debug.Log($"[DebugEvent] Enter / {context.eventId}, target:{context.targetPlayerId}");
    }

    public void BeginEventPhase(SurpriseEventContext context)
    {
        Debug.Log($"[DebugEvent] Begin Phase / {context.eventId}, phase:{context.phase}");
    }

    public void EndEvent(SurpriseEventContext context)
    {
        Debug.Log($"[DebugEvent] End / {context.eventId}");
    }
}