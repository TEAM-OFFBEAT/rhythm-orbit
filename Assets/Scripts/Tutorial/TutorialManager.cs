
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using System;

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

    [SerializeField] private GameObject lobbyButtonRoot;
    [SerializeField] private string lobbySceneName = "Lobby";
    
    [Header("UI")]
    [SerializeField] private HUD hud;
    [SerializeField] private JudgmentLabel p1DefenseJudgmentLabel;
    [SerializeField] private JudgmentLabel p2DefenseJudgmentLabel;
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
    
    [Tooltip("4박 경계선을 이 시간만큼 살짝 지난 경우에도 같은 경계선으로 인정한다. 공격 연습 시작 전 긴 공백 방지용.")]
    [SerializeField, Min(0f)] private float guideBoundaryLateGraceSeconds = 0.25f;

    [Header("Tutorial Sanity")]
    [SerializeField] private int tutorialMaxSanity = 100;
    [SerializeField] private int rallySanityLossPerTurn = 1;
    [SerializeField] private int tutorialMinimumSanity = 90;

    [Header("Defense Reaction")]
    [SerializeField] private int defenseReactionBeats = 4;

    [Header("Tutorial Sound")]
    [SerializeField] private bool playTutorialBgmOnStart = false;
    [SerializeField] private BgmId tutorialBgmId = BgmId.Tutorial;

    [SerializeField] private bool playHitSfx = true;
    [SerializeField] private bool playMissSfx = false;

    [Header("Tutorial Metronome Test")]
    [SerializeField] private bool playGuideMetronome = true;
    [SerializeField] private AudioClip guideMetronomeClip;
    [SerializeField] private AudioSource guideMetronomeLoopSource;

    [SerializeField, Min(1)] private int guideMetronomeIntervalBeats = 4;

    [Tooltip("튜토리얼 시작과 첫 메트로놈을 정확히 맞추기 위한 예약 여유 시간.")]
    [SerializeField, Min(0f)] private float tutorialStartLeadTime = 1f;

    [Header("Tutorial Direct Hit SFX")]
    [SerializeField] private bool useDirectTutorialHitSfx = true;
    [SerializeField] private AudioClip directHitHighClip;
    [SerializeField] private AudioClip directHitLowClip;
    [SerializeField, Range(0f, 1f)] private float directHitVolume = 1f;
    [SerializeField, Min(1)] private int directHitSourceCount = 4;

    private AudioSource[] directHitSources;
    private int nextDirectHitSourceIndex;
    private double tutorialStartDspTime;
    private bool latestDefenseResultAvailable;
    private int latestDefenseTotalCount;
    private int latestDefenseMissCount;
    private AudioClip generatedGuideMetronomeLoopClip;

    [Header("Practice Start Messages")]
    [SerializeField] private string attackPracticeStartMessage = "공격 연습 시작!";
    [SerializeField] private string defensePracticeStartMessage = "방어 연습 시작!";
    [SerializeField] private string rallyPracticeStartMessage = "랠리 연습 시작!";

    [SerializeField, Min(0)] private int practiceStartMessageBeats = 1;

    [Header("Attack Beat Demo")]
    [SerializeField] private bool playAttackBeatDemo = true;

    [Tooltip("공격턴 설명 몇 번째 문장에서 고주파 비트를 보여줄지 설정한다. 1부터 시작한다.")]
    [SerializeField, Min(1)] private int attackHighBeatDemoLineNumber = 4;

    [Tooltip("공격턴 설명 몇 번째 문장에서 저주파 비트를 보여줄지 설정한다. 1부터 시작한다.")]
    [SerializeField, Min(1)] private int attackLowBeatDemoLineNumber = 5;

    [Tooltip("고주파 노트가 공격 라인에서 생성될 위치 비율.0.5가 중앙이다.")]
    [SerializeField, Range(0f, 1f)] private float demoHighNotePositionRatio = 0.47f;

    [Tooltip("저주파 노트가 공격 라인에서 생성될 위치 비율. 0.5가 중앙이다.")]
    [SerializeField, Range(0f, 1f)] private float demoLowNotePositionRatio = 0.53f;

    [SerializeField] private double beatDemoDuration = 2.0;
    [SerializeField] private int demoFirstNoteId = 900000;

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
    private string currentAttackMessage = "";
    private bool showDefenseJudgmentUi;
    private bool currentDefenseIsAiDefense;
    private bool showCurrentDefenseKeyHints;

    private int currentDefenseJudgmentIndex;
    

    private int tutorialP1Sanity;
    private int tutorialP2Sanity;

    private double lastAttackStartDspTime;
    private double lastAttackDuration;
    private bool attackHighBeatDemoStarted;
    private bool attackLowBeatDemoStarted;
    private int nextDemoNoteId;

    private readonly List<NoteData> lastAttackNotes = new List<NoteData>();

    private const string TutorialCompletedKey = "TutorialCompleted";

    private void OnEnable()
    {
        SubscribeEvents();
    }

    private void OnDisable()
    {
        StopGuideMetronome();
        UnsubscribeEvents();
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
            attackTurn.OnAttackNoteCreated += HandleTutorialAttackNoteCreated;
        }

        if (defenseTurn != null)
        {
            defenseTurn.OnDefenseEnded += HandleDefenseEnded;
            defenseTurn.OnJudgment += HandleJudgment;
            defenseTurn.OnDefenseInputResolved += HandleDefenseInputResolved;
        }
    }

    private void UnsubscribeEvents()
    {
        if (attackTurn != null)
        {
            attackTurn.OnAttackEnded -= HandleAttackEnded;
            attackTurn.OnAttackMessageSelected -= HandleAttackMessageSelected;
            attackTurn.OnAttackProgressChanged -= HandleAttackProgressChanged;
            attackTurn.OnAttackNoteCreated -= HandleTutorialAttackNoteCreated;
        }

        if (defenseTurn != null)
        {
            defenseTurn.OnDefenseEnded -= HandleDefenseEnded;
            defenseTurn.OnJudgment -= HandleJudgment;
            defenseTurn.OnDefenseInputResolved -= HandleDefenseInputResolved;
        }
    }

    /// <summary>
    /// 인트로 설명 → 공격 설명 → 공격연습 → 방어 설명 → 방어연습 → 랠리 설명 → 랠리연습 순서로 진행한다.
    /// </summary>
    private IEnumerator RunTutorial()
    {
         InitializeTutorial();

        yield return WaitUntilDspTime(tutorialStartDspTime);

        currentStep = TutorialStep.IntroDialogue;
        yield return PlayDialogue(introGuideLines);

        attackTurnRenderer.ClearAll();

        currentStep = TutorialStep.AttackDialogue;
        yield return PlayAttackDialogueWithBeatDemo();
        yield return RunAttackPractice();

        currentStep = TutorialStep.DefenseDialogue;
        yield return PlayDefenseDialogueWithAttackTransfer();

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
        tutorialStartDspTime = AudioSettings.dspTime + Mathf.Max(0f, tutorialStartLeadTime);

        RhythmClock.Instance?.SetBpm(tutorialBpm);
        RhythmClock.Instance?.StartClock(tutorialStartDspTime);

        TryPlayTutorialBgm();
        StartGuideMetronome(tutorialStartDspTime);
        PrepareDirectHitSources();

        dialoguePlayer?.SetBpm(tutorialBpm);
        dialoguePlayer?.Hide();

        if (lobbyButtonRoot != null)
        {
            lobbyButtonRoot.SetActive(false);
        }

        attackTurnRenderer.SetLocalPlayer(GetPlayerId(playerSide));
        attackTurnRenderer.ClearAll();
        
        attackHighBeatDemoStarted = false;
        attackLowBeatDemoStarted = false;
        nextDemoNoteId = demoFirstNoteId;

        hud?.SetupPlayerPerspective(GetPlayerId(playerSide));
        hud?.UpdateBpm(tutorialBpm);
        hud?.ClearAttackProgress();
        hud?.ClearJudgments();
        hud?.ClearPanelMessages();

        ResetTutorialSanity();
        SetSanityVisible(false);
        UpdateTutorialSanityHud();

        gameCamera?.SetAttackView(playerSide);

        if (NetworkManager.Instance != null && NetworkManager.Instance.IsConnected)
        {
            Debug.LogWarning("TutorialManager: TutorialScene에 연결된 NetworkManager.Instance가 있음. 튜토리얼에서는 비활성화 권장.");
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
        if (logTurnFlow)
        {
            Debug.Log("TutorialManager: AttackPractice 시작");
        }

        ShowPracticeStartMessage(attackPracticeStartMessage);

        bool success = false;
        bool feedbackVisible = false;

        double intervalSeconds = GetGuideMetronomeIntervalSeconds();
        double nextAttackStartDspTime = GetCurrentOrNextGuideBoundaryDspTime(AudioSettings.dspTime);

        while (!success)
        {
            TutorialPatternData pattern = patternProvider.GetAttackPracticePattern();

            // 예약 시간이 너무 많이 지나갔으면 다음 4박 경계로 보정.
            while (nextAttackStartDspTime < AudioSettings.dspTime - guideBoundaryLateGraceSeconds)
            {
                nextAttackStartDspTime += intervalSeconds;
            }

            // 첫 공격 연습도 4박 경계선 기준으로 시작.
            // 단, 경계선을 아주 살짝 지난 경우는 바로 시작해서 긴 공백을 막는다.
            yield return WaitUntilDspTime(nextAttackStartDspTime);

            yield return RunPlayerAttackTurn(
                pattern.message,
                pattern.NoteCount,
                TutorialStep.AttackPractice,
                nextAttackStartDspTime
            );

            success = lastAttackNotes.Count > 0;

            if (!success)
            {
                if (!feedbackVisible)
                {
                    dialoguePlayer?.Show("다음 단계로 넘어가기 위해 F나 J를 눌러보라모.");
                    feedbackVisible = true;
                }

                // 실패해도 피드백을 숨기지 않고, 다음 4박 경계에서 바로 재시도.
                nextAttackStartDspTime += intervalSeconds;
                continue;
            }
        }

        if (feedbackVisible)
        {
            dialoguePlayer?.Hide();
        }

        // 여기서 바로 방어 설명 단계로 넘어간다.
        // lastAttackNotes는 다음 PlayDefenseDialogueWithAttackTransfer에서 사용해야 하므로 지우지 않는다.
    }

    private IEnumerator RunDefensePractice()
    {
        if (logTurnFlow)
        {
            Debug.Log("TutorialManager: DefensePractice 시작");
        }

        ShowPracticeStartMessage(defensePracticeStartMessage);

        double fourBeatSeconds = GetGuideMetronomeIntervalSeconds();

        // 방어연습 1회 = 상대 공격 4박 + 내 방어 4박 = 8박
        double practiceSlotSeconds = fourBeatSeconds * 2.0;

        double nextPracticeStartDspTime =
            GetCurrentOrNextGuideBoundaryDspTime(AudioSettings.dspTime);

        for (int i = 0; i < 3; i++)
        {
            while (nextPracticeStartDspTime < AudioSettings.dspTime - guideBoundaryLateGraceSeconds)
            {
                nextPracticeStartDspTime += practiceSlotSeconds;
            }

            yield return WaitUntilDspTime(nextPracticeStartDspTime);

            TutorialDefensePattern pattern = GetDefensePracticePattern(i);

            yield return RunOpponentDemoAttackThenDefense(
                pattern,
                TutorialStep.DefensePractice,
                forcedAttackStartDspTime: nextPracticeStartDspTime,
                useDefenseViewDelay: false
            );

            bool isLastPractice = i == 2;

            if (!isLastPractice)
            {
                // 추가 대기 없이 다음 턴 위에 자연스럽게 겹쳐서 표시
                ShowDefensePracticeReaction(i);
            }

            nextPracticeStartDspTime += practiceSlotSeconds;
        }

        currentStep = TutorialStep.None;
    }

    private IEnumerator RunRallyPractice()
    {
        if (logTurnFlow)
        {
            Debug.Log("TutorialManager: RallyPractice 시작");
        }

        ShowPracticeStartMessage(rallyPracticeStartMessage);

        ResetTutorialSanity();
        SetSanityVisible(true);
        UpdateTutorialSanityHud();

        double fourBeatSeconds = GetGuideMetronomeIntervalSeconds();
        double nextTurnStartDspTime = GetCurrentOrNextGuideBoundaryDspTime(AudioSettings.dspTime);

        // 랠리 설명이 방금 끝난 직후라면 바로 현재 4박 경계로 붙이고,
        // 너무 늦었으면 다음 4박 경계로 보정.
        while (nextTurnStartDspTime < AudioSettings.dspTime - guideBoundaryLateGraceSeconds)
        {
            nextTurnStartDspTime += fourBeatSeconds;
        }

        yield return WaitUntilDspTime(nextTurnStartDspTime);

        for (int turn = 0; turn < rallyTurnCount; turn++)
        {
            bool isPlayerAttack = turn % 2 == 0;

            if (isPlayerAttack)
            {
                TutorialPatternData pattern = patternProvider.GetRallyPlayerPattern(turn);

                if (pattern == null || pattern.NoteCount <= 0)
                {
                    Debug.LogError($"TutorialManager: RallyPlayerPattern이 비어 있음. turn:{turn}");
                    yield break;
                }

                yield return RunPlayerAttackTurn(
                    pattern.message,
                    pattern.NoteCount,
                    TutorialStep.RallyAttack,
                    nextTurnStartDspTime
                );

                yield return RunDefenseTurnForExistingNotes(
                    attackerSide: playerSide,
                    isAiDefense: true,
                    inputStep: TutorialStep.None,
                    useViewTransitionDelay: false
                );

                ApplyTutorialRallySanityLoss(GetOpponentSide(playerSide));
            }
            else
            {
                TutorialPatternData pattern = patternProvider.GetRallyOpponentPattern(turn);

                if (pattern == null || pattern.NoteCount <= 0)
                {
                    Debug.LogError($"TutorialManager: RallyOpponentPattern이 비어 있음. turn:{turn}");
                    yield break;
                }

                yield return RunOpponentDemoAttackThenDefense(
                    pattern,
                    TutorialStep.RallyDefense,
                    forcedAttackStartDspTime: nextTurnStartDspTime,
                    useDefenseViewDelay: false
                );

                ApplyTutorialRallySanityLoss(playerSide);
            }

            attackTurnRenderer.ClearAll();
            hud?.ClearAttackProgress();
            hud?.ClearJudgments();
            // 패널 메시지는 방어 종료 직후 표시되므로 여기서 지우지 않는다.
            // 각 턴 시작 시 RunPlayerAttackTurn / RunOpponentDemoAttackThenDefense 안에서 ClearPanelMessages()를 호출한다.

            // 랠리 1턴 = 공격 4박 + 방어 4박 = 8박
            nextTurnStartDspTime += fourBeatSeconds * 2.0;

            // 기존 betweenTurnDelay는 쓰지 않음.
            // 다음 턴 시작 시점이 이미 8박 단위로 정해져 있기 때문.
            while (nextTurnStartDspTime < AudioSettings.dspTime - guideBoundaryLateGraceSeconds)
            {
                nextTurnStartDspTime += fourBeatSeconds * 2.0;
            }

            yield return WaitUntilDspTime(nextTurnStartDspTime);
        }
    }

    /// <summary>
    /// 기존 AttackTurn.StartLocalPlayerAttack을 사용해 플레이어 공격 턴을 재생한다.
    /// AttackTurn 내부에서 AttackTurnRenderer.BeginAttackVisual과 SpawnAttackNote가 호출된다.
    /// </summary>
    private IEnumerator RunPlayerAttackTurn(
    string message,
    int targetTapCount,
    TutorialStep inputStep,
    double? forcedStartDspTime = null
    )
    {
        currentStep = inputStep;
        currentHudAttackerSide = playerSide;
        PrepareAttackWaitState();

        attackTurnRenderer.ClearAll();
        hud?.ClearAttackProgress();
        hud?.ClearJudgments();
        gameCamera?.SetAttackView(playerSide);
        hud?.SetTurnOwner(GetPlayerId(playerSide));

        double startDspTime = forcedStartDspTime
        ?? AudioSettings.dspTime + Mathf.Max(0f, attackStartDelay);
        if (logTurnFlow)
        {
            Debug.Log($"TutorialManager: StartLocalPlayerAttack / side:{playerSide}, target:{targetTapCount}, start:{startDspTime:0.000}");
        }

        attackTurn.StartLocalPlayerAttack(playerSide, targetTapCount, message, startDspTime);

        yield return WaitAttackEnd();

        currentStep = TutorialStep.None;
    }

    /// <summary>
    /// 기존 AttackTurn.StartOpponentAttackDemo를 사용해 상대 데모 공격을 재생하고,
    /// 이어서 방어 턴을 시작한다.
    /// </summary>
    private IEnumerator RunOpponentDemoAttackThenDefense(
        TutorialDefensePattern pattern,
        TutorialStep defenseInputStep,
        double? forcedAttackStartDspTime = null,
        bool useDefenseViewDelay = true
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
        gameCamera?.SetAttackView(attackerSide);
        hud?.SetTurnOwner(GetPlayerId(attackerSide));

        double attackStartDspTime = forcedAttackStartDspTime
            ?? AudioSettings.dspTime;

        attackTurn.StartOpponentAttackDemo(
            pattern.message,
            pattern.notes,
            attackStartDspTime
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
            inputStep: defenseInputStep,
            useViewTransitionDelay: useDefenseViewDelay
        );
    }

    /// <summary>
    /// 이미 AttackTurn이 생성한 노트를 기존 DefenseTurn.Begin으로 넘겨 방어 턴을 재생한다.
    /// TutorialManager가 직접 노트 이동/스폰을 하지 않는다.
    /// </summary>
    private IEnumerator RunDefenseTurnForExistingNotes(
    AttackSide attackerSide,
    bool isAiDefense,
    TutorialStep inputStep,
    bool useViewTransitionDelay = true
    )
    {
        if (lastAttackNotes.Count == 0)
        {
            yield break;
        }

        currentStep = inputStep;
        currentDefenseAttackerSide = attackerSide;
        currentDefenseJudgmentIndex = 0;
        showDefenseJudgmentUi = true;
        currentDefenseIsAiDefense = isAiDefense;
        defenseEnded = false;
        
        if (!isAiDefense)
        {
            latestDefenseResultAvailable = false;
            latestDefenseTotalCount = 0;
            latestDefenseMissCount = 0;
        }
        gameCamera?.SetDefenseView(attackerSide);
        hud?.SetTurnOwner(GetPlayerId(GetOpponentSide(attackerSide)));

        if (useViewTransitionDelay && viewTransitionDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(viewTransitionDelay);
        }

        float judgeLineX = attackTurnRenderer.GetJudgeLineX(attackerSide);
        float attackStartX = attackTurnRenderer.GetStartX(attackerSide);
        float attackEndX = attackTurnRenderer.GetEndX(attackerSide);
        GetDefenseLabel(attackerSide)?.SetWorldX(judgeLineX);

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

        if (!string.IsNullOrEmpty(currentAttackMessage))
        {
            string decoded = BuildDecodedMessage(currentAttackMessage, result.Judgments);
            // 튜토리얼은 방어자 패널에 표시 (ShowInDefenderPanel은 공격자 패널 기준이므로 직접 분기)
            bool defenderIsLocalPlayer = currentDefenseAttackerSide != playerSide;
            if (defenderIsLocalPlayer)
                hud?.ShowMyPanelMessage(decoded);
            else
                hud?.ShowOpponentPanelMessage(decoded);
        }

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
        currentAttackMessage = message;
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
        GetDefenseLabel(currentDefenseAttackerSide)?.ShowJudgment(judgment);

        // AI 방어는 DefenseTurn이 OnDefenseInputResolved를 발행하지 않으므로 SFX를 직접 재생한다.
        if (currentDefenseIsAiDefense)
            PlayAiDefenseSfx(judgment, currentDefenseJudgmentIndex);

        if (judgment != Judgment.MISS)
        {
            hud?.SetStarSuccess(currentDefenseJudgmentIndex);
        }

        currentDefenseJudgmentIndex++;
    }

    private void PlayAiDefenseSfx(Judgment judgment, int noteIndex)
    {
        if (judgment == Judgment.MISS)
        {
            if (playMissSfx && SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySfx(SfxId.Miss);
            }

            return;
        }

        if (!playHitSfx)
        {
            return;
        }

        NoteType noteType = noteIndex < lastAttackNotes.Count
            ? lastAttackNotes[noteIndex].noteType
            : NoteType.HIGH;

        PlayTutorialHitSfx(noteType);
    }

    private string BuildDecodedMessage(string attackMsg, Judgment[] judgments)
    {
        char[] chars = attackMsg.ToCharArray();
        bool allSuccess = true;
        int limit = Mathf.Min(chars.Length, judgments?.Length ?? 0);
        for (int i = 0; i < limit; i++)
        {
            if (judgments[i] == Judgment.MISS) { chars[i] = '▨'; allSuccess = false; }
        }
        for (int i = limit; i < chars.Length; i++)
            chars[i] = '▨';
        return new string(chars) + (allSuccess ? "!" : "?");
    }

    private JudgmentLabel GetDefenseLabel(AttackSide attackerSide)
        => attackerSide == AttackSide.P1 ? p2DefenseJudgmentLabel : p1DefenseJudgmentLabel;

    private IEnumerator PlayDialogue(string[] lines)
    {
        if (dialoguePlayer == null)
        {
            yield break;
        }

        double startDspTime = GetCurrentOrNextGuideBoundaryDspTime(AudioSettings.dspTime);

        yield return dialoguePlayer.PlayLines(
            lines,
            guideBeatsPerLine,
            hideWhenFinished: true,
            onLineStarted: null,
            forcedStartDspTime: startDspTime
        );
    }

    private IEnumerator PlayDialogue(string[] lines, double forcedStartDspTime)
    {
        if (dialoguePlayer == null)
        {
            yield break;
        }

        yield return dialoguePlayer.PlayLines(
            lines,
            guideBeatsPerLine,
            hideWhenFinished: true,
            onLineStarted: null,
            forcedStartDspTime: forcedStartDspTime
        );
    }

    /// <summary>
    /// 공격턴 설명 대사를 재생한다.
    /// 지정한 문장이 시작될 때 고주파/저주파 비트 소개 노트를 각각 표시한다.
    /// </summary>
    private IEnumerator PlayAttackDialogueWithBeatDemo()
    {
        if (dialoguePlayer == null)
        {
            yield break;
        }

        attackHighBeatDemoStarted = false;
        attackLowBeatDemoStarted = false;

        double startDspTime = GetCurrentOrNextGuideBoundaryDspTime(AudioSettings.dspTime);

        yield return dialoguePlayer.PlayLines(
            attackGuideLines,
            guideBeatsPerLine,
            hideWhenFinished: true,
            onLineStarted: HandleAttackGuideLineStarted,
            forcedStartDspTime: startDspTime
        );
    }


    /// <summary>
    /// 비트 소개용 노트를 공격 라인의 지정 비율 위치에 생성한다.
    /// positionRatio 0.5가 중앙이다.
    /// </summary>
    private void SpawnBeatDemoNote(NoteType noteType, float positionRatio)
    {
        if (attackTurnRenderer == null)
        {
            return;
        }

        gameCamera?.SetAttackView(playerSide);

        double safeDuration = System.Math.Max(0.01, beatDemoDuration);
        double relativeTime = Mathf.Clamp01(positionRatio) * safeDuration;

        NoteData note = new NoteData
        {
            noteId = nextDemoNoteId++,
            noteType = noteType,
            noteRelativeTime = relativeTime
        };

        attackTurnRenderer.SpawnAttackNote(
            playerSide,
            note,
            safeDuration
        );

        Debug.Log(
            $"TutorialManager: Beat demo note 생성 / " +
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
        StopGuideMetronome();

        dialoguePlayer?.Show("튜토리얼 완료! 이제 본 교신을 시작할 수 있어.");

        PlayerPrefs.SetInt(TutorialCompletedKey, 1);
        PlayerPrefs.Save();

        attackTurnRenderer.ClearAll();
        hud?.ClearAttackProgress();
        hud?.ClearJudgments();
        hud?.ClearPanelMessages();

        if (lobbyButtonRoot != null)
        {
            lobbyButtonRoot.SetActive(true);
        }

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
            return "결과를 확인하지 못했지만, 다음 연습으로 넘어가보겠다모.";
        }

        if (latestDefenseTotalCount <= 0)
        {
            return "아직 받아칠 비트가 없었다모. 다음 신호를 다시 확인해보자모.";
        }

        if (latestDefenseMissCount == 0)
        {
            switch (practiceIndex)
            {
                case 0:
                    return "첫 신호를 정확히 받아쳤다모!";
                case 1:
                    return "이대로 한 번 더!";
                default:
                    return "아주 좋다모!";
            }
        }

        if (latestDefenseMissCount < latestDefenseTotalCount)
        {
            return "괜찮다모. 다음번엔 완벽히 받아칠 수 있을 거다모.";
        }

        switch (practiceIndex)
        {
            case 0:
                return "괜찮다모. 지금은 타이밍을 익히는 단계다모!";
            case 1:
                return "고주파는 F, 저주파는 J를 기억하라모!.";
            default:
                return "연습은 실전에서도 이어진다모!";
        }
    }

    private void HandleTutorialAttackNoteCreated(NoteData note)
    {
        PlayTutorialHitSfx(note.noteType);

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
    TutorialStep defenseInputStep,
    double? forcedAttackStartDspTime = null,
    bool useDefenseViewDelay = false
    )
    {
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
            defenseInputStep,
            forcedAttackStartDspTime,
            useDefenseViewDelay
        );
    }

    /// <summary>
    /// 튜토리얼 BGM 재생 자리.
    /// 아직 BGM이 없으면 playTutorialBgmOnStart를 꺼두면 된다.
    /// </summary>
    private void TryPlayTutorialBgm()
    {
        if (!playTutorialBgmOnStart)
        {
            return;
        }

        SoundManager.Instance?.PlayBgm(tutorialBgmId);
    }

    /// <summary>
    /// 방어 입력 결과에 따라 타격음을 재생한다.
    /// AI 자동 방어는 DefenseTurn 쪽에서 이 이벤트를 발행하지 않으므로 중복 재생되지 않는다.
    /// </summary>
    private void HandleDefenseInputResolved(NoteType noteType, bool isSuccess)
    {
        PlayNoteSfx(noteType, isSuccess);
    }

    /// <summary>
    /// 고주파/저주파 성공 타격음과 선택적 MISS 효과음을 재생한다.
    /// </summary>
    private void PlayNoteSfx(NoteType noteType, bool isSuccess)
    {
        if (SoundManager.Instance == null)
        {
            return;
        }

        if (!isSuccess)
        {
            if (playMissSfx)
            {
                SoundManager.Instance.PlaySfx(SfxId.Miss);
            }

            return;
        }

        if (!playHitSfx)
        {
            return;
        }

        SfxId sfxId = noteType == NoteType.HIGH
            ? SfxId.HitHigh
            : SfxId.HitLow;

        Debug.Log(
            $"[Tutorial SFX] Defense Hit / sfx:{sfxId}, note:{noteType}, " +
            $"frame:{Time.frameCount}, dsp:{AudioSettings.dspTime:0.000}"
        );

        PlayDirectTutorialHitSfx(noteType);
    }

    private void PlayNoteHitSfxThroughSoundManager(NoteType noteType)
    {
        if (!playHitSfx)
            return;

        if (SoundManager.Instance == null)
            return;

        SfxId sfxId = noteType == NoteType.HIGH
            ? SfxId.HitHigh
            : SfxId.HitLow;


        SoundManager.Instance.PlaySfx(sfxId);
    }

    /// <summary>
    /// 튜토리얼 완료 후 로비 씬으로 이동한다.
    /// 완료 버튼 OnClick에서 호출한다.
    /// </summary>
    public void GoToLobby()
    {
        if (string.IsNullOrWhiteSpace(lobbySceneName))
        {
            Debug.LogError("TutorialManager: lobbySceneName이 비어 있음.");
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(lobbySceneName);
    }

    /// <summary>
    /// 튜토리얼 검수용 4박 메트로놈 루프를 시작한다.
    /// 원본 메트로놈 클립 뒤에 무음을 붙인 임시 루프 클립을 만들어 재생한다.
    /// </summary>
    private void StartGuideMetronome(double startDspTime)
    {
        StopGuideMetronome();

        if (!playGuideMetronome)
        {
            return;
        }

        if (guideMetronomeClip == null)
        {
            Debug.LogWarning("TutorialManager: guideMetronomeClip이 없어 검수용 메트로놈을 재생하지 않음.");
            return;
        }

        if (guideMetronomeLoopSource == null)
        {
            guideMetronomeLoopSource = gameObject.AddComponent<AudioSource>();
        }

        guideMetronomeLoopSource.playOnAwake = false;
        guideMetronomeLoopSource.loop = true;
        guideMetronomeLoopSource.spatialBlend = 0f;
        guideMetronomeLoopSource.volume = 1f;
        guideMetronomeLoopSource.pitch = 1f;
        guideMetronomeLoopSource.panStereo = 0f;
        guideMetronomeLoopSource.spatialBlend = 0f;
        guideMetronomeLoopSource.outputAudioMixerGroup = null;

        generatedGuideMetronomeLoopClip = CreateGuideMetronomeLoopClip();

        if (generatedGuideMetronomeLoopClip == null)
        {
            StopGuideMetronome();
            return;
        }

        guideMetronomeLoopSource.clip = generatedGuideMetronomeLoopClip;
        guideMetronomeLoopSource.PlayScheduled(startDspTime);

        Debug.Log(
            $"TutorialManager: 4박 메트로놈 루프 시작 / " +
            $"bpm:{tutorialBpm}, intervalBeats:{guideMetronomeIntervalBeats}, " +
            $"loopLength:{generatedGuideMetronomeLoopClip.length:0.000}"
        );
    }

    /// <summary>
    /// 튜토리얼 검수용 메트로놈 루프를 중지한다.
    /// </summary>
    private void StopGuideMetronome()
    {
        if (guideMetronomeLoopSource != null)
        {
            guideMetronomeLoopSource.Stop();
            guideMetronomeLoopSource.loop = false;
            guideMetronomeLoopSource.clip = null;
        }

        if (generatedGuideMetronomeLoopClip != null)
        {
            Destroy(generatedGuideMetronomeLoopClip);
            generatedGuideMetronomeLoopClip = null;
        }
    }

    /// <summary>
    /// 지정한 DSP 시간이 될 때까지 기다린다.
    /// 튜토리얼 첫 대사와 첫 메트로놈을 맞추는 데 사용한다.
    /// </summary>
    private IEnumerator WaitUntilDspTime(double targetDspTime)
    {
        while (AudioSettings.dspTime < targetDspTime)
        {
            yield return null;
        }
    }

    /// <summary>
    /// 원본 메트로놈 효과음 뒤에 무음을 붙여,
    /// guideMetronomeIntervalBeats 박자 길이의 루프 클립을 만든다.
    /// </summary>
    private AudioClip CreateGuideMetronomeLoopClip()
    {
        double beatSeconds = 60.0 / Mathf.Max(1f, tutorialBpm);
        double intervalSeconds = beatSeconds * Mathf.Max(1, guideMetronomeIntervalBeats);

        int channels = guideMetronomeClip.channels;
        int frequency = guideMetronomeClip.frequency;
        int intervalSamples = Mathf.CeilToInt((float)(intervalSeconds * frequency));

        if (intervalSamples <= 0)
        {
            Debug.LogError("TutorialManager: 메트로놈 루프 길이가 올바르지 않음.");
            return null;
        }

        int sourceSamples = guideMetronomeClip.samples;
        int copySamples = Mathf.Min(sourceSamples, intervalSamples);

        float[] sourceData = new float[sourceSamples * channels];
        float[] loopData = new float[intervalSamples * channels];

        bool canRead = guideMetronomeClip.GetData(sourceData, 0);

        if (!canRead)
        {
            Debug.LogError(
                "TutorialManager: guideMetronomeClip 데이터를 읽을 수 없음. " +
                "메트로놈 클립 Import Settings에서 Load Type을 Decompress On Load로 바꿔야 함."
            );
            return null;
        }

        Array.Copy(
            sourceData,
            0,
            loopData,
            0,
            copySamples * channels
        );

        AudioClip loopClip = AudioClip.Create(
            "Tutorial_GuideMetronome_4BeatLoop",
            intervalSamples,
            channels,
            frequency,
            false
        );

        loopClip.SetData(loopData, 0);

        return loopClip;
    }

    /// <summary>
    /// 튜토리얼 검수용 메트로놈 간격을 초 단위로 반환한다.
    /// 기본값은 90BPM 기준 4박 = 약 2.667초.
    /// </summary>
    private double GetGuideMetronomeIntervalSeconds()
    {
        double beatSeconds = 60.0 / Mathf.Max(1f, tutorialBpm);
        return beatSeconds * Mathf.Max(1, guideMetronomeIntervalBeats);
    }

    /// <summary>
    /// 현재 시각 기준 사용할 4박 경계선을 반환한다.
    /// 방금 경계선을 살짝 지난 경우에는 다음 경계선까지 기다리지 않고,
    /// 방금 지난 경계선을 그대로 사용해 긴 공백을 방지한다.
    /// </summary>
    private double GetCurrentOrNextGuideBoundaryDspTime(double fromDspTime)
    {
        double intervalSeconds = GetGuideMetronomeIntervalSeconds();

        if (intervalSeconds <= 0.0)
        {
            return fromDspTime;
        }

        double elapsed = fromDspTime - tutorialStartDspTime;

        if (elapsed <= 0.0)
        {
            return tutorialStartDspTime;
        }

        double currentIndex = Math.Floor(elapsed / intervalSeconds);
        double currentBoundary = tutorialStartDspTime + currentIndex * intervalSeconds;
        double nextBoundary = currentBoundary + intervalSeconds;

        // 코루틴 프레임 지연 때문에 경계선을 살짝 지난 경우는 같은 박자로 인정.
        if (fromDspTime - currentBoundary <= guideBoundaryLateGraceSeconds)
        {
            return currentBoundary;
        }

        return nextBoundary;
    }
    
    /// <summary>
    /// 공격 연습에서 만든 비트를 상대에게 넘기는 연출과
    /// 방어턴 설명 대사를 동시에 진행한다.
    /// </summary>
    private IEnumerator PlayDefenseDialogueWithAttackTransfer()
    {
        if (lastAttackNotes.Count == 0)
        {
            yield return PlayDialogue(defenseGuideLines);
            yield break;
        }

        double transitionStartDspTime =
            GetCurrentOrNextGuideBoundaryDspTime(AudioSettings.dspTime);

        yield return WaitUntilDspTime(transitionStartDspTime);

        Coroutine transferCoroutine = StartCoroutine(
            RunDefenseTurnForExistingNotes(
                attackerSide: playerSide,
                isAiDefense: true,
                inputStep: TutorialStep.None,
                useViewTransitionDelay: false
            )
        );

        yield return PlayDialogue(defenseGuideLines, transitionStartDspTime);

        if (transferCoroutine != null)
        {
            yield return transferCoroutine;
        }

        attackTurnRenderer.ClearAll();
        hud?.ClearAttackProgress();
        hud?.ClearJudgments();
        hud?.ClearPanelMessages();
    }

    /// <summary>
    /// 연습 단계 시작 안내 문구를 짧게 표시한다.
    /// 4박자 설명 대사와 별개로, 턴 진행 시간을 추가로 차지하지 않는다.
    /// </summary>
    private void ShowPracticeStartMessage(string message)
    {
        if (dialoguePlayer == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        dialoguePlayer.ShowReactionForBeats(
            message,
            practiceStartMessageBeats,
            hideWhenFinished: true
        );
    }

    /// <summary>
    /// 공격턴 설명 문장 시작 시 호출된다.
    /// 공격턴 설명 4번째 문장에서 고주파, 5번째 문장에서 저주파 노트를 표시한다.
    /// </summary>
    private void HandleAttackGuideLineStarted(int lineIndex, string lineText)
    {
        if (!playAttackBeatDemo)
        {
            return;
        }

        int highTargetIndex = Mathf.Max(1, attackHighBeatDemoLineNumber) - 1;
        int lowTargetIndex = Mathf.Max(1, attackLowBeatDemoLineNumber) - 1;

        if (lineIndex == highTargetIndex && !attackHighBeatDemoStarted)
        {
            attackHighBeatDemoStarted = true;

            Debug.Log(
                $"TutorialManager: Attack high beat demo 시작 / " +
                $"attackLine:{lineIndex + 1}, text:{lineText}"
            );

            SpawnBeatDemoNote(
                NoteType.HIGH,
                demoHighNotePositionRatio
            );

            return;
        }

        if (lineIndex == lowTargetIndex && !attackLowBeatDemoStarted)
        {
            attackLowBeatDemoStarted = true;

            Debug.Log(
                $"TutorialManager: Attack low beat demo 시작 / " +
                $"attackLine:{lineIndex + 1}, text:{lineText}"
            );

            SpawnBeatDemoNote(
                NoteType.LOW,
                demoLowNotePositionRatio
            );
        }
    }

    /// <summary>
    /// 튜토리얼 타격음 전용 AudioSource를 준비한다.
    /// SoundManager 풀/믹서/동시재생 제한 문제를 우회하기 위한 테스트용 구조다.
    /// </summary>
    private void PrepareDirectHitSources()
    {
        if (!useDirectTutorialHitSfx)
        {
            return;
        }

        int count = Mathf.Max(1, directHitSourceCount);

        if (directHitSources != null && directHitSources.Length == count)
        {
            return;
        }

        directHitSources = new AudioSource[count];

        for (int i = 0; i < count; i++)
        {
            GameObject sourceObject = new GameObject($"TutorialDirectHitSource_{i}");
            sourceObject.transform.SetParent(transform);

            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.volume = 1f;
            source.pitch = 1f;
            source.panStereo = 0f;
            source.spatialBlend = 0f;
            source.priority = 0;
            source.bypassEffects = true;
            source.bypassListenerEffects = true;
            source.bypassReverbZones = true;
            source.outputAudioMixerGroup = null;

            directHitSources[i] = source;
        }

        nextDirectHitSourceIndex = 0;
    }

    /// <summary>
    /// 튜토리얼 타격음을 SoundManager를 거치지 않고 직접 재생한다.
    /// </summary>
    private void PlayDirectTutorialHitSfx(NoteType noteType)
    {
        if (!useDirectTutorialHitSfx)
        {
            PlayNoteHitSfxThroughSoundManager(noteType);
            return;
        }

        PrepareDirectHitSources();

        AudioClip clip = noteType == NoteType.HIGH
            ? directHitHighClip
            : directHitLowClip;

        if (clip == null)
        {
            Debug.LogWarning($"TutorialManager: 직접 재생용 타격음 클립이 비어 있음. note:{noteType}");
            return;
        }

        if (directHitSources == null || directHitSources.Length == 0)
        {
            Debug.LogWarning("TutorialManager: 직접 재생용 AudioSource가 없음.");
            return;
        }

        AudioSource source = directHitSources[nextDirectHitSourceIndex];
        nextDirectHitSourceIndex = (nextDirectHitSourceIndex + 1) % directHitSources.Length;

        source.Stop();
        source.volume = 1f;
        source.pitch = 1f;
        source.panStereo = 0f;
        source.spatialBlend = 0f;
        source.priority = 0;
        source.outputAudioMixerGroup = null;

        source.PlayOneShot(clip, directHitVolume);

    }

    private void PlayTutorialHitSfx(NoteType noteType)
    {
        if (!playHitSfx)
        {
            return;
        }

        if (useDirectTutorialHitSfx)
        {
            PlayDirectTutorialHitSfx(noteType);
            return;
        }

        PlayNoteHitSfxThroughSoundManager(noteType);
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