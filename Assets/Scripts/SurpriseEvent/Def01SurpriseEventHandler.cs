using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// EVT_DEF_01 유령 신호 이벤트 핸들러.
/// 진입 단계에서 유령 노트를 미리 생성하고 fade in 시킨 뒤,
/// 방어 턴이 시작되면 해당 유령 노트를 입력 후보로 등록한다.
/// </summary>
public class Def01SurpriseEventHandler : MonoBehaviour, ISurpriseEventHandler
{
    [Header("References")]
    [SerializeField] private AttackTurn attackTurn;
    [SerializeField] private AttackTurnRenderer attackTurnRenderer;
    [SerializeField] private DefenseTurn defenseTurn;
    [SerializeField] private SanitySystem sanitySystem;

    [Header("Ghost Note")]
    [SerializeField, Min(0)] private int ghostNoteCount = 2;
    [SerializeField, Min(1)] private int ghostNoteIdBase = 700000;
    [SerializeField, Range(0f, 1f)] private float ghostNoteAlpha = 0.7f;
    [SerializeField, Min(0.01f)] private float ghostFadeInSeconds = 0.45f;
    [Tooltip("공격 판정선이 해당 칸을 지난 뒤 몇 초 후 유령 노트가 나타날지 정한다.")]
    [SerializeField, Min(0f)] private float appearAfterLinePassSeconds = 0.05f;

    [Tooltip("여러 유령 노트가 동시에 나타나지 않도록 추가 시간차를 준다.")]
    [SerializeField, Min(0f)] private float ghostStaggerSeconds = 0.12f;
    [Tooltip("유령 노트가 미리 배치된 칸에 실제 노트가 뒤늦게 생성되면 유령 노트를 제거한다.")]
    [SerializeField] private bool removeGhostIfRealNoteAppearsOnSameStep = true;
    
    [Header("Penalty")]
    [Tooltip("유령 노트를 타격했을 때 대상 플레이어에게 적용할 정신력 감소량.")]
    [SerializeField, Min(0)] private int ghostHitPenalty = 5;

    [Header("SFX")]
    [SerializeField] private bool playSfxOnEnter = true;
    [SerializeField] private bool playSfxOnGhostHit = true;
    [SerializeField] private AudioClip ghostSignalSfx;
    [SerializeField] private AudioClip ghostHitSfx;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

    private readonly List<GhostNoteData> preparedGhostNotes = new();
    private readonly Dictionary<int, double> ghostFadeStartTimes = new();
    private double eventEnteredDspTime;
    private SurpriseEventContext activeContext;
    private Coroutine fadeInCoroutine;
    private bool isSubscribed;
    private bool ghostSignalActivated;
    private int nextGhostNoteId;
    private double cachedNoteDuration;

    public SurpriseEventId EventId => SurpriseEventId.EVT_DEF_01_GhostSignal;

    private void Awake()
    {
        if (attackTurn == null)
        {
            attackTurn = FindAnyObjectByType<AttackTurn>();
        }

        if (attackTurnRenderer == null)
        {
            attackTurnRenderer = FindAnyObjectByType<AttackTurnRenderer>();
        }

        if (defenseTurn == null)
        {
            defenseTurn = FindAnyObjectByType<DefenseTurn>();
        }

        if (sanitySystem == null)
        {
            sanitySystem = FindAnyObjectByType<SanitySystem>();
        }

        nextGhostNoteId = ghostNoteIdBase;
    }

    private void OnDisable()
    {
        StopFadeIn();
        Unsubscribe();

        defenseTurn?.DeactivateGhostSignal();
        attackTurnRenderer?.ClearGhostNotes();

        preparedGhostNotes.Clear();
        activeContext = null;
        ghostSignalActivated = false;

        UnsubscribeAttackNoteCreated();
        ghostFadeStartTimes.Clear();
    }

