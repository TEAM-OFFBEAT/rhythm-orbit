using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 튜토리얼 전체 진행을 담당한다.
/// 턴 재생은 기존 AttackTurn / DefenseTurn / AttackTurnRenderer / GameCamera / HUD를 그대로 사용한다.
/// </summary>
public class TutorialManager : MonoBehaviour
{
    private enum TutorialStep
    {
        None,
        IntroDialogue,
        AttackDialogue,
        AttackPractice,
        DefenseDialogue,
        DefensePractice,
        RallyDialogue,
        RallyAttack,
        RallyDefense,
        Complete
    }

    [Header("Core Turn Components")]
    [SerializeField] private AttackTurn attackTurn;
    [SerializeField] private DefenseTurn defenseTurn;
    [SerializeField] private AttackTurnRenderer attackTurnRenderer;
    [SerializeField] private GameCamera gameCamera;

    [Header("UI")]
    [SerializeField] private HUD hud;
    [SerializeField] private TutorialDialoguePlayer dialoguePlayer;
    [Tooltip("랠리 단계에서 처음 표시할 정신력 UI 루트. 비워두면 활성/비활성은 건드리지 않고 HUD 값만 갱신한다.")]
    [SerializeField] private GameObject mySanityUiRoot;
    [SerializeField] private GameObject opponentSanityUiRoot;

    [Header("Tutorial Settings")]
    [SerializeField] private float tutorialBpm = 90f;
    [SerializeField] private AttackSide playerSide = AttackSide.P1;
    [SerializeField] private int rallyTurnCount = 8;
    [SerializeField] private int guideBeatsPerLine = 4;

    [SerializeField] private TutorialPatternProvider patternProvider;


    [Header("Timing")]
    [Tooltip("공격 시작 예약 지연. 튜토리얼에서는 즉시 입력이 먹도록 0 권장.")]
    [SerializeField] private float attackStartDelay = 0f;
    [SerializeField] private float viewTransitionDelay = 0.15f;
    [SerializeField] private float betweenTurnDelay = 0.4f;

    [Header("Tutorial Sanity")]
    [SerializeField] private int tutorialMaxSanity = 100;
    [SerializeField] private int rallySanityLossPerTurn = 1;
    [SerializeField] private int tutorialMinimumSanity = 90;

    [Header("Defense Reaction")]
    [SerializeField] private int defenseReactionBeats = 4;

    private bool latestDefenseResultAvailable;
    private int latestDefenseTotalCount;
    private int latestDefenseMissCount;

    [Header("Intro Beat Demo")]
    [SerializeField] private bool playIntroBeatDemo = true;

    [Tooltip("인트로 몇 번째 문장에서 비트 소개 노트를 띄울지 설정한다. 1부터 시작한다.")]
    [SerializeField, Min(1)] private int introBeatDemoLineNumber = 6;

    [Tooltip("고주파 노트가 공격 라인에서 생성될 위치 비율.0.5가 중앙이다.")]
    [SerializeField, Range(0f, 1f)] private float introHighNotePositionRatio = 0.47f;

    [Tooltip("저주파 노트가 공격 라인에서 생성될 위치 비율. 0.5가 중앙이다.")]
    [SerializeField, Range(0f, 1f)] private float introLowNotePositionRatio = 0.53f;

    [SerializeField] private double introBeatDemoDuration = 2.0;
    [SerializeField] private int introSecondNoteDelayBeats = 1;
    [SerializeField] private int introDemoFirstNoteId = 900000;

    [Header("Guide Lines")]
    [SerializeField] private string[] introGuideLines =
    {
        "만나서 반가워! 튜토리얼을 도와줄 리모야.",
        "리듬오빗에서는 비트를 보내고 받으면서 교신을 이어가야 해.",
        "비트는 고주파 비트와 저주파 비트 두 종류가 있어."
    };

    [SerializeField] private string[] attackGuideLines =
    {
        "먼저 공격 턴이야.",
        "F키로 고주파 비트, J키로 저주파 비트를 만들 수 있어.",
        "판정선이 움직일 때 원하는 박자에 맞춰 비트를 보내보자."
    };

    [SerializeField] private string[] defenseGuideLines =
    {
        "이번엔 방어 턴이야.",
        "상대가 보낸 비트가 판정선에 닿을 때 같은 키를 눌러 받아치면 돼.",
        "고주파 비트는 F, 저주파 비트는 J로 방어해보자."
    };

