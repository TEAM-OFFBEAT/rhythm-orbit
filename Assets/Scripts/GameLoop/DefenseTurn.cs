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

    [SerializeField] private AttackTurnRenderer attackTurnRenderer;
    [SerializeField] private double fallbackMissTimeoutMs = 100.0;
    [SerializeField] private float fallbackTransferSpeed = 5f;
    // 판정선 도달 예정 시각보다 이 시간(초) 이전의 노트는 MISS 판정 및 키 입력을 무시한다.
    [SerializeField] private double noteActivationLeadTimeS = 1.0;

    private readonly List<NoteData> pendingNotes = new();
    private readonly List<NoteData> receivedNotes = new();
    private NetworkManager networkManager;
    private readonly List<Judgment> judgments = new();
    private bool isRunning;
    private bool isAiDefense;
    private bool isMirrorView;
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
                judgments.Add(Judgment.PERFECT);
                OnJudgment?.Invoke(Judgment.PERFECT);
                attackTurnRenderer.RemoveNote(pendingNotes[i].noteId);
                pendingNotes.RemoveAt(i);
            }
        }
        else
        {
            if (!isMirrorView)
            {
                double missTimeout = JudgeSystem.Instance != null
                    ? JudgeSystem.Instance.GoodWindowMs / 1000.0
                    : fallbackMissTimeoutMs / 1000.0;

                for (int i = pendingNotes.Count - 1; i >= 0; i--)
                {
                    if (now < pendingNotes[i].judgeTime - noteActivationLeadTimeS) continue;
                    if (now <= pendingNotes[i].judgeTime + missTimeout) continue;
                    int missedNoteId = pendingNotes[i].noteId;
                    judgments.Add(Judgment.MISS);
                    OnJudgment?.Invoke(Judgment.MISS);
                    attackTurnRenderer.RemoveNote(missedNoteId);
                    pendingNotes.RemoveAt(i);

                    if (networkManager != null)
                        networkManager.Send(w => PacketSerializer.WriteJudgment(w, missedNoteId, Judgment.MISS));
                }
            }
        }

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
        isMirrorView = NetworkManager.Instance != null && networkManager == null;
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

        foreach (var note in notes)
        {
            note.judgeTime = defenseStartDspTime + note.noteRelativeTime;
            attackTurnRenderer.SetNoteJudgeTime(note.noteId, note.judgeTime);
            pendingNotes.Add(note);
        }

        attackTurnRenderer.StartTransfer(judgeLineX, transferSpeed);
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
        isMirrorView = false;
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
    }

    /// <summary>
    /// 입력 이벤트 수신 시 GameManager.OnTapHigh() / OnTapLow()를 통해 호출.
    /// 인간 방어 모드에서만 유효하며, 입력된 noteType과 일치하는 가장 가까운 미판정 노트를 판정.
    /// </summary>
    public void OnTap(NoteType noteType)
    {
        if (!isRunning || isAiDefense) return;

        if (JudgeSystem.Instance == null)
        {
            Debug.LogWarning("JudgeSystem.Instance가 없어 판정을 수행할 수 없습니다.");
            return;
        }

        double inputTime = AudioSettings.dspTime;

        NoteData target = GetNearestNote(inputTime, noteType);
        if (target == null) return;

        Judgment result = JudgeSystem.Instance.Judge(inputTime, target.judgeTime);
        judgments.Add(result);
        OnJudgment?.Invoke(result);
        attackTurnRenderer.RemoveNote(target.noteId);
        pendingNotes.Remove(target);

        if (networkManager != null)
        {
            int id = target.noteId;
            Judgment j = result;
            networkManager.Send(w => PacketSerializer.WriteJudgment(w, id, j));
        }

        Debug.Log($"Defense Judge / noteId:{target.noteId} type:{target.noteType} → {result}");
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

    private NoteData GetNearestNote(double inputTime, NoteType noteType)
    {
        NoteData nearest = null;
        double minDist = double.MaxValue;

        foreach (var note in pendingNotes)
        {
            if (note.noteType != noteType) continue;
            // 아직 활성화 구간에 진입하지 않은 노트는 키 입력 대상에서 제외
            if (inputTime < note.judgeTime - noteActivationLeadTimeS) continue;
            double dist = System.Math.Abs(note.judgeTime - inputTime);
            if (dist >= minDist) continue;
            minDist = dist;
            nearest = note;
        }

        return nearest;
    }
}