    /// <summary>
    /// 이벤트 진입 시 호출된다.
    /// 방어 턴 시작 전에 유령 노트를 미리 만들고 스으윽 나타나게 한다.
    /// </summary>
    public void EnterEvent(SurpriseEventContext context)
    {
        activeContext = context;
        ghostSignalActivated = false;
 
        eventEnteredDspTime = AudioSettings.dspTime;
        ghostFadeStartTimes.Clear();

        if (playSfxOnEnter)
        {
            PlayClip(ghostSignalSfx);
        }

        PrepareGhostPreviewNotes(context);
        StartGhostFadeIn();
        SubscribeAttackNoteCreated();

        Debug.Log($"DEF_01 GhostSignal 진입 / previewCount:{preparedGhostNotes.Count}");
    }

    /// <summary>
    /// 방어 페이즈가 실제로 시작되면 미리 만들어둔 유령 노트를 입력 후보로 등록한다.
    /// 여기서 새 유령 노트를 생성하면 방어턴 시작 때 갑자기 생기므로 생성하지 않는다.
    /// </summary>
    public void BeginEventPhase(SurpriseEventContext context)
    {
        activeContext = context;

        Subscribe();
        TryActivateGhostSignal();

        Debug.Log($"DEF_01 GhostSignal 적용 준비 / target:P{context.targetPlayerId}");
    }

    /// <summary>
    /// 방어 페이즈 종료 시 유령 노트를 제거하고 이벤트 구독을 해제한다.
    /// </summary>
    public void EndEvent(SurpriseEventContext context)
    {
        StopFadeIn();

        defenseTurn?.DeactivateGhostSignal();
        attackTurnRenderer?.ClearGhostNotes();

        Unsubscribe();

        preparedGhostNotes.Clear();
        activeContext = null;
        ghostSignalActivated = false;

        UnsubscribeAttackNoteCreated();

        ghostFadeStartTimes.Clear();

        Debug.Log("DEF_01 GhostSignal 종료");
    }

    private void PrepareGhostPreviewNotes(SurpriseEventContext context)
    {
        preparedGhostNotes.Clear();
        attackTurnRenderer?.ClearGhostNotes();

        if (attackTurn == null || attackTurnRenderer == null)
        {
            Debug.LogWarning("DEF_01: AttackTurn 또는 AttackTurnRenderer가 연결되지 않음.");
            return;
        }

        if (ghostNoteCount <= 0)
        {
            return;
        }

        double noteDuration = GetNoteDurationSeconds();

        if (noteDuration <= 0.0 || attackTurn.AttackDuration <= 0.0)
        {
            Debug.LogWarning("DEF_01: 노트 길이 또는 공격 턴 길이가 올바르지 않음.");
            return;
        }

        cachedNoteDuration = noteDuration;

        List<int> candidateSteps = BuildBalancedCandidateSteps(noteDuration);

        if (candidateSteps.Count == 0)
        {
            Debug.Log("DEF_01: 유령 노트를 배치할 빈 박자가 없음.");
            return;
        }

        List<int> selectedSteps = PickEvenlyDistributedSteps(candidateSteps, ghostNoteCount);

        // 등장 순서는 살짝 랜덤하게.
        // 위치는 이미 균등하게 뽑혔기 때문에 Shuffle해도 몰림 문제는 거의 없음.
        Shuffle(selectedSteps);

        int spawnCount = selectedSteps.Count;

        AttackSide attackerSide = attackTurn.CurrentSide;

        float attackStartX = attackTurnRenderer.GetStartX(attackerSide);
        float attackEndX = attackTurnRenderer.GetEndX(attackerSide);

        for (int i = 0; i < spawnCount; i++)
        {
            int step = selectedSteps[i];
            double relativeTime = step * noteDuration;

            GhostNoteData ghostNote = new GhostNoteData
            {
                noteId = nextGhostNoteId++,
                noteType = Random.value < 0.5f ? NoteType.HIGH : NoteType.LOW,
                noteRelativeTime = relativeTime,
                judgeTime = context.phaseStartDspTime + relativeTime
            };

            preparedGhostNotes.Add(ghostNote);

            attackTurnRenderer.SpawnGhostNote(
                ghostNote,
                attackTurn.AttackDuration,
                attackStartX,
                attackEndX,
                0f
            );

            double linePassDspTime =
                attackTurn.AttackStartDspTime +
                ghostNote.noteRelativeTime +
                appearAfterLinePassSeconds;

            double staggeredEnterDspTime =
                eventEnteredDspTime +
                ghostStaggerSeconds * i;

            ghostFadeStartTimes[ghostNote.noteId] =
                System.Math.Max(linePassDspTime, staggeredEnterDspTime);

            Debug.Log(
                $"DEF_01 Ghost preview 생성 / id:{ghostNote.noteId}, " +
                $"type:{ghostNote.noteType}, step:{step}, rel:{relativeTime:0.000}"
            );
        }
    }

