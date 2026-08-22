using System.Collections.Generic;
using UnityEngine;


/// <summary>
///  공격 턴 종료 시 GameManager에 전달되는 결과 데이터
///  생성된 노트 목록과 공격 중 발생한 입력 실수 횟수를 포함
/// </summary>
public struct AttackResult
{
    public IReadOnlyList<NoteData> Notes; //방어 턴으로 넘길 공격 노트 목록
    public int BadTimingInputCount;       //공격 시간 밖 입력 또는 박자 이탈 횟수
    public int MissingNoteCount;          //공격 노트 누락 횟수
    public int ExtraNoteCount;            //공격 초과 입력 횟수
    public int DuplicateInputCount;       //공격 중복 입력 횟수
}

public class AttackTurn : MonoBehaviour
{
    /// <summary>
    /// 공격 구간 총 길이(초). DefenseTurn의 barSpeed 계산에 사용.
    /// </summary>
    public double AttackDuration => attackDuration;

    /// <summary>
    /// 현재 공격 턴이 시작된 DSP 시각. 미러뷰의 defenseStartDspTime 계산에 사용.
    /// </summary>
    public double AttackStartDspTime => attackStartDspTime;

    [Header("Rhythm [리듬 설정]")]
    [SerializeField] private int subdivisions = 2; // 1박당 분할 수. 2=반박(기본), 3=3연음

    // 4/4박자 고정. 1마디 = 4박 * subdivisions 스텝
    private int StepsPerMeasure => 4 * subdivisions;

    // 0번째 칸은 공격 시작 지점이라 입력 가능 칸에서 제외한다.
    private const int FirstPlayableGridStep = 1;

    // 현재 공격 턴 길이. 지금은 1마디 고정.
    private int attackMeasureCount = 1;

    [Header("Attack input window [공격 성공 범위]")]
    [SerializeField, Range(0f, 0.5f)] private float attackTimingWindowRatio = 0.25f;
    
    
    [Header("References [참조]")]
    [SerializeField] private RhythmClock rhythmClock;
    [SerializeField] private AttackTurnRenderer attackTurnRenderer;
    [SerializeField] private NetworkManager networkManager; // null이면 로컬 전용

    private int badTimingInputCount;
    private int missingNoteCount;
    private int extraNoteCount;
    private int duplicateInputCount;

    private readonly List<NoteData> createdNotes = new();
    private readonly List<(double time, NoteType type)> opponentDemoRelativeTimes = new();
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

    private void Awake()
    {
        if (rhythmClock == null)
        {
            rhythmClock = RhythmClock.Instance;
        }

        if (networkManager == null &&
            NetworkManager.Instance != null &&
            NetworkManager.Instance.IsConnected)
        {
            networkManager = NetworkManager.Instance;
        }
    }

    /// <summary>
    /// 공격 노트가 생성됐을 때 발행.
    /// TutorialManager가 받아서 방어 연습 때 힌트 표시
    /// </summary>
    public event System.Action<NoteData> OnAttackNoteCreated;

    /// <summary>
    /// 공격 메시지가 정해졌을 때 발행.
    /// GameManager가 받아서 HUD에 현재 공격 메시지를 표시한다.
    /// </summary>
    public event System.Action<string, int> OnAttackMessageSelected;

    /// <summary>
    /// 공격 노트 생성 진행도가 바뀔 때 발행.
    /// currentCount는 현재까지 생성된 노트 수, targetCount는 목표 노트 수.
    /// </summary>
    public event System.Action<int, int> OnAttackProgressChanged;

    /// <summary>
    /// 공격 턴이 끝났을 때 발행.
    /// GameManager가 AttackResult를 받아 방어 턴 시작 및 정신력 패널티 처리를 진행한다.
    /// </summary>
    public event System.Action<AttackResult> OnAttackEnded;

    /// <summary>
    /// 공격 입력이 성공/실패로 확정됐을 때 발행한다.
    /// GameManager가 구독해 성공이면 HitHigh/HitLow, 실패면 Miss 사운드를 재생한다.
    /// </summary>
    public event System.Action<NoteType, bool> OnAttackInputResolved;
    
    public int OpponentDemoNoteCount => opponentDemoGridSteps.Length;

    /// <summary>
    /// 현재 BPM 기준 1스텝(1/subdivisions 박) 길이를 반환.
    /// RhythmClock이 연결되지 않았을 경우 기본 BPM 120 기준으로 계산한다.
    /// 인스펙터 연결이 빠져도 RhythmClock.Instance를 찾아서 계산한다.
    /// </summary>
    private double NoteDuration
    {
        get
        {
            RhythmClock clock = rhythmClock != null
                ? rhythmClock
                : RhythmClock.Instance;

            if (clock == null)
            {
                Debug.LogWarning("RhythmClock을 찾을 수 없습니다. 기본 BPM 120 기준으로 계산합니다.");
                return 60.0 / 120.0 / System.Math.Max(1, subdivisions);
            }

            return clock.GetNoteDuration(subdivisions);
        }
    }

