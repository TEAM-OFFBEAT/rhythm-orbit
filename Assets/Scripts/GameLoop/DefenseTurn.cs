using System.Collections.Generic;
using UnityEngine;

public struct DefenseResult
{
    public Judgment[] Judgments;
    public int MissCount;
}

public class DefenseTurn : MonoBehaviour
{
    /// <summary>
    /// 방어 턴이 종료되면 판정 결과와 함께 발행.
    /// </summary>
    public event System.Action<DefenseResult> OnDefenseEnded;

    /// <summary>
    /// 노트 판정 시 결과와 함께 발행.
    /// </summary>
    public event System.Action<Judgment> OnJudgment;

    /// <summary>
    /// 방어 입력 또는 자동 MISS가 성공/실패로 확정됐을 때 발행한다.
    /// GameManager가 구독해 성공이면 HitHigh/HitLow, 실패면 Miss 사운드를 재생한다.
    /// </summary>
    public event System.Action<NoteType, bool> OnDefenseInputResolved;

    /// <summary>
    /// EVT_DEF_01 유령 노트를 플레이어가 타격했을 때 발행한다.
    /// 일반 Judgment에는 포함하지 않는다.
    /// </summary>
    public event System.Action<GhostNoteData> OnGhostNoteHit;

    /// <summary>
    /// EVT_DEF_01 유령 노트가 판정선을 지나 자동 소멸했을 때 발행한다.
    /// </summary>
    public event System.Action<GhostNoteData> OnGhostNotePassed;

    /// <summary>
    /// 방어 턴이 실제로 시작되어 pendingNotes와 transfer 정보가 준비되면 발행한다.
    /// DEF_01 유령 신호처럼 방어 시작 직후 추가 처리가 필요한 이벤트가 구독한다.
    /// </summary>
    public event System.Action OnDefenseBegan;

    /// <summary>
    /// 현재 방어 턴이 진행 중인지 외부에서 읽기 위한 값.
    /// </summary>
    public bool IsRunning => isRunning;
    
    [SerializeField] private AttackTurnRenderer attackTurnRenderer;
    [SerializeField] private double fallbackMissTimeoutMs = 100.0;
    [SerializeField] private float fallbackTransferSpeed = 5f;
    // 판정선 도달 예정 시각보다 이 시간(ms) 이전의 노트는 MISS 판정 및 키 입력을 무시한다.
    [SerializeField] private double noteActivationLeadTimeMs = 300.0;
    [SerializeField] private int subdivisions = 2;
    [SerializeField, Range(0f, 0.5f)] private float defenseTimingWindowRatio = 0.25f;

    private readonly List<NoteData> pendingNotes = new();
    private readonly List<NoteData> receivedNotes = new();

    private readonly List<GhostNoteData> ghostNotes = new();

    [Header("EVT_DEF_01 Ghost Signal")]
    [SerializeField, Min(1)] private int ghostNoteIdBase = 700000;
    [SerializeField, Min(0f)] private double ghostPassDisappearDelayMs = 80.0;

    private int nextGhostNoteId;
    private double currentDefenseStartDspTime;
    private float currentJudgeLineX;
    private float currentAttackStartX;
    private float currentAttackEndX;
    private double currentAttackDuration;

    private NetworkManager networkManager;
    private readonly List<Judgment> judgments = new();
    private bool isRunning;
    private bool isAiDefense;
    private double defenseEndDspTime;
    private AttackSide pendingAttackSide;
    private double pendingAttackDuration;

    private void Update()
    {
        if (!isRunning) return;

        double now = AudioSettings.dspTime;

        if (isAiDefense)
        {
            for (int i = pendingNotes.Count - 1; i >= 0; i--)
            {
                if (now < pendingNotes[i].judgeTime) continue;

                // AI 자동 PERFECT는 실제 입력이 아니므로 타격음 재생 X
                ResolveDefenseNote(pendingNotes[i], Judgment.PERFECT, playInputSfx: false);
            }
        }
        else
        {
            double missTimeout = GetDefenseTimingWindowSeconds();

            for (int i = pendingNotes.Count - 1; i >= 0; i--)
            {
                if (now < pendingNotes[i].judgeTime - noteActivationLeadTimeMs / 1000.0) continue;
                if (now <= pendingNotes[i].judgeTime + missTimeout) continue;

                ResolveDefenseNote(pendingNotes[i], Judgment.MISS);
            }
        }

        UpdateGhostNotes(now);
        if (isRunning && pendingNotes.Count == 0 && now >= defenseEndDspTime) EndDefense();
    }