    /// <summary>
    /// 전체 플레이 가능 칸 중 실제 노트가 없는 칸을 후보로 만든다.
    /// 초반을 제외하지 않고, 전체 범위에서 골고루 뽑을 준비를 한다.
    /// </summary>
    private List<int> BuildBalancedCandidateSteps(double noteDuration)
    {
        List<int> candidates = new List<int>();
        HashSet<int> occupiedSteps = new HashSet<int>();

        IReadOnlyList<NoteData> createdNotes = attackTurn.CurrentCreatedNotes;

        if (createdNotes != null)
        {
            foreach (NoteData note in createdNotes)
            {
                if (note == null)
                {
                    continue;
                }

                int occupiedStep = GetGridStep(note.noteRelativeTime, noteDuration);
                occupiedSteps.Add(occupiedStep);
            }
        }

        int maxStep = Mathf.FloorToInt((float)(attackTurn.AttackDuration / noteDuration));

        // 0번째 칸은 시작 경계, maxStep은 종료 경계라 제외.
        // 즉 1 ~ maxStep - 1만 유령 노트 후보.
        for (int step = 1; step < maxStep; step++)
        {
            if (occupiedSteps.Contains(step))
            {
                continue;
            }

            candidates.Add(step);
        }

        candidates.Sort();

        return candidates;
    }

    private void StartGhostFadeIn()
    {
        StopFadeIn();

        if (preparedGhostNotes.Count == 0)
        {
            return;
        }

        fadeInCoroutine = StartCoroutine(FadeInRoutine());
    }

    private IEnumerator FadeInRoutine()
    {
        float fadeDuration = Mathf.Max(0.01f, ghostFadeInSeconds);

        bool allFinished = false;

        while (!allFinished)
        {
            allFinished = true;

            double now = AudioSettings.dspTime;

            foreach (GhostNoteData ghostNote in preparedGhostNotes)
            {
                if (ghostNote == null)
                {
                    continue;
                }

                if (!ghostFadeStartTimes.TryGetValue(ghostNote.noteId, out double fadeStartDspTime))
                {
                    fadeStartDspTime = eventEnteredDspTime;
                    ghostFadeStartTimes[ghostNote.noteId] = fadeStartDspTime;
                }

                double fadeElapsed = now - fadeStartDspTime;

                if (fadeElapsed <= 0.0)
                {
                    NoteRenderer.Instance?.SetNoteAlpha(ghostNote.noteId, 0f);
                    allFinished = false;
                    continue;
                }

                float t = Mathf.Clamp01((float)(fadeElapsed / fadeDuration));
                float alpha = Mathf.Lerp(0f, ghostNoteAlpha, t);

                NoteRenderer.Instance?.SetNoteAlpha(ghostNote.noteId, alpha);

                if (t < 1f)
                {
                    allFinished = false;
                }
            }

            yield return null;
        }

        SetPreparedGhostAlpha(ghostNoteAlpha);
        fadeInCoroutine = null;
    }

    private void SetPreparedGhostAlpha(float alpha)
    {
        foreach (GhostNoteData ghostNote in preparedGhostNotes)
        {
            if (ghostNote == null)
            {
                continue;
            }

            NoteRenderer.Instance?.SetNoteAlpha(ghostNote.noteId, alpha);
        }
    }

    private void StopFadeIn()
    {
        if (fadeInCoroutine == null)
        {
            return;
        }

        StopCoroutine(fadeInCoroutine);
        fadeInCoroutine = null;
    }

