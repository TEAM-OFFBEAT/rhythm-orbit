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
    /// 상대 공격 노트를 수신하고 화면에 생성한 뒤 발행한다.
    /// 유령 이벤트가 실제 노트와의 겹침을 검사할 때 사용한다.
    /// </summary>
    public event System.Action<NoteData> OnAttackNoteReceived;

    // 공격 중 수신한 노트 목록. 외부에서는 읽기만 한다.
    public IReadOnlyList<NoteData> ReceivedAttackNotes => receivedNotes;
    internal IReadOnlyList<NoteData> PendingAttackNotes => pendingNotes;

    /// <summary>
    /// 현재 방어 턴이 진행 중인지 외부에서 읽기 위한 값.
    /// </summary>
    public bool IsRunning => isRunning;

    
    [SerializeField] private AttackTurnRenderer attackTurnRenderer;
    [SerializeField] private float fallbackTransferSpeed = 5f;
    [SerializeField] private int subdivisions = 2;

    private readonly List<NoteData> pendingNotes = new();
    private readonly List<NoteData> receivedNotes = new();

    private readonly List<GhostNoteData> ghostNotes = new();

    [Header("Mode Visuals")]
    [SerializeField] private SpriteRenderer p2DefenseLineRenderer;
    [SerializeField] private Sprite p2DefenseLineSprite44;
    [SerializeField] private Sprite p2DefenseLineSprite34;

    [Header("EVT_DEF_01 Ghost Signal")]
    [SerializeField, Min(0f)] private double ghostPassDisappearDelayMs = 80.0;

    private double currentDefenseStartDspTime;
    private float currentJudgeLineX;
    private float currentAttackStartX;
    private float currentAttackEndX;
    private double currentAttackDuration;

    private NetworkManager networkManager;
    private readonly List<Judgment> judgments = new();
    private bool isRunning;
    private readonly Dictionary<int, int> originalNoteIndices = new();
    private readonly Dictionary<int, Judgment> indexedJudgments = new();
    public int LastJudgedOriginalIndex { get; private set; }
    private bool ghostConversionActive;
    private bool isAiDefense;
    private bool playAiHitSfx;
    private double defenseEndDspTime;
    private AttackSide pendingAttackSide;
    private double pendingAttackDuration;

    private bool isPaused;

    /// <summary>
    /// 박자 모드에 따라 P2 방어 판정선 스프라이트를 전환한다.
    /// </summary>
    public void SetMode(TimeSignatureMode mode)
    {
        if (p2DefenseLineRenderer == null) return;
        p2DefenseLineRenderer.sprite = mode == TimeSignatureMode.ThreeFour ? p2DefenseLineSprite34 : p2DefenseLineSprite44;
    }

    /// <summary>
    /// 방어 턴 Update 루프를 일시정지하거나 재개한다.
    /// </summary>
    public void SetPaused(bool value) => isPaused = value;

    private void Update()
    {
        if (!isRunning) return;
        if (isPaused) return;

        double now = AudioSettings.dspTime;

        if (isAiDefense)
        {
            for (int i = pendingNotes.Count - 1; i >= 0; i--)
            {
                if (now < pendingNotes[i].judgeTime) continue;

                // 싱글모드에서는 자동 방어도 노트 종류에 맞는 타격음을 재생한다.
                ResolveDefenseNote(pendingNotes[i], Judgment.PERFECT, playInputSfx: playAiHitSfx);
            }
        }
        else
        {
            JudgeSystem judgeSystem = JudgeSystem.Instance;

            if (judgeSystem == null)
            {
                return;
            }

            double noteDuration = GetCurrentNoteDurationSeconds();

            for (int i = pendingNotes.Count - 1; i >= 0; i--)
            {
                if (!judgeSystem.HasPassedHitWindow(
                    now,
                    pendingNotes[i].judgeTime,
                    noteDuration))
                {
                    continue;
                }

                ResolveDefenseNote(
                pendingNotes[i],
                Judgment.MISS,
                wasHit: false);
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
    public void Begin(
    IReadOnlyList<NoteData> notes,
    float judgeLineX,
    float attackStartX,
    float attackEndX,
    double attackDuration,
    bool isAiDefense = false,
    NetworkManager networkManager = null,
    double remoteAttackStartDspTime = 0.0,
    bool playAiHitSfx = false)
    {
        this.networkManager = networkManager;

        if (attackTurnRenderer == null)
        {
            Debug.LogError(
                "DefenseTurn: AttackTurnRenderer가 연결되지 않았습니다.");
            return;
        }

        pendingNotes.Clear();
        judgments.Clear();
        originalNoteIndices.Clear();
        indexedJudgments.Clear();
        ghostConversionActive = false;
        this.isAiDefense = isAiDefense;
        this.playAiHitSfx = playAiHitSfx;
        isRunning = true;

        double defenseStartDspTime;

        if (remoteAttackStartDspTime > 0.0)
            defenseStartDspTime =
                remoteAttackStartDspTime + attackDuration;
        else
            defenseStartDspTime = AudioSettings.dspTime;

        defenseEndDspTime = defenseStartDspTime + attackDuration;

        currentDefenseStartDspTime = defenseStartDspTime;
        currentJudgeLineX = judgeLineX;
        currentAttackStartX = attackStartX;
        currentAttackEndX = attackEndX;
        currentAttackDuration = attackDuration;

        ghostNotes.Clear();

        float transferSpeed = CalculateTransferSpeed(
            notes,
            judgeLineX,
            attackStartX,
            attackEndX,
            attackDuration,
            defenseStartDspTime);

        if (notes != null)
        {
            foreach (NoteData note in notes)
            {
                if (note == null) continue;

                note.judgeTime =
                    defenseStartDspTime + note.noteRelativeTime;

                attackTurnRenderer.SetNoteJudgeTime(
                    note.noteId, note.judgeTime);

                pendingNotes.Add(note);
            }
        }

        for (int i = 0; i < pendingNotes.Count; i++)
            originalNoteIndices[pendingNotes[i].noteId] = i;
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
    /// judgeTime을 계산하고 노트를 생성한 뒤 수신 이벤트를 발행한다.
    /// </summary>
    public void OnNoteReceived(
        NoteCreatedPacket packet,
        double localDefenseStart)
    {
        var note = new NoteData
        {
            noteId = packet.noteId,
            noteType = packet.noteType,
            noteRelativeTime = packet.noteRelativeTime,
            judgeTime = localDefenseStart + packet.noteRelativeTime
        };

        if (isRunning && networkManager != null && localDefenseStart == currentDefenseStartDspTime)
        {
            originalNoteIndices[note.noteId] = originalNoteIndices.Count;
            pendingNotes.Add(note);
        }
        else
            receivedNotes.Add(note);

        if (attackTurnRenderer != null)
        {
            Vector3? spawnPos = attackTurnRenderer.SpawnAttackNote(
                pendingAttackSide,
                note,
                pendingAttackDuration);

            attackTurnRenderer.SetNoteJudgeTime(
                note.noteId,
                note.judgeTime);

            if (spawnPos.HasValue)
            {
                HitEffectSpawner.Instance?.Play(
                    note.noteType,
                    spawnPos.Value);
            }
        }

        OnAttackNoteReceived?.Invoke(note);
    }

    /// <summary>
    /// defense phase 시작 DSP 시각에 GameManager가 호출한다.
    /// receivedNotes는 OnNoteReceived에서 이미 judgeTime이 설정되어 있다.
    /// </summary>
    public void BeginTransfer(
    float judgeLineX,
    float attackStartX,
    float attackEndX,
    double attackDuration,
    NetworkManager networkManager,
    double defenseStartDspTime)
    {
        this.networkManager = networkManager;
        isAiDefense = false;

        if (attackTurnRenderer == null)
        {
            Debug.LogError(
                "DefenseTurn: AttackTurnRenderer가 연결되지 않았습니다.");
            return;
        }

        pendingNotes.Clear();
        judgments.Clear();
        originalNoteIndices.Clear();
        indexedJudgments.Clear();
        ghostConversionActive = false;
        isRunning = true;

        defenseEndDspTime = defenseStartDspTime + attackDuration;

        currentDefenseStartDspTime = defenseStartDspTime;
        currentJudgeLineX = judgeLineX;
        currentAttackStartX = attackStartX;
        currentAttackEndX = attackEndX;
        currentAttackDuration = attackDuration;

        ghostNotes.Clear();

        float transferSpeed = CalculateTransferSpeed(
            receivedNotes,
            judgeLineX,
            attackStartX,
            attackEndX,
            attackDuration,
            defenseStartDspTime);

        // judgeTime은 OnNoteReceived에서 이미 계산됨 — 여기서 재계산하지 않는다.
        foreach (NoteData note in receivedNotes)
        {
            if (note == null) continue;

            pendingNotes.Add(note);
        }

        receivedNotes.Clear();

        for (int i = 0; i < pendingNotes.Count; i++)
            originalNoteIndices[pendingNotes[i].noteId] = i;
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

        if (normalTarget == null)
        {
            OnDefenseInputResolved?.Invoke(inputNoteType, false);
            if (ghostConversionActive)
                OnGhostNoteHit?.Invoke(ghostTarget);
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
            double noteDuration = GetCurrentNoteDurationSeconds();

            result = JudgeSystem.Instance != null
                ? JudgeSystem.Instance.Judge(inputTime, normalTarget.judgeTime, noteDuration)
                : Judgment.GOOD;
        }

        ResolveDefenseNote(normalTarget, result);
    }

    /// <summary>
    /// 방어 노트 하나의 판정 결과를 공통 처리한다.
    /// 타격한 노트는 즉시 제거하고, 자동 MISS는 판정 데이터만 정리한다.
    /// </summary>
    private void ResolveDefenseNote(
        NoteData note,
        Judgment judgment,
        bool playInputSfx = true,
        bool wasHit = true)
    {
        if (note == null) return;

        judgments.Add(judgment);
        LastJudgedOriginalIndex = originalNoteIndices.TryGetValue(note.noteId, out int index)
            ? index : judgments.Count - 1;
        indexedJudgments[LastJudgedOriginalIndex] = judgment;

        bool shouldPlayInputSfx = playInputSfx && (!isAiDefense || playAiHitSfx);

        if (shouldPlayInputSfx)
        {
            bool isSuccess = judgment != Judgment.MISS;
            OnDefenseInputResolved?.Invoke(note.noteType, isSuccess);
        }

        OnJudgment?.Invoke(judgment);

        if (judgment != Judgment.MISS)
        {
            Vector3 effectPos = attackTurnRenderer != null
                ? attackTurnRenderer.GetTransferJudgeLineWorldPos()
                : Vector3.zero;

            HitEffectSpawner.Instance?.Play(
                note.noteType,
                effectPos,
                judgment == Judgment.PERFECT);
        }

        // 입력으로 판정한 노트는 결과가 MISS여도 즉시 제거한다.
        if (wasHit)
        {
            attackTurnRenderer?.RemoveNote(note.noteId);
        }

        pendingNotes.Remove(note);

        if (networkManager != null)
        {
            int id = note.noteId;
            Judgment j = judgment;
            bool hit = wasHit;

            networkManager.Send(
                w => PacketSerializer.WriteJudgment(w, id, j, hit));
        }

        Debug.Log(
            $"Defense Judge / noteId:{note.noteId} " +
            $"type:{note.noteType} → {judgment}");
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
            Judgments = BuildPositionalJudgments(),
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
        JudgeSystem judgeSystem = JudgeSystem.Instance;

        if (judgeSystem == null)
        {
            return null;
        }

        NoteData nearest = null;
        double minDist = double.MaxValue;
        double noteDuration = GetCurrentNoteDurationSeconds();

        foreach (var note in pendingNotes)
        {
            if (!judgeSystem.IsNoteActive(inputTime, note.judgeTime))
            {
                continue;
            }

            if (judgeSystem.HasPassedHitWindow(
                inputTime, note.judgeTime, noteDuration))
            {
                continue;
            }

            double dist = System.Math.Abs(
                judgeSystem.CalcOffsetMs(inputTime, note.judgeTime)
            );

            if (dist >= minDist)
            {
                continue;
            }

            minDist = dist;
            nearest = note;
        }

        return nearest;
    }

    /// <summary>
    /// EVT_DEF_01 유령 신호를 활성화하고 현재 방어 턴의 빈 박자에 유령 노트를 생성한다.
    /// </summary>
    public bool ConvertDeliveredNotesToGhosts(int minimumCount, float alpha)
    {
        if (!isRunning || pendingNotes.Count < Mathf.Max(3, minimumCount)) return false;
        ghostConversionActive = true;
        var candidates = new List<NoteData>(pendingNotes);
        int count = candidates.Count == 3 ? 1 : Random.Range(1, 3);
        for (int i = 0; i < count; i++)
        {
            int selected = Random.Range(0, candidates.Count);
            NoteData note = candidates[selected];
            candidates.RemoveAt(selected);
            pendingNotes.Remove(note);
            var ghost = new GhostNoteData
            {
                noteId = note.noteId,
                noteType = note.noteType,
                noteRelativeTime = note.noteRelativeTime,
                judgeTime = note.judgeTime
            };
            ghostNotes.Add(ghost);
            attackTurnRenderer.ConvertNoteToGhost(ghost, alpha);
            // 공격자 관찰 화면에도 제외 위치를 전달한다. 별도의 성공/실패 판정은 아니다.
            networkManager?.Send(w =>
                PacketSerializer.WriteJudgment(w, note.noteId, Judgment.SKIPPED, false));
        }
        return true;
    }

    public void DeactivateGhostSignal()
    {
        ClearGhostNotesInternal();
        ghostConversionActive = false;
    }

    private Judgment[] BuildPositionalJudgments()
    {
        var result = new Judgment[originalNoteIndices.Count];
        for (int i = 0; i < result.Length; i++)
            result[i] = indexedJudgments.TryGetValue(i, out Judgment value) ? value : Judgment.MISS;
        return result;
    }
    private GhostNoteData GetNearestGhostNoteByTime(double inputTime)
    {
        GhostNoteData nearest = null;
        double minDist = double.MaxValue;
        JudgeSystem judgeSystem = JudgeSystem.Instance;

        if (judgeSystem == null)
        {
            return null;
        }

        double noteDuration = GetCurrentNoteDurationSeconds();

        foreach (GhostNoteData ghostNote in ghostNotes)
        {
            if (ghostNote == null)
            {
                continue;
            }

            if (!judgeSystem.IsNoteActive(inputTime, ghostNote.judgeTime))
            {
                continue;
            }

            if (judgeSystem.HasPassedHitWindow(inputTime, ghostNote.judgeTime, noteDuration))
            {
                continue;
            }

            double dist = System.Math.Abs(judgeSystem.CalcOffsetMs(inputTime, ghostNote.judgeTime));

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
    /// 유령 노트가 판정선을 지난 뒤 자동 소멸하는 처리.
    /// 패널티는 적용하지 않는다.
    /// </summary>
    private void ResolveGhostNotePassed(GhostNoteData ghostNote)
    {
        if (ghostNote == null)
        {
            return;
        }

        int index = originalNoteIndices[ghostNote.noteId];
        indexedJudgments[index] = Judgment.SKIPPED;
        LastJudgedOriginalIndex = index;
        OnJudgment?.Invoke(Judgment.SKIPPED);
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

        ghostNotes.Remove(ghostNote);

        attackTurnRenderer?.RemoveGhostNote(
            ghostNote.noteId,
            playDisappear: true);
    }

    private void ClearGhostNotesInternal()
    {
        attackTurnRenderer?.ClearGhostNotes();
        ghostNotes.Clear();
    }

    private double GetCurrentNoteDurationSeconds()
    {
        return RhythmClock.Instance != null
            ? RhythmClock.Instance.GetNoteDuration(subdivisions)
            : 60.0 / 106.0 / System.Math.Max(1, subdivisions);
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
        originalNoteIndices.Clear();
        indexedJudgments.Clear();
        ghostConversionActive = false;

        ClearGhostNotesInternal();

        networkManager = null;
        defenseEndDspTime = 0.0;
        pendingAttackDuration = 0.0;
    }

    /// <summary>
    /// 일반 노트가 있으면 기존처럼 가장 이른 일반 노트로 속도를 정한다.
    /// 일반 노트가 없는 경우에만 유령을 기준으로 속도를 정한다.
    /// </summary>
    private float CalculateTransferSpeed(
        IReadOnlyList<NoteData> notes,
        float judgeLineX,
        float attackStartX,
        float attackEndX,
        double attackDuration,
        double defenseStartDspTime)
    {
        double firstRelativeTime = double.MaxValue;

        if (notes != null)
        {
            foreach (NoteData note in notes)
            {
                if (note == null) continue;

                if (note.noteRelativeTime < firstRelativeTime)
                {
                    firstRelativeTime = note.noteRelativeTime;
                }
            }
        }

        if (firstRelativeTime == double.MaxValue)
        {
            // 빈 공격 턴에서도 유령 이동을 시작할 수 있도록 한다.
            return attackTurnRenderer.GetGhostTransferSpeed(
                judgeLineX,
                defenseStartDspTime,
                fallbackTransferSpeed);
        }

        if (firstRelativeTime <= 0.0)
        {
            return fallbackTransferSpeed;
        }

        double safeDuration = System.Math.Max(0.01, attackDuration);

        float initialX = Mathf.Lerp(
            attackStartX,
            attackEndX,
            (float)(firstRelativeTime / safeDuration));

        return (float)(
            Mathf.Abs(judgeLineX - initialX) / firstRelativeTime);
    }

    
}