    [SerializeField] private string[] rallyGuideLines =
    {
        "이제 실전처럼 공격과 방어를 번갈아 연습해보자.",
        "랠리 단계부터 정신력 게이지가 표시돼.",
        "튜토리얼에서는 정신력이 아주 조금만 줄고, 패배하지는 않아."
    };

    [Header("Tutorial Pattern")]
    
    [Header("Debug")]
    [SerializeField] private bool logInputRouting = true;
    [SerializeField] private bool logTurnFlow = true;

    private TutorialStep currentStep = TutorialStep.None;
    private AttackSide currentHudAttackerSide = AttackSide.P1;
    private AttackSide currentDefenseAttackerSide = AttackSide.P1;

    private bool attackEnded;
    private bool defenseEnded;
    private bool showDefenseJudgmentUi;
    private bool currentDefenseIsAiDefense;
    private bool introBeatDemoStarted;
    private bool showCurrentDefenseKeyHints;

    private int nextIntroDemoNoteId;
    private int currentDefenseJudgmentIndex;
    

    private int tutorialP1Sanity;
    private int tutorialP2Sanity;

    private double lastAttackStartDspTime;
    private double lastAttackDuration;
    

    private readonly List<NoteData> lastAttackNotes = new List<NoteData>();

    private const string TutorialCompletedKey = "TutorialCompleted";

    private void OnEnable()
    {
        SubscribeEvents();
        if (attackTurn != null)
        {
            attackTurn.OnAttackNoteCreated += HandleTutorialAttackNoteCreated;
        }
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
        if (attackTurn != null)
        {
            attackTurn.OnAttackNoteCreated -= HandleTutorialAttackNoteCreated;
        }
    }

    private void Start()
    {
        if (!ValidateReferences())
        {
            Debug.LogError("TutorialManager: 필수 참조가 없어 튜토리얼 시작 중단.");
            return;
        }

        StartCoroutine(RunTutorial());
    }

    private void SubscribeEvents()
    {
        if (attackTurn != null)
        {
            attackTurn.OnAttackEnded += HandleAttackEnded;
            attackTurn.OnAttackMessageSelected += HandleAttackMessageSelected;
            attackTurn.OnAttackProgressChanged += HandleAttackProgressChanged;
        }

        if (defenseTurn != null)
        {
            defenseTurn.OnDefenseEnded += HandleDefenseEnded;
            defenseTurn.OnJudgment += HandleJudgment;
        }
    }

    private void UnsubscribeEvents()
    {
        if (attackTurn != null)
        {
            attackTurn.OnAttackEnded -= HandleAttackEnded;
            attackTurn.OnAttackMessageSelected -= HandleAttackMessageSelected;
            attackTurn.OnAttackProgressChanged -= HandleAttackProgressChanged;
        }

        if (defenseTurn != null)
        {
            defenseTurn.OnDefenseEnded -= HandleDefenseEnded;
            defenseTurn.OnJudgment -= HandleJudgment;
        }
    }

    /// <summary>
    /// 인트로 설명 → 공격 설명 → 공격연습 → 방어 설명 → 방어연습 → 랠리 설명 → 랠리연습 순서로 진행한다.
    /// </summary>
    private IEnumerator RunTutorial()
    {
        InitializeTutorial();

        currentStep = TutorialStep.IntroDialogue;
        yield return PlayIntroDialogueWithBeatDemo();

        attackTurnRenderer.ClearAll();

        currentStep = TutorialStep.AttackDialogue;
        yield return PlayDialogue(attackGuideLines);

        yield return RunAttackPractice();

        currentStep = TutorialStep.DefenseDialogue;
        yield return PlayDialogue(defenseGuideLines);

        yield return RunDefensePractice();

        currentStep = TutorialStep.RallyDialogue;
        yield return PlayDialogue(rallyGuideLines);

        yield return RunRallyPractice();

        currentStep = TutorialStep.Complete;
        CompleteTutorial();
    }

