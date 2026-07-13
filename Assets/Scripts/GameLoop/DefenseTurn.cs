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

    private readonly List<NoteData> pendingNotes = new();
    private readonly List<Judgment> judgments = new();
    private bool isRunning;
    private bool isAiDefense;

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
            double missTimeout = JudgeSystem.Instance != null
                ? JudgeSystem.Instance.GoodWindowMs / 1000.0
                : fallbackMissTimeoutMs / 1000.0;

            for (int i = pendingNotes.Count - 1; i >= 0; i--)
            {
                if (now <= pendingNotes[i].judgeTime + missTimeout) continue;
                judgments.Add(Judgment.MISS);
                OnJudgment?.Invoke(Judgment.MISS);
                attackTurnRenderer.RemoveNote(pendingNotes[i].noteId);
                pendingNotes.RemoveAt(i);
            }
        }

        if (isRunning && pendingNotes.Count == 0) EndDefense();
    }

    /// <summary>
    /// 방어 턴을 시작. 노트별 출발 시각을 역산해 judgeLineX에 정확히 gridTime(= defenseStart + noteRelativeTime)에 도착하도록 조정.
    /// transferSpeed는 가장 이른 노트가 defenseStart에 출발해 judgeLineX에 도착하는 속도로 결정.
    /// 이후 노트일수록 이동 거리가 길어 더 빨리 출발하며, 모든 노트가 공격 턴과 동일한 박자 체감으로 도착.
    /// isAiDefense = true 이면 judgeTime 도달 시 자동 PERFECT 처리.
    /// </summary>
    public void Begin(IReadOnlyList<NoteData> notes, float judgeLineX, float attackStartX, float attackEndX, double attackDuration, bool isAiDefense = false)
    {
        if (attackTurnRenderer == null)
        {
            Debug.LogError("DefenseTurn: AttackTurnRenderer가 연결되지 않았습니다.");
            return;
        }

        pendingNotes.Clear();
        judgments.Clear();
        this.isAiDefense = isAiDefense;
        isRunning = true;

        if (notes.Count == 0)
        {
            EndDefense();
            return;
        }

        // 가장 이른 노트 기준으로 transferSpeed 결정 — 이 노트가 defenseStart에 출발해 judgeTime에 도착
        double firstRelativeTime = double.MaxValue;
        foreach (var n in notes)
            if (n.noteRelativeTime < firstRelativeTime) firstRelativeTime = n.noteRelativeTime;

        float firstInitialX = Mathf.Lerp(attackStartX, attackEndX, (float)(firstRelativeTime / attackDuration));
        float transferSpeed = firstRelativeTime > 0.0
            ? Mathf.Abs(judgeLineX - firstInitialX) / (float)firstRelativeTime
            : 5f;

        double defenseStartDspTime = AudioSettings.dspTime;

        foreach (var note in notes)
        {
            // 네트워크 구현 시: defenseStartDspTime = attackStartDspTime + clockOffset + leadTime 으로 교체.
            note.judgeTime = defenseStartDspTime + note.noteRelativeTime;
            attackTurnRenderer.SetNoteJudgeTime(note.noteId, note.judgeTime);
            pendingNotes.Add(note);
        }

        attackTurnRenderer.StartTransfer(judgeLineX, transferSpeed);
    }

    /// <summary>
    /// 입력 이벤트 수신 시 GameManager.OnTap()을 통해 호출.
    /// 인간 방어 모드에서만 유효하며, 가장 가까운 미판정 노트를 판정.
    /// </summary>
    public void OnTap()
    {
        if (!isRunning || isAiDefense) return;

        if (JudgeSystem.Instance == null)
        {
            Debug.LogWarning("JudgeSystem.Instance가 없어 판정을 수행할 수 없습니다.");
            return;
        }

        double inputTime = AudioSettings.dspTime;

        NoteData target = GetNearestNote(inputTime);
        if (target == null) return;

        Judgment result = JudgeSystem.Instance.Judge(inputTime, target.judgeTime);
        judgments.Add(result);
        OnJudgment?.Invoke(result);
        attackTurnRenderer.RemoveNote(target.noteId);
        pendingNotes.Remove(target);

        Debug.Log($"Defense Judge / noteId:{target.noteId} → {result}");

        if (pendingNotes.Count == 0) EndDefense();
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

    private NoteData GetNearestNote(double inputTime)
    {
        NoteData nearest = null;
        double minDist = double.MaxValue;

        foreach (var note in pendingNotes)
        {
            double dist = System.Math.Abs(note.judgeTime - inputTime);
            if (dist >= minDist) continue;
            minDist = dist;
            nearest = note;
        }

        return nearest;
    }
}