    /// <summary>
    /// 로컬 플레이어의 공격 턴을 시작. turnStartDspTime은 GameManager의 DSP 페이즈 루프가 전달한다.
    /// </summary>
    public void StartLocalPlayerAttack(AttackSide side, int targetNoteCount, string attackMessage, double turnStartDspTime)
    {
        StartAttack(side, true, targetNoteCount, attackMessage, turnStartDspTime);
    }

    /// <summary>
    /// 개발용 P2 공격 데모 시작. DevAttackPanel 버튼에서 호출.
    /// </summary>
    public void StartOpponentAttackDemo(string attackMessage)
    {
        StartAttack(AttackSide.P2, false, opponentDemoGridSteps.Length, attackMessage, AudioSettings.dspTime);

        opponentDemoRelativeTimes.Clear();
        double noteDuration = NoteDuration;

        for (int i = 0; i < opponentDemoGridSteps.Length; i++)
        {
            int step = opponentDemoGridSteps[i];
            double relativeTime = step * noteDuration;
            if (relativeTime <= attackDuration)
                opponentDemoRelativeTimes.Add((relativeTime, i % 2 == 0 ? NoteType.HIGH : NoteType.LOW));
        }

        targetTapCount = opponentDemoRelativeTimes.Count;
        nextOpponentDemoIndex = 0;
    }

    public void StartOpponentAttackDemo(string attackMessage, NoteType[] demoPattern)
    {
        StartOpponentAttackDemo(attackMessage, demoPattern, AudioSettings.dspTime);
    }

    /// <summary>
    /// 튜토리얼용 상대 데모 공격 시작.
    /// 지정한 노트 패턴을 지정한 DSP 시간에 맞춰 자동 생성한다.
    /// </summary>
    public void StartOpponentAttackDemo(
        string attackMessage,
        NoteType[] demoPattern,
        double startDspTime
    )
    {
        if (demoPattern == null || demoPattern.Length == 0)
        {
            Debug.LogWarning("AttackTurn: 데모 패턴이 비어 있음.");
            return;
        }

        StartAttack(
            AttackSide.P2,
            false,
            demoPattern.Length,
            attackMessage,
            startDspTime
        );

        opponentDemoRelativeTimes.Clear();

        double noteDuration = NoteDuration;

        for (int i = 0; i < demoPattern.Length; i++)
        {
            int step = 2 + i * 2;
            double relativeTime = step * noteDuration;

            if (relativeTime <= attackDuration)
            {
                opponentDemoRelativeTimes.Add((relativeTime, demoPattern[i]));
            }
        }

        targetTapCount = opponentDemoRelativeTimes.Count;
        nextOpponentDemoIndex = 0;

        OnAttackProgressChanged?.Invoke(0, targetTapCount);
    }

    /// <summary>
    /// 공격 턴 입력을 처리한다.
    /// 공격 시간 안의 입력은 가장 가까운 박자선에 스냅한다.
    /// 박자 어긋남은 감점 카운트에 기록하되, 스냅된 노트는 생성한다.
    /// </summary>
    public void OnTap(NoteType noteType)
    {
        if (!isRunning || !isLocalPlayerAttack) return;

        double noteDuration = NoteDuration;
        double relativeTime = GetCorrectedRelativeInputTime();

        // 공격 시간 자체 밖 입력은 스냅 보정 대상이 아니므로 노트를 생성하지 않는다.
        if (relativeTime < 0.0 || relativeTime > attackDuration)
        {
            badTimingInputCount++;
            OnAttackInputResolved?.Invoke(noteType, false);
            return;
        }

        int nearestGridStep = GetNearestPlayableGridStep(relativeTime, noteDuration);
        double snappedRelativeTime = nearestGridStep * noteDuration;
        double offsetMs = System.Math.Abs(relativeTime - snappedRelativeTime) * 1000.0;

        double timingWindowMs = noteDuration * attackTimingWindowRatio * 1000.0;
        bool isTimingSuccess = offsetMs <= timingWindowMs;

        // 같은 박자선에 이미 노트가 있으면 중복 입력으로 처리하고 노트를 생성하지 않는다.
        if (createdGridSteps.Contains(nearestGridStep))
        {
            duplicateInputCount++;
            OnAttackInputResolved?.Invoke(noteType, false);
            return;
        }

        // 박자 어긋남은 감점 대상이지만, 기획 기준상 가장 가까운 박자선으로 보정해 전달한다.
        if (!isTimingSuccess)
        {
            badTimingInputCount++;
        }

        // 목표 개수를 초과한 입력은 패널티 카운트에 기록하지만, 노트 자체는 생성한다.
        if (createdNotes.Count >= targetTapCount)
        {
            extraNoteCount++;
        }

        CreateAttackNote(snappedRelativeTime, noteType, isInputSuccessForSfx: true);
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

    /// <summary>
    /// 공격 턴 공통 초기화.
    /// 공격자 방향, 입력 주체, 목표 노트 수, 메시지를 설정하고 공격 판정선 이동을 시작한다.
    /// turnStartDspTime은 GameManager의 DSP 페이즈 루프가 전달한 턴 시작 시각이다.
    /// </summary>
    private void StartAttack(AttackSide side, bool localPlayerAttack, int requiredTapCount, string attackMessage, double turnStartDspTime)
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
        gridStepCount = Mathf.Max(1, attackMeasureCount) * StepsPerMeasure;
        attackDuration = noteDuration * gridStepCount;
        attackStartDspTime = turnStartDspTime;
        targetTapCount = Mathf.Clamp(requiredTapCount, 1, MaxPlayableTapCount);
        SetAttackMessage(attackMessage);
        OnAttackProgressChanged?.Invoke(0, targetTapCount);

        if (attackTurnRenderer != null)
            attackTurnRenderer.BeginAttackVisual(currentSide, attackStartDspTime, attackDuration, gridStepCount);
        else
            Debug.LogWarning("AttackTurnRenderer가 연결되지 않았습니다.");
    }