    private void InitializeTutorial()
    {
        Time.timeScale = 1f;
        currentStep = TutorialStep.None;

         if (hud != null && !hud.gameObject.activeSelf)
        {
            hud.gameObject.SetActive(true);
            Debug.LogWarning("TutorialManager: HUD 루트가 비활성화되어 있어 강제로 활성화함.");
        }
        RhythmClock.Instance?.SetBpm(tutorialBpm);
        RhythmClock.Instance?.StartClock(AudioSettings.dspTime);

        dialoguePlayer?.SetBpm(tutorialBpm);
        dialoguePlayer?.Hide();

        attackTurnRenderer.SetLocalPlayer(GetPlayerId(playerSide));
        attackTurnRenderer.ClearAll();
        
        introBeatDemoStarted = false;
        nextIntroDemoNoteId = introDemoFirstNoteId;

        hud?.SetupPlayerPerspective(GetPlayerId(playerSide));
        hud?.UpdateBpm(tutorialBpm);
        hud?.ClearAttackProgress();
        hud?.ClearJudgments();
        hud?.ClearPanelMessages();

        ResetTutorialSanity();
        SetSanityVisible(false);
        UpdateTutorialSanityHud();

        gameCamera?.SetAttackView(playerSide);

        if (NetworkManager.Instance != null)
        {
            Debug.LogWarning("TutorialManager: TutorialScene에 NetworkManager.Instance가 있음. 튜토리얼에서는 비활성화 권장.");
        }

        Debug.Log("TutorialManager: Initialized");
    }

    public void OnTapHigh()
    {
        HandleTap(NoteType.HIGH);
    }

    public void OnTapLow()
    {
        HandleTap(NoteType.LOW);
    }

    public void OnTapHigh(InputValue value)
    {
        if (value != null && !value.isPressed) return;
        HandleTap(NoteType.HIGH);
    }

    public void OnTapLow(InputValue value)
    {
        if (value != null && !value.isPressed) return;
        HandleTap(NoteType.LOW);
    }

    /// <summary>
    /// TutorialInputRouter 또는 UI 버튼에서 호출한다.
    /// </summary>
    public void HandleTap(NoteType noteType)
    {
        if (logInputRouting)
        {
            Debug.Log($"TutorialManager HandleTap / step:{currentStep}, note:{noteType}");
        }

        switch (currentStep)
        {
            case TutorialStep.AttackPractice:
            case TutorialStep.RallyAttack:
                attackTurn.OnTap(noteType);
                break;

            case TutorialStep.DefensePractice:
            case TutorialStep.RallyDefense:
                defenseTurn.OnTap(noteType);
                break;

            default:
                Debug.Log($"TutorialManager: 현재 단계({currentStep})에서는 입력을 무시함.");
                break;
        }
    }

    private IEnumerator RunAttackPractice()
    {
        if (logTurnFlow) Debug.Log("TutorialManager: AttackPractice 시작");

        bool success = false;

        while (!success)
        {
            TutorialPatternData pattern = patternProvider.GetAttackPracticePattern();

            yield return RunPlayerAttackTurn(
                pattern.message,
                pattern.NoteCount,
                TutorialStep.AttackPractice
            );

            success = lastAttackNotes.Count > 0;

            if (!success)
            {
                dialoguePlayer?.Show("비트를 1개 이상 보내야 다음 단계로 넘어갈 수 있어. F나 J를 눌러봐.");
                yield return new WaitForSecondsRealtime(1.2f);
                dialoguePlayer?.Hide();
            }
        }

        yield return RunDefenseTurnForExistingNotes(
            attackerSide: playerSide,
            isAiDefense: true,
            inputStep: TutorialStep.None
        );

        attackTurnRenderer.ClearAll();
        hud?.ClearAttackProgress();
        hud?.ClearJudgments();
        hud?.ClearPanelMessages();

        yield return new WaitForSecondsRealtime(betweenTurnDelay);
    }

    private IEnumerator RunDefensePractice()
    {
        if (logTurnFlow)
            Debug.Log("TutorialManager: DefensePractice 시작");

        for (int i = 0; i < 3; i++)
        {
            TutorialDefensePattern pattern = GetDefensePracticePattern(i);

            yield return RunOpponentDemoAttackThenDefense(
                pattern,
                TutorialStep.DefensePractice
            );

            bool isLastPractice = i == 2;

            if (!isLastPractice)
            {
                ShowDefensePracticeReaction(i);
            }

            yield return new WaitForSecondsRealtime(betweenTurnDelay);
        }

        currentStep = TutorialStep.None;
    }

