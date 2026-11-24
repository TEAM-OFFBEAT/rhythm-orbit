using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 실제 전달된 노트 일부를 방어 시작 시 유령으로 변환한다.
/// 취소된 이벤트의 발동 횟수 집계는 SurpriseEventManager가 담당한다.
/// </summary>
public class Def01SurpriseEventHandler : MonoBehaviour, ISurpriseEventHandler, ISurpriseEventPreludeHandler
{
    [SerializeField] private DefenseTurn defenseTurn;
    [SerializeField] private SanitySystem sanitySystem;
    [SerializeField, Min(3)] private int minimumDeliveredNotes = 3;
    [SerializeField, Range(0f, 1f)] private float ghostNoteAlpha = 0.7f;
    [SerializeField, Min(0)] private int ghostHitPenalty = 5;

    private SurpriseEventContext activeContext;
    [SerializeField, Min(0f)] private float conversionLeadSeconds = 0.45f;
    [SerializeField, Min(0f)] private float conversionStaggerSeconds = 0.12f;
    private readonly List<int> selectedNoteIds = new();
    private Coroutine previewCoroutine;
    private AttackTurn attackTurn;
    private bool activated;

    public void BeginPrelude(SurpriseEventContext context)
    {
        activeContext = context;
        var network = NetworkManager.Instance;
        if (network != null && network.LocalPlayerId != context.targetPlayerId) return;
        attackTurn = FindAnyObjectByType<AttackTurn>();
        if (defenseTurn == null || attackTurn == null) return;
        previewCoroutine = StartCoroutine(PreviewConversion(context));
    }

    public void EndPrelude(SurpriseEventContext context) => Cleanup();

    private IEnumerator PreviewConversion(SurpriseEventContext context)
    {
        while (AudioSettings.dspTime < context.phaseStartDspTime - conversionLeadSeconds)
            yield return null;

        IReadOnlyList<NoteData> notes;
        do
        {
            notes = NetworkManager.Instance != null
                ? defenseTurn.ReceivedAttackNotes : attackTurn.CurrentCreatedNotes;
            if (notes.Count >= minimumDeliveredNotes) break;
            if (AudioSettings.dspTime >= context.phaseStartDspTime)
            {
                previewCoroutine = null;
                yield break;
            }
            yield return null;
        } while (true);

        var candidates = new List<NoteData>(notes);
        int count = candidates.Count == 3 ? 1 : Random.Range(1, 3);
        var selected = new List<NoteData>();
        for (int i = 0; i < count; i++)
        {
            int index = Random.Range(0, candidates.Count);
            selected.Add(candidates[index]);
            candidates.RemoveAt(index);
        }
        selected.Sort((a, b) => a.noteRelativeTime.CompareTo(b.noteRelativeTime));
        foreach (NoteData note in selected) selectedNoteIds.Add(note.noteId);

        for (int i = 0; i < selected.Count; i++)
        {
            double conversionTime = context.phaseStartDspTime - conversionLeadSeconds +
                i * conversionStaggerSeconds;
            while (AudioSettings.dspTime < conversionTime &&
                   AudioSettings.dspTime < context.phaseStartDspTime)
                yield return null;
            if (activated) break;
            NoteData note = selected[i];
            NoteRenderer.Instance?.ConvertNoteToGhost(note.noteId, ghostNoteAlpha,
                context.phaseStartDspTime + note.noteRelativeTime);
            NetworkManager.Instance?.Send(w =>
                PacketSerializer.WriteJudgment(w, note.noteId, Judgment.SKIPPED, false));
        }
        previewCoroutine = null;
    }
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
        if (!defenseTurn.ConvertDeliveredNotesToGhosts(minimumDeliveredNotes, ghostNoteAlpha, selectedNoteIds))
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
        if (previewCoroutine != null)
        {
            StopCoroutine(previewCoroutine);
            previewCoroutine = null;
        }
        selectedNoteIds.Clear();
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