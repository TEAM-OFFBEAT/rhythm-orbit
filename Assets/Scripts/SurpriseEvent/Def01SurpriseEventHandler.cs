using UnityEngine;

/// <summary>
/// 공격 종료로 확정된 실제 전달 수를 확인한 뒤 진입 안내와 유령 변환을 시작한다.
/// </summary>
public class Def01SurpriseEventHandler : MonoBehaviour, ISurpriseEventHandler
{
    [SerializeField] private DefenseTurn defenseTurn;
    [SerializeField] private SanitySystem sanitySystem;
    [SerializeField, Min(3)] private int minimumDeliveredNotes = 3;
    [SerializeField, Range(0f, 1f)] private float ghostNoteAlpha = 0.7f;
    [SerializeField, Min(0)] private int ghostHitPenalty = 5;
    [SerializeField, Min(0f)] private float conversionStaggerSeconds = 0.12f;

    private SurpriseEventContext activeContext;
    private AttackTurn attackTurn;
    private NetworkManager network;
    private SurpriseEventManager eventManager;
    private int finalNoteCount = -1;
    private bool phaseBegun;
    private bool activated;

    public SurpriseEventId EventId => SurpriseEventId.EVT_DEF_01_GhostSignal;

    private void Awake()
    {
        if (defenseTurn == null) defenseTurn = FindAnyObjectByType<DefenseTurn>();
        if (sanitySystem == null) sanitySystem = FindAnyObjectByType<SanitySystem>();
        attackTurn = FindAnyObjectByType<AttackTurn>();
        eventManager = GetComponentInParent<SurpriseEventManager>();
    }

    public void EnterEvent(SurpriseEventContext context)
    {
        activeContext = context;
        finalNoteCount = -1;
        phaseBegun = false;
        activated = false;
        network = NetworkManager.Instance;
        if (attackTurn != null) attackTurn.OnAttackEnded += HandleAttackEnded;
        if (network != null) network.OnAttackCompleted += HandleRemoteAttackCompleted;
        if (defenseTurn != null) defenseTurn.OnDefenseBegan += TryActivate;
    }

    private void HandleAttackEnded(AttackResult result)
    {
        if (activeContext == null) return;
        // 원격 공격 턴의 로컬 AttackTurn 결과는 사용하지 않는다.
        if (network != null && network.LocalPlayerId == activeContext.targetPlayerId) return;
        int count = result.Notes?.Count ?? 0;
        double defenseStart = attackTurn.AttackStartDspTime + attackTurn.AttackDuration;
        if (System.Math.Abs(defenseStart - activeContext.phaseStartDspTime) > 0.01) return;
        network?.Send(w => PacketSerializer.WriteAttackCompleted(w, defenseStart, count));
        ConfirmFinalCount(count);
    }

    private void HandleRemoteAttackCompleted(double remoteDefenseStart, int count)
    {
        double localStart = network.TimeSync?.CorrectTime(remoteDefenseStart) ?? remoteDefenseStart;
        if (activeContext == null ||
            System.Math.Abs(localStart - activeContext.phaseStartDspTime) > 0.1) return;
        ConfirmFinalCount(count);
    }

    private void ConfirmFinalCount(int count)
    {
        if (finalNoteCount >= 0) return;
        finalNoteCount = count;
        eventManager?.ConfirmGhostEvent(count >= Mathf.Max(3, minimumDeliveredNotes));
        TryActivate();
    }

    public void BeginEventPhase(SurpriseEventContext context)
    {
        phaseBegun = true;
        TryActivate();
    }

    private void Update()
    {
        // 방어 시작과 수신 순서가 달라도 공격 종료로 확정된 노트 수를 기다린다.
        if (phaseBegun && !activated && finalNoteCount >= 0) TryActivate();
    }

    private void TryActivate()
    {
        if (activated || !phaseBegun || finalNoteCount < 0 || defenseTurn == null) return;
        if (network != null && network.LocalPlayerId != activeContext.targetPlayerId) return;
        if (finalNoteCount < Mathf.Max(3, minimumDeliveredNotes))
        {
            activated = true;
            return;
        }
        if (!defenseTurn.IsRunning || defenseTurn.PendingAttackNotes.Count < finalNoteCount) return;
        activated = true;
        defenseTurn.OnGhostNoteHit += HandleExtraInput;
        defenseTurn.ConvertDeliveredNotesToGhosts(
            minimumDeliveredNotes, ghostNoteAlpha, conversionStaggerSeconds);
    }

    private void HandleExtraInput(GhostNoteData note)
    {
        if (activeContext == null) return;
        int target = activeContext.targetPlayerId;
        sanitySystem?.ApplyDirect(target, ghostHitPenalty);
        network?.Send(w => PacketSerializer.WriteSanityChange(w, (byte)target, ghostHitPenalty));
    }

    public void EndEvent(SurpriseEventContext context) => Cleanup();
    private void OnDisable() => Cleanup();

    private void Cleanup()
    {
        if (attackTurn != null) attackTurn.OnAttackEnded -= HandleAttackEnded;
        if (network != null) network.OnAttackCompleted -= HandleRemoteAttackCompleted;
        if (defenseTurn != null)
        {
            defenseTurn.OnDefenseBegan -= TryActivate;
            defenseTurn.OnGhostNoteHit -= HandleExtraInput;
            defenseTurn.DeactivateGhostSignal();
        }
        activeContext = null;
        finalNoteCount = -1;
        phaseBegun = false;
        activated = false;
        network = null;
    }
}