    private IEnumerator RunRallyPractice()
    {
        if (logTurnFlow) Debug.Log("TutorialManager: RallyPractice 시작");

        ResetTutorialSanity();
        SetSanityVisible(true);
        UpdateTutorialSanityHud();

        dialoguePlayer?.Show("랠리 시작! 이제 정신력 게이지를 보면서 공격과 방어를 번갈아 해보자.");
        yield return new WaitForSecondsRealtime(1.2f);
        dialoguePlayer?.Hide();

        for (int turn = 0; turn < rallyTurnCount; turn++)
        {
            bool isPlayerAttack = turn % 2 == 0;

            if (isPlayerAttack)
            {
                TutorialPatternData pattern = patternProvider.GetRallyPlayerPattern(turn);

                yield return RunPlayerAttackTurn(
                    pattern.message,
                    pattern.NoteCount,
                    TutorialStep.RallyAttack
                );

                yield return RunDefenseTurnForExistingNotes(
                    attackerSide: playerSide,
                    isAiDefense: true,
                    inputStep: TutorialStep.None
                );

                ApplyTutorialRallySanityLoss(GetOpponentSide(playerSide));
            }
            else
            {
                TutorialPatternData pattern = patternProvider.GetRallyOpponentPattern(turn);

                yield return RunOpponentDemoAttackThenDefense(
                    pattern,
                    TutorialStep.RallyDefense
                );

                ApplyTutorialRallySanityLoss(playerSide);
            }

            attackTurnRenderer.ClearAll();
            hud?.ClearAttackProgress();
            hud?.ClearJudgments();
            hud?.ClearPanelMessages();

            yield return new WaitForSecondsRealtime(betweenTurnDelay);
        }
    }

    /// <summary>
    /// 기존 AttackTurn.StartLocalPlayerAttack을 사용해 플레이어 공격 턴을 재생한다.
    /// AttackTurn 내부에서 AttackTurnRenderer.BeginAttackVisual과 SpawnAttackNote가 호출된다.
    /// </summary>
    private IEnumerator RunPlayerAttackTurn(string message, int targetTapCount, TutorialStep inputStep)
    {
        currentStep = inputStep;
        currentHudAttackerSide = playerSide;
        PrepareAttackWaitState();

        attackTurnRenderer.ClearAll();
        hud?.ClearAttackProgress();
        hud?.ClearJudgments();
        hud?.ClearPanelMessages();
        gameCamera?.SetAttackView(playerSide);

        double startDspTime = AudioSettings.dspTime + Mathf.Max(0f, attackStartDelay);

        if (logTurnFlow)
        {
            Debug.Log($"TutorialManager: StartLocalPlayerAttack / side:{playerSide}, target:{targetTapCount}, start:{startDspTime:0.000}");
        }

        attackTurn.StartLocalPlayerAttack(playerSide, targetTapCount, message, startDspTime);

        yield return WaitAttackEnd();

        currentStep = TutorialStep.None;
    }

    /// <summary>
    /// 기존 AttackTurn.StartOpponentAttackDemo를 사용해 상대 데모 공격을 재생하고, 이어서 방어 턴을 시작한다.
    /// </summary>
    private IEnumerator RunOpponentDemoAttackThenDefense(
    TutorialDefensePattern pattern,
    TutorialStep defenseInputStep
    )
    {
        AttackSide attackerSide = GetOpponentSide(playerSide);

        currentStep = TutorialStep.None;
        currentHudAttackerSide = attackerSide;
        showCurrentDefenseKeyHints = pattern.showKeyHints;

        PrepareAttackWaitState();

        attackTurnRenderer.ClearAll();
        hud?.ClearAttackProgress();
        hud?.ClearJudgments();
        hud?.ClearPanelMessages();
        gameCamera?.SetAttackView(attackerSide);

        attackTurn.StartOpponentAttackDemo(
            pattern.message,
            pattern.notes
        );

        yield return WaitAttackEnd();

        showCurrentDefenseKeyHints = false;


        if (lastAttackNotes.Count == 0)
        {
            Debug.LogWarning("TutorialManager: 상대 데모 공격에서 생성된 노트가 없음.");
            yield break;
        }

        yield return RunDefenseTurnForExistingNotes(
            attackerSide,
            isAiDefense: false,
            inputStep: defenseInputStep
        );
    }