    /// <summary>
    /// 방어 턴을 시작. 노트별 출발 시각을 역산해 judgeLineX에 정확히 gridTime(= defenseStart + noteRelativeTime)에 도착하도록 조정.
    /// transferSpeed는 가장 이른 노트가 defenseStart에 출발해 judgeLineX에 도착하는 속도로 결정.
    /// 이후 노트일수록 이동 거리가 길어 더 빨리 출발하며, 모든 노트가 공격 턴과 동일한 박자 체감으로 도착.
    /// isAiDefense = true 이면 judgeTime 도달 시 자동 PERFECT 처리.
    /// remoteAttackStartDspTime이 0이 아닌 경우, AudioSettings.dspTime 대신 이 값을 기반으로 defenseStartDspTime을 결정적으로 계산한다.
    /// </summary>
    public void Begin(IReadOnlyList<NoteData> notes, float judgeLineX, float attackStartX, float attackEndX,
        double attackDuration, bool isAiDefense = false, NetworkManager networkManager = null,
        double remoteAttackStartDspTime = 0.0)
    {
        this.networkManager = networkManager;
        if (attackTurnRenderer == null)
        {
            Debug.LogError("DefenseTurn: AttackTurnRenderer가 연결되지 않았습니다.");
            return;
        }

        pendingNotes.Clear();
        judgments.Clear();
        this.isAiDefense = isAiDefense;
        isRunning = true;

        if (notes.Count == 0) return;

        // 가장 이른 노트 기준으로 transferSpeed 결정 — 이 노트가 defenseStart에 출발해 judgeTime에 도착
        double firstRelativeTime = double.MaxValue;
        foreach (var n in notes)
            if (n.noteRelativeTime < firstRelativeTime) firstRelativeTime = n.noteRelativeTime;

        float firstInitialX = Mathf.Lerp(attackStartX, attackEndX, (float)(firstRelativeTime / attackDuration));
        float transferSpeed = firstRelativeTime > 0.0
            ? Mathf.Abs(judgeLineX - firstInitialX) / (float)firstRelativeTime
            : fallbackTransferSpeed;

        double defenseStartDspTime;
        if (remoteAttackStartDspTime > 0.0)
            defenseStartDspTime = remoteAttackStartDspTime + attackDuration;
        else
            defenseStartDspTime = AudioSettings.dspTime;
        defenseEndDspTime = defenseStartDspTime + attackDuration;

        currentDefenseStartDspTime = defenseStartDspTime;
        currentJudgeLineX = judgeLineX;
        currentAttackStartX = attackStartX;
        currentAttackEndX = attackEndX;
        currentAttackDuration = attackDuration;
        nextGhostNoteId = ghostNoteIdBase;

        ghostNotes.Clear();

        foreach (var note in notes)
        {
            note.judgeTime = defenseStartDspTime + note.noteRelativeTime;
            attackTurnRenderer.SetNoteJudgeTime(note.noteId, note.judgeTime);
            pendingNotes.Add(note);
        }

        attackTurnRenderer.StartTransfer(judgeLineX, transferSpeed);
        OnDefenseBegan?.Invoke();
    }

    /// <summary>
    /// GameManager의 DSP 페이즈 루프가 원격 공격 phase 시작 시 호출한다.
    /// 이후 OnNoteReceived()에서 즉시 스폰할 수 있도록 컨텍스트를 준비한다.
    /// </summary>
    public void PrepareForIncomingAttack(AttackSide attackSide, double attackDuration)
    {
        pendingAttackSide     = attackSide;
        pendingAttackDuration = attackDuration;
        receivedNotes.Clear();
    }

