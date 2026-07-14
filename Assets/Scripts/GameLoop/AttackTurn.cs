using System.Collections.Generic;
// using TMPro;
using UnityEngine;
// using System.Collections;

public struct AttackResult
{
    public IReadOnlyList<NoteData> Notes;
    public int BadTimingInputCount;
    public int MissingNoteCount;
    public int ExtraNoteCount;
    public int DuplicateInputCount;
}

public class AttackTurn : MonoBehaviour
{
    /// <summary>
    /// 공격 턴이 종료되면 생성된 노트 목록과 함께 발행. GameManager가 구독.
    /// </summary>
    public event System.Action<AttackResult> OnAttackEnded;
    /// <summary>
    /// 공격 구간 총 길이(초). DefenseTurn의 barSpeed 계산에 사용.
    /// </summary>
    public double AttackDuration => attackDuration;

    private const int HalfBeatStepsPerMeasure = 8;
    private const int FirstPlayableGridStep = 1;

    private int attackMeasureCount = 1;

    [Header("Attack input window [공격 성공 범위]")]
    [SerializeField] private double attackInputWindowMs = 100.0;
    
    
    // [Header("Penalty Alert UI")]
    // [SerializeField] private float penaltyAlertDuration = 1.0f;

    [Header("References [참조]")]
    [SerializeField] private RhythmClock rhythmClock;
    [SerializeField] private AttackTurnRenderer attackTurnRenderer;

    // private TMP_Text attackStatusLabel;
    // private TMP_Text attackPenaltyLabel;
    // private Coroutine penaltyAlertCoroutine;

    private int badTimingInputCount;
    private int missingNoteCount;
    private int extraNoteCount;
    private int duplicateInputCount;

    private readonly List<NoteData> createdNotes = new();
    private readonly List<double> opponentDemoRelativeTimes = new();
    private readonly HashSet<int> createdGridSteps = new();

    private AttackSide currentSide;
    private double attackStartDspTime;
    private double attackDuration;
    private int gridStepCount;
    private int targetTapCount;
    private int nextNoteId;
    private int nextOpponentDemoIndex;
    private bool isRunning;
    private bool isLocalPlayerAttack;
    private readonly int[] opponentDemoGridSteps = { 2, 4, 6 };
    
    private string currentAttackMessage;
    /// <summary>
    /// 공격 메시지가 선택되면 발행. GameManager가 구독해서 HUD에 표시.
    /// </summary>
    public event System.Action<string, int> OnAttackMessageSelected;
    public int OpponentDemoNoteCount => opponentDemoGridSteps.Length;

    private double NoteDuration
    {
        get
        {
            if (rhythmClock == null)
            {
                Debug.LogWarning("RhythmClock이 연결되지 않았습니다. 기본 BPM 120 기준으로 계산합니다.");
                return 30.0 / 120.0;
            }
            return rhythmClock.GetNoteDuration();
        }
    }

    // /// <summary>
    // /// DevAttackPanel에서 공격 테스트 UI를 등록.
    // /// </summary>
    // public void RegisterView(AttackTurnRenderer renderer, TMP_Text statusLabel, TMP_Text penaltyLabel)
    // {
    //     attackTurnRenderer = renderer;
    //     attackStatusLabel = statusLabel;
    //     attackPenaltyLabel = penaltyLabel;
    //     if (attackPenaltyLabel != null)
    //     {
    //         attackPenaltyLabel.text = string.Empty;
    //         attackPenaltyLabel.gameObject.SetActive(false);
    //     }
    //     ShowStatus("ATTACK TEST READY");
    // }

    // /// <summary>
    // /// DevAttackPanel 제거 시 등록된 UI 참조를 해제.
    // /// </summary>
    // public void UnregisterView(AttackTurnRenderer renderer, TMP_Text statusLabel, TMP_Text penaltyLabel)
    // {
    //     if (attackTurnRenderer == renderer) attackTurnRenderer = null;
    //     if (attackStatusLabel == statusLabel) attackStatusLabel = null;
    //     if (attackPenaltyLabel == penaltyLabel) attackPenaltyLabel = null;
    // }

    
    /// <summary>
    /// 개발용 P1 공격 테스트 시작. DevAttackPanel 버튼에서 호출.
    /// </summary>
    public void StartLocalPlayerAttack(int targetNoteCount, string attackMessage)
    {
        StartAttack(AttackSide.P1, true, targetNoteCount, attackMessage);
    }