    /// <summary>
    /// 이미 AttackTurn이 생성한 노트를 기존 DefenseTurn.Begin으로 넘겨 방어 턴을 재생한다.
    /// TutorialManager가 직접 노트 이동/스폰을 하지 않는다.
    /// </summary>
    private IEnumerator RunDefenseTurnForExistingNotes(AttackSide attackerSide, bool isAiDefense, TutorialStep inputStep)
    {
        if (lastAttackNotes.Count == 0)
        {
            yield break;
        }

        currentStep = inputStep;
        currentDefenseAttackerSide = attackerSide;
        currentDefenseJudgmentIndex = 0;
        showDefenseJudgmentUi = !isAiDefense;
        currentDefenseIsAiDefense = isAiDefense;
        defenseEnded = false;
        
        if (!isAiDefense)
        {
            latestDefenseResultAvailable = false;
            latestDefenseTotalCount = 0;
            latestDefenseMissCount = 0;
        }
        gameCamera?.SetDefenseView(attackerSide);
        yield return new WaitForSecondsRealtime(viewTransitionDelay);

        float judgeLineX = attackTurnRenderer.GetJudgeLineX(attackerSide);
        float attackStartX = attackTurnRenderer.GetStartX(attackerSide);
        float attackEndX = attackTurnRenderer.GetEndX(attackerSide);

        if (logTurnFlow)
        {
            Debug.Log($"TutorialManager: Defense Begin / attacker:{attackerSide}, notes:{lastAttackNotes.Count}, ai:{isAiDefense}, duration:{lastAttackDuration:0.000}, attackStart:{lastAttackStartDspTime:0.000}");
        }

        defenseTurn.Begin(
            lastAttackNotes,
            judgeLineX,
            attackStartX,
            attackEndX,
            lastAttackDuration,
            isAiDefense: isAiDefense,
            networkManager: null,
            remoteAttackStartDspTime: lastAttackStartDspTime
        );

        float timeout = Mathf.Max(2f, (float)lastAttackDuration + 5f);
        float elapsed = 0f;

        while (!defenseEnded && elapsed < timeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!defenseEnded)
        {
            Debug.LogWarning("TutorialManager: DefenseTurn 종료 이벤트를 받지 못해 timeout으로 진행.");
        }

        showDefenseJudgmentUi = false;
        currentStep = TutorialStep.None;
    }

    private void PrepareAttackWaitState()
    {
        attackEnded = false;
        lastAttackNotes.Clear();
        lastAttackStartDspTime = 0.0;
        lastAttackDuration = 0.0;
    }

    private IEnumerator WaitAttackEnd()
    {
        float timeout = 10f;
        float elapsed = 0f;

        while (!attackEnded && elapsed < timeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!attackEnded)
        {
            Debug.LogWarning("TutorialManager: AttackTurn 종료 이벤트를 받지 못해 timeout으로 진행.");
        }
    }

    private void HandleAttackEnded(AttackResult result)
    {
        attackEnded = true;
        lastAttackNotes.Clear();

        if (result.Notes != null)
        {
            foreach (NoteData note in result.Notes)
            {
                lastAttackNotes.Add(note);
            }
        }

        lastAttackStartDspTime = attackTurn.AttackStartDspTime;
        lastAttackDuration = attackTurn.AttackDuration;

        Debug.Log($"TutorialManager: AttackEnded / notes:{lastAttackNotes.Count}, start:{lastAttackStartDspTime:0.000}, duration:{lastAttackDuration:0.000}");
    }

    private void HandleDefenseEnded(DefenseResult result)
    {
        defenseEnded = true;

        if (!currentDefenseIsAiDefense)
        {
            latestDefenseResultAvailable = true;
            latestDefenseMissCount = result.MissCount;
            latestDefenseTotalCount = result.Judgments == null ? 0 : result.Judgments.Length;

            Debug.Log(
                $"TutorialManager: DefenseResult 저장 / " +
                $"total:{latestDefenseTotalCount}, miss:{latestDefenseMissCount}"
            );
        }

        Debug.Log($"TutorialManager: DefenseEnded / miss:{result.MissCount}");
    }