    /// <summary>
    /// NOTE_CREATED 수신 시 GameManager를 통해 호출.
    /// judgeTime을 즉시 계산해 설정하고 노트를 스폰한다.
    /// localDefenseStart는 localAttackStartDspTime + currentTurnDuration, 즉 방어 페이즈 시작 시각.
    /// </summary>
    public void OnNoteReceived(NoteCreatedPacket packet, double localDefenseStart)
    {
        var note = new NoteData
        {
            noteId           = packet.noteId,
            noteType         = packet.noteType,
            noteRelativeTime = packet.noteRelativeTime,
            judgeTime        = localDefenseStart + packet.noteRelativeTime
        };
        receivedNotes.Add(note);
        if (attackTurnRenderer != null)
        {
            attackTurnRenderer.SpawnAttackNote(pendingAttackSide, note, pendingAttackDuration);
            attackTurnRenderer.SetNoteJudgeTime(note.noteId, note.judgeTime);
        }
    }

    /// <summary>
    /// defense phase 시작 DSP 시각에 GameManager가 호출한다.
    /// receivedNotes는 OnNoteReceived에서 이미 judgeTime이 설정되어 있다.
    /// </summary>
    public void BeginTransfer(float judgeLineX, float attackStartX, float attackEndX,
        double attackDuration, NetworkManager networkManager, double defenseStartDspTime)
    {
        this.networkManager = networkManager;
        isAiDefense  = false;

        if (attackTurnRenderer == null)
        {
            Debug.LogError("DefenseTurn: AttackTurnRenderer가 연결되지 않았습니다.");
            return;
        }

        pendingNotes.Clear();
        judgments.Clear();
        isRunning       = true;
        defenseEndDspTime = defenseStartDspTime + attackDuration;

        currentDefenseStartDspTime = defenseStartDspTime;
        currentJudgeLineX = judgeLineX;
        currentAttackStartX = attackStartX;
        currentAttackEndX = attackEndX;
        currentAttackDuration = attackDuration;
        nextGhostNoteId = ghostNoteIdBase;

        ghostNotes.Clear();

        if (receivedNotes.Count == 0)
        {
            receivedNotes.Clear();
            return;
        }

        double firstRelativeTime = double.MaxValue;
        foreach (var n in receivedNotes)
            if (n.noteRelativeTime < firstRelativeTime) firstRelativeTime = n.noteRelativeTime;

        float firstInitialX  = Mathf.Lerp(attackStartX, attackEndX,
            (float)(firstRelativeTime / System.Math.Max(0.01, attackDuration)));
        float transferSpeed  = firstRelativeTime > 0.0
            ? Mathf.Abs(judgeLineX - firstInitialX) / (float)firstRelativeTime
            : fallbackTransferSpeed;

        // judgeTime은 OnNoteReceived에서 이미 계산됨 — 여기서 재계산하지 않는다.
        foreach (var note in receivedNotes)
            pendingNotes.Add(note);

        receivedNotes.Clear();
        attackTurnRenderer.StartTransfer(judgeLineX, transferSpeed);
        OnDefenseBegan?.Invoke();
    }

    /// <summary>
    /// 방어 턴 입력을 처리한다.
    /// 시간상 가장 가까운 활성 노트를 먼저 찾고, 키가 맞으면 JudgeSystem에 타이밍 판정을 위임한다.
    /// 키가 틀리면 즉시 MISS로 처리한다.
    /// 또한 유령 노트까지 입력 후보로 보되,
    /// 유령 노트가 선택되면 일반 Judgement에는 포함하지 않고 별도 처리한다.
    /// </summary>
    /// <summary>
    public void OnTap(NoteType inputNoteType)
    {
        if (!isRunning || isAiDefense) return;

        double inputTime = AudioSettings.dspTime;

        NoteData normalTarget = GetNearestNoteByTime(inputTime);
        GhostNoteData ghostTarget = GetNearestGhostNoteByTime(inputTime);

        if (ghostTarget != null && ShouldUseGhostTarget(inputTime, normalTarget, ghostTarget))
        {
            ResolveGhostNoteHit(ghostTarget);
            return;
        }

        // 방어할 활성 노트가 없는데 누른 경우.
        // 게임 로직상 추가 감점은 하지 않고 사운드만 Miss로 처리한다.
        if (normalTarget == null)
        {
            OnDefenseInputResolved?.Invoke(inputNoteType, false);
            return;
        }

        bool isKeySuccess = inputNoteType == normalTarget.noteType;

        Judgment result;

        if (!isKeySuccess)
        {
            result = Judgment.MISS;
        }
        else
        {
            double noteDuration = RhythmClock.Instance != null
                ? RhythmClock.Instance.GetNoteDuration(subdivisions)
                : fallbackMissTimeoutMs / 1000.0 / defenseTimingWindowRatio;

            result = JudgeSystem.Instance != null
                ? JudgeSystem.Instance.Judge(inputTime, normalTarget.judgeTime, noteDuration)
                : Judgment.GOOD;
        }

        ResolveDefenseNote(normalTarget, result);
    }