    private void Subscribe()
    {
        if (isSubscribed || defenseTurn == null)
        {
            return;
        }

        defenseTurn.OnGhostNoteHit += HandleGhostNoteHit;
        defenseTurn.OnDefenseBegan += HandleDefenseBegan;
        isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (!isSubscribed || defenseTurn == null)
        {
            return;
        }

        defenseTurn.OnGhostNoteHit -= HandleGhostNoteHit;
        defenseTurn.OnDefenseBegan -= HandleDefenseBegan;
        isSubscribed = false;
    }

    private void HandleDefenseBegan()
    {
        TryActivateGhostSignal();
    }

    private void TryActivateGhostSignal()
    {
        if (ghostSignalActivated)
        {
            return;
        }

        if (activeContext == null)
        {
            return;
        }

        if (defenseTurn == null)
        {
            Debug.LogWarning("DEF_01: DefenseTurn이 연결되지 않음.");
            return;
        }

        if (!defenseTurn.IsRunning)
        {
            Debug.Log("DEF_01: DefenseTurn이 아직 시작되지 않아 OnDefenseBegan을 기다림.");
            return;
        }

        defenseTurn.SetGhostSignalNotes(preparedGhostNotes);

        ghostSignalActivated = true;

        Debug.Log(
            $"DEF_01 GhostSignal 적용 시작 / " +
            $"target:P{activeContext.targetPlayerId}, ghostCount:{preparedGhostNotes.Count}"
        );
    }

    private void HandleGhostNoteHit(GhostNoteData ghostNote)
    {
        if (activeContext == null)
        {
            return;
        }

        if (ghostHitPenalty > 0 && sanitySystem != null)
        {
            sanitySystem.ApplyDirect(activeContext.targetPlayerId, ghostHitPenalty);
        }

        if (playSfxOnGhostHit)
        {
            AudioClip clip = ghostHitSfx != null ? ghostHitSfx : ghostSignalSfx;
            PlayClip(clip);
        }

        Debug.Log(
            $"DEF_01 Ghost hit penalty / " +
            $"target:P{activeContext.targetPlayerId}, penalty:{ghostHitPenalty}, noteId:{ghostNote?.noteId}"
        );
    }

    private double GetNoteDurationSeconds()
    {
        if (RhythmClock.Instance != null)
        {
            return RhythmClock.Instance.GetNoteDuration(2);
        }

        return 60.0 / 120.0 / 2.0;
    }

    private void Shuffle(List<int> values)
    {
        for (int i = 0; i < values.Count; i++)
        {
            int randomIndex = Random.Range(i, values.Count);
            (values[i], values[randomIndex]) = (values[randomIndex], values[i]);
        }
    }

    private void PlayClip(AudioClip clip)
    {
        if (clip == null)
        {
            return;
        }

        AudioSource.PlayClipAtPoint(clip, Vector3.zero, sfxVolume);
    }

    /// <summary>
    /// 후보 칸을 전체 구간 기준으로 나눠서 골고루 선택한다.
    /// 예: 후보가 1~7이고 2개를 뽑으면 앞쪽 구간 1개, 뒤쪽 구간 1개를 뽑는다.
    /// </summary>
    private List<int> PickEvenlyDistributedSteps(List<int> candidates, int count)
    {
        List<int> selected = new List<int>();

        if (candidates == null || candidates.Count == 0)
        {
            return selected;
        }

        int targetCount = Mathf.Min(Mathf.Max(0, count), candidates.Count);

        if (targetCount <= 0)
        {
            return selected;
        }

        if (targetCount >= candidates.Count)
        {
            selected.AddRange(candidates);
            return selected;
        }

        for (int i = 0; i < targetCount; i++)
        {
            int startIndex = Mathf.FloorToInt((float)i * candidates.Count / targetCount);
            int endIndex = Mathf.FloorToInt((float)(i + 1) * candidates.Count / targetCount) - 1;

            startIndex = Mathf.Clamp(startIndex, 0, candidates.Count - 1);
            endIndex = Mathf.Clamp(endIndex, startIndex, candidates.Count - 1);

            int randomIndex = Random.Range(startIndex, endIndex + 1);
            selected.Add(candidates[randomIndex]);
        }

        return selected;
    }