    private void HandleAttackMessageSelected(string message, int targetCount)
    {
        hud?.ShowInAttackerPanel(message, currentHudAttackerSide);
        hud?.UpdateAttackProgress(0, targetCount);
    }

    private void HandleAttackProgressChanged(int currentCount, int targetCount)
    {
        hud?.UpdateAttackProgress(currentCount, targetCount);
    }

    private void HandleJudgment(Judgment judgment)
    {
        if (!showDefenseJudgmentUi)
        {
            return;
        }

        hud?.ShowJudgment(judgment, currentDefenseAttackerSide);

        if (judgment != Judgment.MISS)
        {
            hud?.SetStarSuccess(currentDefenseJudgmentIndex);
        }

        currentDefenseJudgmentIndex++;
    }

    private IEnumerator PlayDialogue(string[] lines)
    {
        if (dialoguePlayer == null)
        {
            yield break;
        }

        yield return dialoguePlayer.PlayLines(lines, guideBeatsPerLine);
    }

    /// <summary>
    /// 인트로 대사를 재생한다.
    /// 지정한 문장이 시작될 때 고주파/저주파 비트 소개 노트를 화면 중앙 부근에 띄운다.
    /// </summary>
    private IEnumerator PlayIntroDialogueWithBeatDemo()
    {
        if (dialoguePlayer == null)
        {
            yield break;
        }

        introBeatDemoStarted = false;

        yield return dialoguePlayer.PlayLines(
            introGuideLines,
            guideBeatsPerLine,
            hideWhenFinished: true,
            onLineStarted: HandleIntroLineStarted
        );
    }

    /// <summary>
    /// 인트로 문장 시작 시 호출된다.
    /// introBeatDemoLineNumber번째 문장에서 비트 소개 노트를 생성한다.
    /// </summary>
    private void HandleIntroLineStarted(int lineIndex, string lineText)
    {
        if (!playIntroBeatDemo)
        {
            return;
        }

        if (introBeatDemoStarted)
        {
            return;
        }

        int targetIndex = Mathf.Max(1, introBeatDemoLineNumber) - 1;

        if (lineIndex != targetIndex)
        {
            return;
        }

        introBeatDemoStarted = true;

        Debug.Log(
            $"TutorialManager: Intro beat demo 시작 / " +
            $"line:{lineIndex + 1}, text:{lineText}"
        );

        StartCoroutine(PlayIntroBeatDemo());
    }

    /// <summary>
    /// 인트로에서 고주파/저주파 노트를 순서대로 하나씩 생성한다.
    /// 실제 공격 턴을 시작하지 않고, AttackTurnRenderer의 노트 표시만 사용한다.
    /// </summary>
    private IEnumerator PlayIntroBeatDemo()
    {
        if (attackTurnRenderer == null)
        {
            yield break;
        }

        gameCamera?.SetAttackView(playerSide);

        SpawnIntroDemoNote(
            NoteType.HIGH,
            introHighNotePositionRatio
        );

        yield return new WaitForSecondsRealtime(
            GetTutorialBeatSeconds() * Mathf.Max(0, introSecondNoteDelayBeats)
        );

        SpawnIntroDemoNote(
            NoteType.LOW,
            introLowNotePositionRatio
        );
    }

    /// <summary>
    /// 인트로 소개용 노트를 공격 라인의 지정 비율 위치에 생성한다.
    /// positionRatio 0.5가 중앙이다.
    /// </summary>
    private void SpawnIntroDemoNote(NoteType noteType, float positionRatio)
    {
        double safeDuration = System.Math.Max(0.01, introBeatDemoDuration);
        double relativeTime = Mathf.Clamp01(positionRatio) * safeDuration;

        NoteData note = new NoteData
        {
            noteId = nextIntroDemoNoteId++,
            noteType = noteType,
            noteRelativeTime = relativeTime
        };

        attackTurnRenderer.SpawnAttackNote(
            playerSide,
            note,
            safeDuration
        );

        Debug.Log(
            $"TutorialManager: Intro demo note 생성 / " +
            $"id:{note.noteId}, type:{note.noteType}, ratio:{positionRatio:0.00}"
        );
    }