    /// <summary>
    /// 방어 노트 하나의 판정 결과를 공통 처리한다.
    /// 실제 방어 입력에서 발생한 판정만 입력 결과 사운드 이벤트를 발행한다.
    /// 공격자 미러뷰나 AI 자동 판정은 시각 동기화용이므로 타격음을 재생하지 않는다.
    /// </summary>
    private void ResolveDefenseNote(NoteData note, Judgment judgment, bool playInputSfx = true)
    {
        if (note == null) return;

        judgments.Add(judgment);

        bool shouldPlayInputSfx = playInputSfx && !isAiDefense;

        if (shouldPlayInputSfx)
        {
            bool isSuccess = judgment != Judgment.MISS;
            OnDefenseInputResolved?.Invoke(note.noteType, isSuccess);
        }

        OnJudgment?.Invoke(judgment);

        attackTurnRenderer.RemoveNote(note.noteId);
        pendingNotes.Remove(note);

        if (networkManager != null)
        {
            int id = note.noteId;
            Judgment j = judgment;
            networkManager.Send(w => PacketSerializer.WriteJudgment(w, id, j));
        }

        Debug.Log($"Defense Judge / noteId:{note.noteId} type:{note.noteType} → {judgment}");
    }

    private void EndDefense()
    {
        isRunning = false;

        int missCount = 0;
        foreach (var j in judgments)
        {
            if (j == Judgment.MISS) missCount++;
        }

        var result = new DefenseResult
        {
            Judgments = judgments.ToArray(),
            MissCount = missCount,
        };

        Debug.Log($"Defense End / total:{judgments.Count}, miss:{missCount}");
        OnDefenseEnded?.Invoke(result);
    }

    /// <summary>
    /// 현재 입력 시각과 가장 가까운 활성 노트를 반환한다.
    /// 아직 활성화 구간에 들어오지 않은 노트는 입력 대상으로 보지 않는다.
    /// </summary>
    private NoteData GetNearestNoteByTime(double inputTime)
    {
        NoteData nearest = null;
        double minDist = double.MaxValue;

        foreach (var note in pendingNotes)
        {
            if (inputTime < note.judgeTime - noteActivationLeadTimeMs / 1000.0) continue;

            double dist = System.Math.Abs(note.judgeTime - inputTime);

            if (dist >= minDist) continue;

            minDist = dist;
            nearest = note;
        }

        return nearest;
    }

    /// <summary>
    /// 현재 BPM과 subdivisions 기준으로 방어 성공 판정 허용 시간을 초 단위로 반환한다.
    /// 기본 기준은 노트 간격의 ±25%다.
    /// </summary>
    private double GetDefenseTimingWindowSeconds()
    {
        if (RhythmClock.Instance == null)
        {
            return fallbackMissTimeoutMs / 1000.0;
        }

        return RhythmClock.Instance.GetNoteDuration(subdivisions) * defenseTimingWindowRatio;
    }