    private int GetGridStep(double relativeTime, double noteDuration)
    {
        if (noteDuration <= 0.0)
        {
            return 0;
        }

        return Mathf.RoundToInt((float)(relativeTime / noteDuration));
    }

    private void SubscribeAttackNoteCreated()
    {
        if (attackTurn == null)
        {
            return;
        }

        attackTurn.OnAttackNoteCreated -= HandleAttackNoteCreated;
        attackTurn.OnAttackNoteCreated += HandleAttackNoteCreated;
    }

    private void UnsubscribeAttackNoteCreated()
    {
        if (attackTurn == null)
        {
            return;
        }

        attackTurn.OnAttackNoteCreated -= HandleAttackNoteCreated;
    }

    private void HandleAttackNoteCreated(NoteData note)
    {
        if (!removeGhostIfRealNoteAppearsOnSameStep)
        {
            return;
        }

        if (note == null || cachedNoteDuration <= 0.0)
        {
            return;
        }

        int realStep = GetGridStep(note.noteRelativeTime, cachedNoteDuration);

        for (int i = preparedGhostNotes.Count - 1; i >= 0; i--)
        {
            GhostNoteData ghostNote = preparedGhostNotes[i];

            if (ghostNote == null)
            {
                preparedGhostNotes.RemoveAt(i);
                continue;
            }

            int ghostStep = GetGridStep(ghostNote.noteRelativeTime, cachedNoteDuration);

            if (ghostStep != realStep)
            {
                continue;
            }

            bool relocated = TryRelocateGhostNote(ghostNote);

            if (!relocated)
            {
                attackTurnRenderer?.RemoveGhostNote(ghostNote.noteId);
                preparedGhostNotes.RemoveAt(i);

                Debug.Log($"DEF_01: 옮길 빈 칸이 없어 유령 노트 제거 / step:{realStep}");
            }
        }
    }

    private bool TryRelocateGhostNote(GhostNoteData ghostNote)
    {
        if (ghostNote == null || attackTurn == null || attackTurnRenderer == null)
        {
            return false;
        }

        List<int> emptySteps = BuildEmptyStepsExceptCurrentGhost(ghostNote);

        if (emptySteps.Count == 0)
        {
            return false;
        }

        int newStep = emptySteps[Random.Range(0, emptySteps.Count)];
        double newRelativeTime = newStep * cachedNoteDuration;

        attackTurnRenderer.RemoveGhostNote(ghostNote.noteId);

        ghostNote.noteRelativeTime = newRelativeTime;
        ghostNote.judgeTime = activeContext.phaseStartDspTime + newRelativeTime;

        AttackSide attackerSide = attackTurn.CurrentSide;

        attackTurnRenderer.SpawnGhostNote(
            ghostNote,
            attackTurn.AttackDuration,
            attackTurnRenderer.GetStartX(attackerSide),
            attackTurnRenderer.GetEndX(attackerSide),
            0f
        );

        Debug.Log($"DEF_01: 실제 노트와 겹친 유령 노트 재배치 / newStep:{newStep}");

        return true;
    }

    private List<int> BuildEmptyStepsExceptCurrentGhost(GhostNoteData currentGhost)
    {
        List<int> emptySteps = new List<int>();
        HashSet<int> blockedSteps = new HashSet<int>();

        IReadOnlyList<NoteData> createdNotes = attackTurn.CurrentCreatedNotes;

        if (createdNotes != null)
        {
            foreach (NoteData note in createdNotes)
            {
                if (note == null)
                {
                    continue;
                }

                blockedSteps.Add(GetGridStep(note.noteRelativeTime, cachedNoteDuration));
            }
        }

        foreach (GhostNoteData ghostNote in preparedGhostNotes)
        {
            if (ghostNote == null || ghostNote == currentGhost)
            {
                continue;
            }

            blockedSteps.Add(GetGridStep(ghostNote.noteRelativeTime, cachedNoteDuration));
        }

        int maxStep = Mathf.FloorToInt((float)(attackTurn.AttackDuration / cachedNoteDuration));

        for (int step = 1; step < maxStep; step++)
        {
            if (blockedSteps.Contains(step))
            {
                continue;
            }

            emptySteps.Add(step);
        }

        return emptySteps;
    }
}