    /// <summary>
    /// 튜토리얼 현재 BPM 기준 1박 길이를 반환한다.
    /// </summary>
    private float GetTutorialBeatSeconds()
    {
        if (RhythmClock.Instance != null)
        {
            return (float)RhythmClock.Instance.GetBeatDuration();
        }

        return 60f / Mathf.Max(1f, tutorialBpm);
    }
    private void ResetTutorialSanity()
    {
        tutorialP1Sanity = tutorialMaxSanity;
        tutorialP2Sanity = tutorialMaxSanity;
    }

    private void SetSanityVisible(bool visible)
    {
        if (mySanityUiRoot != null)
        {
            mySanityUiRoot.SetActive(visible);
        }

        if (opponentSanityUiRoot != null)
        {
            opponentSanityUiRoot.SetActive(visible);
        }
    }

    private void UpdateTutorialSanityHud()
    {
        hud?.UpdateSanity(tutorialP1Sanity, tutorialP2Sanity, tutorialMaxSanity);
    }

    private void ApplyTutorialRallySanityLoss(AttackSide damagedSide)
    {
        if (damagedSide == AttackSide.P1)
        {
            tutorialP1Sanity = Mathf.Max(tutorialMinimumSanity, tutorialP1Sanity - rallySanityLossPerTurn);
        }
        else
        {
            tutorialP2Sanity = Mathf.Max(tutorialMinimumSanity, tutorialP2Sanity - rallySanityLossPerTurn);
        }

        UpdateTutorialSanityHud();
    }

    private int GetPlayerId(AttackSide side)
    {
        return side == AttackSide.P1 ? 1 : 2;
    }

    private AttackSide GetOpponentSide(AttackSide side)
    {
        return side == AttackSide.P1 ? AttackSide.P2 : AttackSide.P1;
    }

    private void CompleteTutorial()
    {
        dialoguePlayer?.Show("튜토리얼 완료! 이제 본 교신을 시작할 수 있어.");

        PlayerPrefs.SetInt(TutorialCompletedKey, 1);
        PlayerPrefs.Save();

        attackTurnRenderer.ClearAll();
        hud?.ClearAttackProgress();
        hud?.ClearJudgments();
        hud?.ClearPanelMessages();

        Debug.Log("TutorialManager: Complete");
    }

    private bool ValidateReferences()
    {
        bool valid = true;

        if (playerSide != AttackSide.P1)
        {
            Debug.LogWarning("TutorialManager: 현재 AttackTurn.StartOpponentAttackDemo가 P2 기준이라 Player Side는 P1 권장.");
        }

        if (attackTurn == null)
        {
            Debug.LogError("TutorialManager: attackTurn 연결 필요.");
            valid = false;
        }

        if (defenseTurn == null)
        {
            Debug.LogError("TutorialManager: defenseTurn 연결 필요.");
            valid = false;
        }

        if (attackTurnRenderer == null)
        {
            Debug.LogError("TutorialManager: attackTurnRenderer 연결 필요.");
            valid = false;
        }

        if (gameCamera == null)
        {
            Debug.LogError("TutorialManager: gameCamera 연결 필요.");
            valid = false;
        }

        if (hud == null)
        {
            Debug.LogError("TutorialManager: hud 연결 필요.");
            valid = false;
        }

        if (dialoguePlayer == null)
        {
            Debug.LogError("TutorialManager: dialoguePlayer 연결 필요.");
            valid = false;
        }

        if (patternProvider == null)
        {
            Debug.LogError("TutorialManager: patternProvider 연결 필요.");
            valid = false;
        }

        return valid;
    }

    /// <summary>
    /// 방어 연습 결과에 따라 튜토리얼 반응 문장을 표시한다.
    /// 턴 진행을 멈추지 않는다.
    /// </summary>
    private void ShowDefensePracticeReaction(int practiceIndex)
    {
        if (dialoguePlayer == null)
        {
            return;
        }

        string reaction = GetDefensePracticeReaction(practiceIndex);

        dialoguePlayer.ShowReactionForBeats(
            reaction,
            defenseReactionBeats
        );
    }

