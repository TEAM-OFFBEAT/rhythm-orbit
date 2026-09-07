using UnityEngine;

/// <summary>
/// EVT_ATK_01 불완전 송신 이벤트 핸들러.
/// AttackTurn의 더블탭 모드를 활성화/비활성화하고,
/// 반쪽 노트 파괴 시 대상 플레이어에게 정신력 -incompletePenalty를 적용한다.
/// </summary>
public class Atk01SurpriseEventHandler : MonoBehaviour, ISurpriseEventHandler
{
    [Tooltip("반쪽 노트 파괴 1회당 정신력 감소량.")]
    [SerializeField] private int incompletePenalty = 2;

    private AttackTurn attackTurn;
    private SanitySystem sanitySystem;

    private SurpriseEventContext activeContext;

    private void Awake()
    {
        attackTurn = FindObjectOfType<AttackTurn>();
        sanitySystem = FindObjectOfType<SanitySystem>();
    }

    public SurpriseEventId EventId => SurpriseEventId.EVT_ATK_01_IncompleteTransmission;

    /// <summary>
    /// 이벤트 진입 시 호출. 토스트·효과음은 SurpriseEventManager가 처리한다.
    /// </summary>
    public void EnterEvent(SurpriseEventContext context) { }

    /// <summary>
    /// 공격 4박 페이즈가 시작될 때 호출. AttackTurn을 더블탭 모드로 전환한다.
    /// </summary>
    public void BeginEventPhase(SurpriseEventContext context)
    {
        activeContext = context;
        attackTurn.OnHalfNoteIncomplete += ApplyIncompletePenalty;
        attackTurn.ActivateDoubleTapMode();
    }

    /// <summary>
    /// 공격 4박 페이즈가 끝날 때 호출. AttackTurn을 정상 모드로 복귀시킨다.
    /// </summary>
    public void EndEvent(SurpriseEventContext context)
    {
        attackTurn.DeactivateDoubleTapMode();
        attackTurn.OnHalfNoteIncomplete -= ApplyIncompletePenalty;
        activeContext = null;
    }

    private void ApplyIncompletePenalty()
    {
        if (activeContext == null) return;
        sanitySystem.ApplyDirect(activeContext.targetPlayerId, incompletePenalty);
    }
}