    /// <summary>
    /// P2 데모 공격에서 정해진 relativeTime에 도달한 노트를 자동 생성한다.
    /// </summary>
    private void UpdateOpponentDemo(double elapsed)
    {
        while (nextOpponentDemoIndex < opponentDemoRelativeTimes.Count &&
               elapsed >= opponentDemoRelativeTimes[nextOpponentDemoIndex].time)
        {
            var entry = opponentDemoRelativeTimes[nextOpponentDemoIndex];
            CreateAttackNote(entry.time, entry.type);
            nextOpponentDemoIndex++;
        }
    }

    /// <summary>
    /// 공격 노트를 생성하고 렌더러와 진행도 UI에 반영한다.
    /// judgeTime은 방어 턴 전환 시 DefenseTurn에서 설정한다.
    /// </summary>
    private void CreateAttackNote(double noteRelativeTime, NoteType noteType, bool isInputSuccessForSfx = true)
    {
        NoteData note = new NoteData
        {
            noteId = nextNoteId++,
            noteType = noteType,
            noteRelativeTime = noteRelativeTime
            // judgeTime은 여기서 설정하지 않는다.
            // 방어자 쪽 transfer 이후 defenseStartDspTime + noteRelativeTime으로 계산한다.
        };

        createdNotes.Add(note);
        if (isLocalPlayerAttack && networkManager != null && networkManager.IsConnected)
        {
            int id = note.noteId;
            double rel = note.noteRelativeTime;
            NoteType nt = note.noteType;
            networkManager.Send(w => PacketSerializer.WriteNoteCreated(w, id, rel, nt));
        }
        createdGridSteps.Add(GetNearestPlayableGridStep(noteRelativeTime, NoteDuration));
        OnAttackProgressChanged?.Invoke(createdNotes.Count, targetTapCount);
        if (attackTurnRenderer != null)
            attackTurnRenderer.SpawnAttackNote(currentSide, note, attackDuration);

        OnAttackInputResolved?.Invoke(noteType, isInputSuccessForSfx);
        
        OnAttackNoteCreated?.Invoke(note);
        
        Debug.Log($"Attack Note Created / id: {note.noteId}, type: {note.noteType}, relativeTime: {note.noteRelativeTime:0.000}s");
    }

    /// <summary>
    /// 공격 턴을 종료하고 AttackResult를 생성해 GameManager에 전달한다.
    /// 부족한 노트 수는 이 시점에 계산한다.
    /// </summary>
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

    /// <summary>
    /// 마지막으로 입력 가능한 grid step.
    /// 마지막 step은 공격 종료 지점이므로 제외한다.
    /// </summary>
    private int LastPlayableGridStep => Mathf.Max(FirstPlayableGridStep, gridStepCount - 1);
    
    /// <summary>
    /// 현재 공격 턴에서 입력 가능한 최대 노트 개수.
    /// </summary>
    private int MaxPlayableTapCount => LastPlayableGridStep - FirstPlayableGridStep + 1;
    
    /// <summary>
    /// JudgeSystem의 키 입력 오프셋을 적용해 공격 시작 이후의 상대 입력 시간을 계산한다.
    /// </summary>
    private double GetCorrectedRelativeInputTime()
    {
        double inputTime = AudioSettings.dspTime;
        if (JudgeSystem.Instance != null)
            inputTime -= JudgeSystem.Instance.KeyInputOffsetMs / 1000.0;
        return inputTime - attackStartDspTime;
    }

    /// <summary>
    /// 가장 가까운 입력 가능한 grid step을 반환한다.
    /// </summary>
    private int GetNearestPlayableGridStep(double relativeTime, double noteDuration)
    {
        if (noteDuration <= 0.0) return FirstPlayableGridStep;
        int nearestStep = Mathf.RoundToInt((float)(relativeTime / noteDuration));
        return Mathf.Clamp(nearestStep, FirstPlayableGridStep, LastPlayableGridStep);
    }

    /// <summary>
    /// 현재 공격 메시지를 저장하고 HUD 표시용 이벤트를 발행한다.
    /// 메시지가 비어 있으면 목표 노트 수만큼 '?'로 대체한다.
    /// </summary>
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