    /// <summary>
    /// 개발용 P2 공격 데모 시작. DevAttackPanel 버튼에서 호출.
    /// </summary>
    public void StartOpponentAttackDemo(string attackMessage)
{
    StartAttack(AttackSide.P2, false, opponentDemoGridSteps.Length, attackMessage);

    opponentDemoRelativeTimes.Clear();
    double noteDuration = NoteDuration;

    foreach (int step in opponentDemoGridSteps)
    {
        double relativeTime = step * noteDuration;
        if (relativeTime <= attackDuration)
            opponentDemoRelativeTimes.Add(relativeTime);
    }

    targetTapCount = opponentDemoRelativeTimes.Count;
    nextOpponentDemoIndex = 0;
}

    /// <summary>
    /// Tap Action 입력 시 호출. P1 공격 중이면 noteRelativeTime을 생성.
    /// </summary>
    public void OnTap()
    {
        if (!isRunning || !isLocalPlayerAttack) return;

        double noteDuration = NoteDuration;
        double relativeTime = GetCorrectedRelativeInputTime();

        if (relativeTime < 0.0 || relativeTime > attackDuration)
        {
            badTimingInputCount++;
            return;
        }

        int nearestGridStep = GetNearestPlayableGridStep(relativeTime, noteDuration);
        double snappedRelativeTime = nearestGridStep * noteDuration;
        double offsetMs = System.Math.Abs(relativeTime - snappedRelativeTime) * 1000.0;

        if (offsetMs > attackInputWindowMs)
        {
            badTimingInputCount++;
            return;
        }

        if (createdGridSteps.Contains(nearestGridStep))
        {
            duplicateInputCount++;
            return;
        }

        if (createdNotes.Count >= targetTapCount)
        {
            extraNoteCount++;
        }

        CreateAttackNote(snappedRelativeTime);
    }

    private void Update()
    {
        if (!isRunning) return;

        double elapsed = AudioSettings.dspTime - attackStartDspTime;

        if (!isLocalPlayerAttack)
            UpdateOpponentDemo(elapsed);

        if (elapsed >= attackDuration)
            EndAttack();
    }

    private void StartAttack(AttackSide side, bool localPlayerAttack, int requiredTapCount, string attackMessage)
    {
        currentSide = side;
        isLocalPlayerAttack = localPlayerAttack;
        isRunning = true;
        nextNoteId = 0;
        nextOpponentDemoIndex = 0;
        createdNotes.Clear();
        createdGridSteps.Clear();
        opponentDemoRelativeTimes.Clear();
        extraNoteCount = 0;
        missingNoteCount = 0;
        duplicateInputCount = 0;
        badTimingInputCount = 0;

        double noteDuration = NoteDuration;
        gridStepCount = Mathf.Max(1, attackMeasureCount) * HalfBeatStepsPerMeasure;
        attackDuration = noteDuration * gridStepCount;
        attackStartDspTime = AudioSettings.dspTime;
        targetTapCount = Mathf.Clamp(requiredTapCount, 1, MaxPlayableTapCount);
        SetAttackMessage(attackMessage);

        if (attackTurnRenderer != null)
            attackTurnRenderer.BeginAttackVisual(currentSide, attackStartDspTime, attackDuration, gridStepCount);
        else
            Debug.LogWarning("AttackTurnRenderer가 연결되지 않았습니다.");
    }

