using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class AttackTurn : SceneSingleton<AttackTurn>
{
    private const int HalfBeatStepsPerMeasure = 8;
    private const int FirstPlayableGridStep=1;

    [Header("Attack Settings")]
    [SerializeField] private int minTargetTapCount = 2;
    [SerializeField] private int maxTargetTapCount = 4;
    [SerializeField] private int attackMeasureCount = 1;
    [SerializeField] private double gridToleranceMs = 100.0;
    [SerializeField] private bool blockInputAfterFailure = true;

    private readonly List<NoteData> createdNotes = new();
    private readonly List<double> opponentDemoRelativeTimes = new();

    private AttackTurnRenderer attackTurnRenderer;
    private TMP_Text attackStatusLabel;

    private AttackSide currentSide;
    private double attackStartDspTime;
    private double attackDuration;
    private int gridStepCount;
    private int targetTapCount;
    private int nextNoteId;
    private int nextOpponentDemoIndex;
    private bool isRunning;
    private bool isLocalPlayerAttack;
    private bool hasFailed;

    private readonly int[] opponentDemoGridSteps =
    {
        2,
        4,
        6
    };

    private double NoteDuration
    {
        get
        {
            if (RhythmClock.Instance == null)
            {
                Debug.LogWarning("RhythmClock.Instance가 없습니다. 기본 BPM 120 기준으로 계산합니다.");
                return 30.0 / 120.0;
            }

            return RhythmClock.Instance.GetNoteDuration();
        }
    }

    /// <summary>
    /// DevAttackPanel에서 공격 테스트 UI를 등록.
    /// </summary>
    public void RegisterView(AttackTurnRenderer renderer, TMP_Text statusLabel)
    {
        attackTurnRenderer = renderer;
        attackStatusLabel = statusLabel;
        ShowStatus("ATTACK TEST READY");
    }

    /// <summary>
    /// DevAttackPanel 제거 시 등록된 UI 참조를 해제.
    /// </summary>
    public void UnregisterView(AttackTurnRenderer renderer, TMP_Text statusLabel)
    {
        if (attackTurnRenderer == renderer)
        {
            attackTurnRenderer = null;
        }

        if (attackStatusLabel == statusLabel)
        {
            attackStatusLabel = null;
        }
    }

    /// <summary>
    /// 개발용 P1 공격 테스트 시작. DevAttackPanel 버튼에서 호출.
    /// </summary>
    public void StartLocalPlayerAttack()
    {
        int safeMin = Mathf.Max(1, minTargetTapCount);
        int safeMax = Mathf.Max(safeMin, maxTargetTapCount);
        safeMax = Mathf.Min(safeMax, MaxPlayableTapCount);

        int randomTargetCount = Random.Range(safeMin, safeMax + 1);

        StartAttack(AttackSide.P1, true, randomTargetCount);
    }

    /// <summary>
    /// 개발용 P2 공격 데모 시작. DevAttackPanel 버튼에서 호출.
    /// </summary>
    public void StartOpponentAttackDemo()
    {
        StartAttack(AttackSide.P2, false, opponentDemoGridSteps.Length);

        opponentDemoRelativeTimes.Clear();
        double noteDuration = NoteDuration;

        foreach (int step in opponentDemoGridSteps)
        {
            double relativeTime = step * noteDuration;

            if (relativeTime <= attackDuration)
            {
                opponentDemoRelativeTimes.Add(relativeTime);
            }
        }

        targetTapCount = opponentDemoRelativeTimes.Count;
        nextOpponentDemoIndex = 0;

        ShowStatus($"P2 ATTACK DEMO START / TARGET: {targetTapCount}");
    }

    /// <summary>
    /// Tap Action 입력 시 호출. P1 공격 중이면 noteRelativeTime을 생성.
    /// </summary>
    public void OnTap()
    {
        if (!isRunning || !isLocalPlayerAttack) return;
        if (blockInputAfterFailure && hasFailed) return;

        double now = AudioSettings.dspTime;
        double relativeTime = now - attackStartDspTime;

        if (relativeTime < 0.0 || relativeTime > attackDuration)
        {
            FailAttack("ATTACK FAILED: OUT OF TIME");
            return;
        }

        if (createdNotes.Count >= targetTapCount)
        {
            FailAttack("ATTACK FAILED: TOO MANY TAPS");
            return;
        }

        double noteDuration = NoteDuration;

        if (!BeatCalculator.IsOnGrid(relativeTime, noteDuration, gridToleranceMs))
        {
            FailAttack("ATTACK FAILED: OFF GRID");
            return;
        }

        double snappedRelativeTime = BeatCalculator.SnapToBeat(relativeTime, noteDuration);
        snappedRelativeTime = ClampToAttackDuration(snappedRelativeTime);

        int gridStepIndex = GetGridStepIndex(snappedRelativeTime, noteDuration);

        if (!IsPlayableGridStep(gridStepIndex))
        {
            FailAttack("ATTACK FAILED: EMPTY BEAT");
            return;
        }

        if (HasDuplicateTiming(snappedRelativeTime))
        {
            FailAttack("ATTACK FAILED: DUPLICATE TIMING");
            return;
        }

        CreateAttackNote(snappedRelativeTime);
        ShowStatus($"P1 ATTACK {createdNotes.Count} / {targetTapCount}");
    }

    private void Update()
    {
        if (!isRunning) return;

        double elapsed = AudioSettings.dspTime - attackStartDspTime;

        if (!isLocalPlayerAttack)
        {
            UpdateOpponentDemo(elapsed);
        }

        if (elapsed >= attackDuration)
        {
            EndAttack();
        }
    }

    private void StartAttack(AttackSide side, bool localPlayerAttack, int requiredTapCount)
    {
        currentSide = side;
        isLocalPlayerAttack = localPlayerAttack;
        isRunning = true;
        hasFailed = false;
        nextNoteId = 0;
        nextOpponentDemoIndex = 0;
        createdNotes.Clear();
        opponentDemoRelativeTimes.Clear();

        double noteDuration = NoteDuration;
        gridStepCount = Mathf.Max(1, attackMeasureCount) * HalfBeatStepsPerMeasure;
        attackDuration = noteDuration * gridStepCount;
        attackStartDspTime = AudioSettings.dspTime;

        targetTapCount = Mathf.Clamp(requiredTapCount, 1, MaxPlayableTapCount);
        

        if (attackTurnRenderer != null)
        {
            attackTurnRenderer.BeginAttackVisual(currentSide, attackStartDspTime, attackDuration, gridStepCount);
        }
        else
        {
            Debug.LogWarning("AttackTurnRenderer가 등록되지 않았습니다.");
        }

        string attackerLabel = currentSide == AttackSide.P1 ? "P1" : "P2";
        ShowStatus($"{attackerLabel} ATTACK START / TARGET: {targetTapCount}");
    }

    private void UpdateOpponentDemo(double elapsed)
    {
        while (nextOpponentDemoIndex < opponentDemoRelativeTimes.Count &&
               elapsed >= opponentDemoRelativeTimes[nextOpponentDemoIndex])
        {
            CreateAttackNote(opponentDemoRelativeTimes[nextOpponentDemoIndex]);
            nextOpponentDemoIndex++;

            ShowStatus($"P2 ATTACK DEMO {createdNotes.Count} / {targetTapCount}");
        }
    }

    private void CreateAttackNote(double noteRelativeTime)
    {
        NoteData note = new NoteData
        {
            noteId = nextNoteId++,
            noteRelativeTime = noteRelativeTime
            // judgeTime은 여기서 설정하지 않는다.
            // 방어자 쪽 transfer 이후 defenseStartDspTime + noteRelativeTime으로 계산한다.
        };

        createdNotes.Add(note);

        if (attackTurnRenderer != null)
        {
            attackTurnRenderer.SpawnAttackNote(currentSide, note, attackDuration);
        }

        Debug.Log($"Attack Note Created / id: {note.noteId}, relativeTime: {note.noteRelativeTime:0.000}s");
    }

    private void EndAttack()
    {
        if (!isRunning) return;

        isRunning = false;

        if (attackTurnRenderer != null)
        {
            attackTurnRenderer.StopLine();
        }

        if (!hasFailed && createdNotes.Count < targetTapCount)
        {
            FailAttack("ATTACK FAILED: NOT ENOUGH TAPS");
        }

        string attackerLabel = currentSide == AttackSide.P1 ? "P1" : "P2";
        string result = hasFailed ? "FAILED" : "COMPLETE";

        ShowStatus($"{attackerLabel} ATTACK {result} / NOTES: {createdNotes.Count}");
        Debug.Log($"Attack End / result: {result}, noteCount: {createdNotes.Count}");
    }

    private void FailAttack(string message)
    {
        hasFailed = true;
        ShowStatus(message);
        Debug.LogWarning(message);
    }

    private bool HasDuplicateTiming(double snappedRelativeTime)
    {
        const double epsilon = 0.001;

        foreach (NoteData note in createdNotes)
        {
            if (System.Math.Abs(note.noteRelativeTime - snappedRelativeTime) <= epsilon)
            {
                return true;
            }
        }

        return false;
    }

    private double ClampToAttackDuration(double relativeTime)
    {
        if (relativeTime < 0.0) return 0.0;
        if (relativeTime > attackDuration) return attackDuration;
        return relativeTime;
    }

    private void ShowStatus(string message)
    {
        if (attackStatusLabel != null)
        {
            attackStatusLabel.text = message;
        }
    }
    
    private int LastPlayableGridStep => Mathf.Max(FirstPlayableGridStep, gridStepCount - 1);

    private int MaxPlayableTapCount => LastPlayableGridStep - FirstPlayableGridStep + 1;

    private int GetGridStepIndex(double relativeTime, double noteDuration)
    {
        if (noteDuration <= 0.0) return 0;
        return Mathf.RoundToInt((float)(relativeTime / noteDuration));
    }

    private bool IsPlayableGridStep(int gridStepIndex)
    {
        return gridStepIndex >= FirstPlayableGridStep &&
            gridStepIndex <= LastPlayableGridStep;
    }

}