    /// <summary>
    /// EVT_DEF_01 유령 신호를 활성화하고 현재 방어 턴의 빈 박자에 유령 노트를 생성한다.
    /// </summary>
    public void ActivateGhostSignal(int ghostCount, float ghostAlpha)
    {
        if (!isRunning || isAiDefense)
        {
            return;
        }

        if (attackTurnRenderer == null)
        {
            Debug.LogWarning("DefenseTurn: AttackTurnRenderer가 없어 유령 노트를 생성할 수 없음.");
            return;
        }

        ClearGhostNotesInternal();

        List<int> candidateSteps = BuildGhostCandidateGridSteps();

        if (candidateSteps.Count == 0)
        {
            Debug.Log("DefenseTurn: 유령 노트를 생성할 빈 박자가 없음.");
            return;
        }

        Shuffle(candidateSteps);

        int spawnCount = Mathf.Min(Mathf.Max(0, ghostCount), candidateSteps.Count);
        double noteDuration = GetCurrentNoteDurationSeconds();

        for (int i = 0; i < spawnCount; i++)
        {
            int step = candidateSteps[i];
            double relativeTime = step * noteDuration;

            GhostNoteData ghostNote = new GhostNoteData
            {
                noteId = nextGhostNoteId++,
                noteType = Random.value < 0.5f ? NoteType.HIGH : NoteType.LOW,
                noteRelativeTime = relativeTime,
                judgeTime = currentDefenseStartDspTime + relativeTime
            };

            ghostNotes.Add(ghostNote);

            attackTurnRenderer.SpawnGhostNote(
                ghostNote,
                currentAttackDuration,
                currentAttackStartX,
                currentAttackEndX,
                ghostAlpha
            );

            Debug.Log(
                $"DefenseTurn: Ghost note 생성 / " +
                $"id:{ghostNote.noteId}, type:{ghostNote.noteType}, step:{step}, rel:{relativeTime:0.000}"
            );
        }
    }

    /// <summary>
    /// EVT_DEF_01 유령 신호를 종료하고 남은 유령 노트를 제거한다.
    /// </summary>
    public void DeactivateGhostSignal()
    {
        ClearGhostNotesInternal();
    }

    /// <summary>
    /// DEF_01 진입 단계에서 미리 생성된 유령 노트를
    /// 방어 턴 입력 후보로 등록한다.
    /// 시각 오브젝트는 이미 AttackTurnRenderer에 생성되어 있으므로 여기서는 새로 Spawn하지 않는다.
    /// </summary>
    public void SetGhostSignalNotes(IReadOnlyList<GhostNoteData> preparedNotes)
    {
        ghostNotes.Clear();

        if (!isRunning || isAiDefense)
        {
            return;
        }

        if (preparedNotes == null)
        {
            return;
        }

        foreach (GhostNoteData ghostNote in preparedNotes)
        {
            if (ghostNote == null)
            {
                continue;
            }

            ghostNotes.Add(ghostNote);
        }

        Debug.Log($"DefenseTurn: Ghost signal input 등록 / count:{ghostNotes.Count}");
    }

    /// <summary>
    /// 실제 노트와 겹치지 않는 빈 grid step 목록을 만든다.
    /// </summary>
    private List<int> BuildGhostCandidateGridSteps()
    {
        List<int> candidates = new List<int>();
        HashSet<int> occupiedSteps = new HashSet<int>();

        double noteDuration = GetCurrentNoteDurationSeconds();

        if (noteDuration <= 0.0 || currentAttackDuration <= 0.0)
        {
            return candidates;
        }

        foreach (NoteData note in pendingNotes)
        {
            if (note == null)
            {
                continue;
            }

            int step = GetNearestGridStep(note.noteRelativeTime, noteDuration);
            occupiedSteps.Add(step);
        }

        int maxStep = Mathf.FloorToInt((float)(currentAttackDuration / noteDuration));

        // 0번째 칸과 마지막 칸은 시작/끝 경계라 제외.
        for (int step = 1; step < maxStep; step++)
        {
            if (occupiedSteps.Contains(step))
            {
                continue;
            }

            candidates.Add(step);
        }

        return candidates;
    }

    private GhostNoteData GetNearestGhostNoteByTime(double inputTime)
    {
        GhostNoteData nearest = null;
        double minDist = double.MaxValue;
        double hitWindow = GetDefenseTimingWindowSeconds();

        foreach (GhostNoteData ghostNote in ghostNotes)
        {
            if (ghostNote == null)
            {
                continue;
            }

            if (inputTime < ghostNote.judgeTime - noteActivationLeadTimeMs / 1000.0)
            {
                continue;
            }

            if (inputTime > ghostNote.judgeTime + hitWindow)
            {
                continue;
            }

            double dist = System.Math.Abs(ghostNote.judgeTime - inputTime);

            if (dist >= minDist)
            {
                continue;
            }

            minDist = dist;
            nearest = ghostNote;
        }

        return nearest;
    }