    private void UpdateOpponentDemo(double elapsed)
    {
        while (nextOpponentDemoIndex < opponentDemoRelativeTimes.Count &&
               elapsed >= opponentDemoRelativeTimes[nextOpponentDemoIndex])
        {
            CreateAttackNote(opponentDemoRelativeTimes[nextOpponentDemoIndex]);
            nextOpponentDemoIndex++;
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
        createdGridSteps.Add(GetNearestPlayableGridStep(noteRelativeTime, NoteDuration));

        if (attackTurnRenderer != null)
            attackTurnRenderer.SpawnAttackNote(currentSide, note, attackDuration);

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

        missingNoteCount = Mathf.Max(0, targetTapCount - createdNotes.Count);

        Debug.Log(
            $"Attack End / noteCount: {createdNotes.Count}, target: {targetTapCount}, " +
            $"badTiming:{badTimingInputCount}, missing:{missingNoteCount}, " +
            $"extra:{extraNoteCount}, duplicate:{duplicateInputCount}"
        );

        AttackResult result = new AttackResult
        {
            Notes = new List<NoteData>(createdNotes).AsReadOnly(),
            BadTimingInputCount = badTimingInputCount,
            MissingNoteCount = missingNoteCount,
            ExtraNoteCount = extraNoteCount,
            DuplicateInputCount = duplicateInputCount
        };

        OnAttackEnded?.Invoke(result);
    }

    private int LastPlayableGridStep => Mathf.Max(FirstPlayableGridStep, gridStepCount - 1);
    private int MaxPlayableTapCount => LastPlayableGridStep - FirstPlayableGridStep + 1;

    private double GetCorrectedRelativeInputTime()
    {
        double inputTime = AudioSettings.dspTime;
        if (JudgeSystem.Instance != null)
            inputTime -= JudgeSystem.Instance.KeyInputOffsetMs / 1000.0;
        return inputTime - attackStartDspTime;
    }

    private int GetNearestPlayableGridStep(double relativeTime, double noteDuration)
    {
        if (noteDuration <= 0.0) return FirstPlayableGridStep;
        int nearestStep = Mathf.RoundToInt((float)(relativeTime / noteDuration));
        return Mathf.Clamp(nearestStep, FirstPlayableGridStep, LastPlayableGridStep);
    }


    // private void ShowStatus(string message)
    // {
    //     if (attackStatusLabel != null) attackStatusLabel.text = message;
    // }

    // private void ShowPenaltyAlert(int amount, string reason)
    // {
    //     if (attackPenaltyLabel == null) return;
    //     attackPenaltyLabel.text = $"정신력 -{amount}\n{ConvertPenaltyReasonToText(reason)}";
    //     attackPenaltyLabel.gameObject.SetActive(true);
    //     if (penaltyAlertCoroutine != null) StopCoroutine(penaltyAlertCoroutine);
    //     penaltyAlertCoroutine = StartCoroutine(HidePenaltyAlertAfterDelay());
    // }

    // private IEnumerator HidePenaltyAlertAfterDelay()
    // {
    //     yield return new WaitForSeconds(penaltyAlertDuration);
    //     if (attackPenaltyLabel != null)
    //     {
    //         attackPenaltyLabel.text = string.Empty;
    //         attackPenaltyLabel.gameObject.SetActive(false);
    //     }
    //     penaltyAlertCoroutine = null;
    // }

    // private string ConvertPenaltyReasonToText(string reason)
    // {
    //     if (reason.Contains("OFF_GRID")) return "OFF GRID";
    //     if (reason.Contains("OUT_OF_ATTACK_TIME")) return "OUT OF ATTACK TIME";
    //     if (reason.Contains("MISSING_NOTES")) return "MISSING NOTES";
    //     if (reason.Contains("EXTRA_NOTE")) return "EXTRA NOTE";
    //     if (reason.Contains("DUPLICATE_INPUT")) return "DUPLICATE INPUT";
    //     return "ATTACK PANELTY";
    // }
   private void SetAttackMessage(string attackMessage)
    {
        if (string.IsNullOrEmpty(attackMessage))
        {
            currentAttackMessage = new string('?', targetTapCount);
        }
        else
        {
            currentAttackMessage = attackMessage;
        }

        OnAttackMessageSelected?.Invoke(currentAttackMessage, targetTapCount);

        Debug.Log($"Attack Message Selected / target:{targetTapCount}, message:{currentAttackMessage}");
    }
}
