using UnityEngine;

/// <summary>
/// 실제 전달된 노트 일부를 방어 시작 시 유령으로 변환한다.
/// 취소된 이벤트의 발동 횟수 집계는 SurpriseEventManager가 담당한다.
/// </summary>
public class Def01SurpriseEventHandler : MonoBehaviour, ISurpriseEventHandler
{
    [SerializeField] private DefenseTurn defenseTurn;
    [SerializeField] private SanitySystem sanitySystem;
    [SerializeField, Min(3)] private int minimumDeliveredNotes = 3;
    [SerializeField, Range(0f, 1f)] private float ghostNoteAlpha = 0.7f;
    [SerializeField, Min(0)] private int ghostHitPenalty = 5;

    private SurpriseEventContext activeContext;
    private bool activated;
    public SurpriseEventId EventId => SurpriseEventId.EVT_DEF_01_GhostSignal;

    private void Awake()
    {
        if (defenseTurn == null) defenseTurn = FindAnyObjectByType<DefenseTurn>();
        if (sanitySystem == null) sanitySystem = FindAnyObjectByType<SanitySystem>();
    }

    public void EnterEvent(SurpriseEventContext context)
    {
        activeContext = context;
    }

    public void BeginEventPhase(SurpriseEventContext context)
    {
        activeContext = context;
        var network = NetworkManager.Instance;
        if (network != null && network.LocalPlayerId != context.targetPlayerId) return;
        if (defenseTurn == null) return;

        defenseTurn.OnDefenseBegan += TryActivate;
        defenseTurn.OnGhostNoteHit += HandleExtraInput;
        TryActivate();
    }

    private void TryActivate()
    {
        if (activated || !defenseTurn.IsRunning) return;
        activated = true;
        if (!defenseTurn.ConvertDeliveredNotesToGhosts(minimumDeliveredNotes, ghostNoteAlpha))
            Debug.Log("DEF_01: 전달 노트 부족으로 취소. 일반 방어 진행.");
    }

    private void HandleExtraInput(GhostNoteData note)
    {
        if (activeContext == null) return;
        int target = activeContext.targetPlayerId;
        sanitySystem?.ApplyDirect(target, ghostHitPenalty);
        NetworkManager.Instance?.Send(w =>
            PacketSerializer.WriteSanityChange(w, (byte)target, ghostHitPenalty));
    }

    public void EndEvent(SurpriseEventContext context) => Cleanup();
    private void OnDisable() => Cleanup();

    private void Cleanup()
    {
        if (defenseTurn != null)
        {
            defenseTurn.OnDefenseBegan -= TryActivate;
            defenseTurn.OnGhostNoteHit -= HandleExtraInput;
            defenseTurn.DeactivateGhostSignal();
        }
        activeContext = null;
        activated = false;
    }
}