    /// <summary>
    /// 실제 노트와 유령 노트가 둘 다 입력 후보일 때 어느 쪽으로 처리할지 결정한다.
    /// 더 가까운 쪽을 우선한다.
    /// </summary>
    private bool ShouldUseGhostTarget(double inputTime, NoteData normalTarget, GhostNoteData ghostTarget)
    {
        if (ghostTarget == null)
        {
            return false;
        }

        if (normalTarget == null)
        {
            return true;
        }

        double normalDist = System.Math.Abs(normalTarget.judgeTime - inputTime);
        double ghostDist = System.Math.Abs(ghostTarget.judgeTime - inputTime);

        return ghostDist <= normalDist;
    }

    /// <summary>
    /// 유령 노트를 타격했을 때 처리한다.
    /// 실제 방어 Judgment에는 포함하지 않는다.
    /// </summary>
    private void ResolveGhostNoteHit(GhostNoteData ghostNote)
    {
        if (ghostNote == null)
        {
            return;
        }

        RemoveGhostNoteInternal(ghostNote);
        OnGhostNoteHit?.Invoke(ghostNote);

        Debug.Log($"DefenseTurn: Ghost note hit / id:{ghostNote.noteId}, type:{ghostNote.noteType}");
    }

    /// <summary>
    /// 유령 노트가 판정선을 지난 뒤 자동 소멸하는 처리.
    /// 패널티는 적용하지 않는다.
    /// </summary>
    private void ResolveGhostNotePassed(GhostNoteData ghostNote)
    {
        if (ghostNote == null)
        {
            return;
        }

        RemoveGhostNoteInternal(ghostNote);
        OnGhostNotePassed?.Invoke(ghostNote);

        Debug.Log($"DefenseTurn: Ghost note passed / id:{ghostNote.noteId}");
    }

    private void UpdateGhostNotes(double now)
    {
        double disappearDelaySeconds = ghostPassDisappearDelayMs / 1000.0;

        for (int i = ghostNotes.Count - 1; i >= 0; i--)
        {
            GhostNoteData ghostNote = ghostNotes[i];

            if (ghostNote == null)
            {
                ghostNotes.RemoveAt(i);
                continue;
            }

            if (now < ghostNote.judgeTime + disappearDelaySeconds)
            {
                continue;
            }

            ResolveGhostNotePassed(ghostNote);
        }
    }

    private void RemoveGhostNoteInternal(GhostNoteData ghostNote)
    {
        if (ghostNote == null)
        {
            return;
        }

        attackTurnRenderer?.RemoveGhostNote(ghostNote.noteId);
        ghostNotes.Remove(ghostNote);
    }

    private void ClearGhostNotesInternal()
    {
        attackTurnRenderer?.ClearGhostNotes();
        ghostNotes.Clear();
    }

    private double GetCurrentNoteDurationSeconds()
    {
        if (RhythmClock.Instance != null)
        {
            return RhythmClock.Instance.GetNoteDuration(subdivisions);
        }

        return fallbackMissTimeoutMs / 1000.0 / defenseTimingWindowRatio;
    }

    private int GetNearestGridStep(double relativeTime, double noteDuration)
    {
        if (noteDuration <= 0.0)
        {
            return 0;
        }

        return Mathf.RoundToInt((float)(relativeTime / noteDuration));
    }

    private void Shuffle(List<int> values)
    {
        for (int i = 0; i < values.Count; i++)
        {
            int randomIndex = Random.Range(i, values.Count);
            (values[i], values[randomIndex]) = (values[randomIndex], values[i]);
        }
    }

    /// <summary>
    /// 재시작 또는 씬 이동을 위해 진행 중인 방어 턴을 강제로 정리한다.
    /// OnDefenseEnded는 호출하지 않는다.
    /// </summary>
    public void CancelDefense()
    {
        isRunning = false;
        isAiDefense = false;

        pendingNotes.Clear();
        receivedNotes.Clear();
        judgments.Clear();

        ClearGhostNotesInternal();

        networkManager = null;
        defenseEndDspTime = 0.0;
        pendingAttackDuration = 0.0;
    }
}