    /// <summary>
    /// 방어 연습용 패턴을 반환한다.
    /// 1회차: F 1개, 키 힌트 표시
    /// 2회차: J 1개, 키 힌트 표시
    /// 3회차: 짧은 패턴, 키 힌트 숨김
    /// </summary>
    private TutorialDefensePattern GetDefensePracticePattern(int index)
    {
        switch (index)
        {
            case 0:
                return new TutorialDefensePattern(
                    "응",
                    new[] { NoteType.HIGH },
                    showKeyHints: true
                );

            case 1:
                return new TutorialDefensePattern(
                    "나",
                    new[] { NoteType.LOW },
                    showKeyHints: true
                );

            default:
                return new TutorialDefensePattern(
                    "반가워",
                    new[] { NoteType.HIGH, NoteType.LOW, NoteType.HIGH },
                    showKeyHints: false
                );
        }
    }

    /// <summary>
    /// 최근 방어 결과를 기준으로 피드백 문장을 만든다.
    /// </summary>
    private string GetDefensePracticeReaction(int practiceIndex)
    {
        if (!latestDefenseResultAvailable)
        {
            return "결과를 확인하지 못했지만, 다음 연습으로 넘어가볼게.";
        }

        if (latestDefenseTotalCount <= 0)
        {
            return "아직 받아칠 비트가 없었어. 다음 신호를 다시 확인해보자.";
        }

        if (latestDefenseMissCount == 0)
        {
            switch (practiceIndex)
            {
                case 0:
                    return "좋아! 첫 신호를 정확히 받아쳤어.";
                case 1:
                    return "잘하고 있어. 비트 종류도 잘 구분했어!";
                default:
                    return "완벽해! 이제 실전 랠리로 넘어가도 되겠어.";
            }
        }

        if (latestDefenseMissCount < latestDefenseTotalCount)
        {
            return "괜찮아, 몇 개는 받아쳤어. 다음엔 판정선에 닿는 순간을 더 노려보자.";
        }

        switch (practiceIndex)
        {
            case 0:
                return "괜찮아. 지금은 타이밍을 익히는 단계야. 판정선에 닿을 때 눌러보자.";
            case 1:
                return "이번엔 놓쳤지만 괜찮아. 고주파는 F, 저주파는 J를 기억해.";
            default:
                return "연습이니까 괜찮아. 실전 랠리에서도 정신력은 조금만 줄어들 거야.";
        }
    }

    private void HandleTutorialAttackNoteCreated(NoteData note)
    {
        if (NoteRenderer.Instance == null)
            return;

        if (!showCurrentDefenseKeyHints)
        {
            NoteRenderer.Instance.HideKeyHint(note.noteId);
            return;
        }

        string key = note.noteType == NoteType.HIGH ? "F" : "J";
        Color color = note.noteType == NoteType.HIGH
            ? new Color(0.45f, 0.9f, 1f)
            : new Color(1f, 0.25f, 0.25f);

        NoteRenderer.Instance.ShowKeyHint(note.noteId, key, color);
    }
    
    /// <summary>
    /// 일반 튜토리얼 패턴을 상대 데모 공격으로 실행한다.
    /// 랠리 단계에서는 키 힌트를 표시하지 않는다.
    /// </summary>
    private IEnumerator RunOpponentDemoAttackThenDefense(
        TutorialPatternData pattern,
        TutorialStep defenseInputStep
    )
    {   
        if (pattern == null || pattern.NoteCount <= 0)
        {
            Debug.LogWarning("TutorialManager: 비어 있는 방어 데모 패턴.");
            yield break;
        }

        if (pattern == null || pattern.NoteCount <= 0)
        {
            Debug.LogWarning("TutorialManager: 비어 있는 상대 데모 패턴.");
            yield break;
        }

        TutorialDefensePattern defensePattern = new TutorialDefensePattern(
            pattern.message,
            pattern.notes,
            showKeyHints: false
        );

        yield return RunOpponentDemoAttackThenDefense(
            defensePattern,
            defenseInputStep
        );
    }
}
/// <summary>
/// 튜토리얼 방어 연습용 메시지/노트 패턴/키 힌트 표시 여부를 함께 담는다.
/// </summary>
[System.Serializable]
public class TutorialDefensePattern
{
    public string message;
    public NoteType[] notes;
    public bool showKeyHints;

    public int NoteCount => notes == null ? 0 : notes.Length;

    public TutorialDefensePattern(string message, NoteType[] notes, bool showKeyHints)
    {
        this.message = message;
        this.notes = notes;
        this.showKeyHints = showKeyHints;
    }

    
}