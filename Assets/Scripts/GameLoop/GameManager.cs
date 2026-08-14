using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 게임 상태 머신 + 컴포넌트 간 이벤트 중계자 역할.
/// DSP 페이즈 루프로 턴 흐름을 제어하고, 입력 라우팅, 이벤트 중계, 카메라 뷰 전환 등 게임 루프 전반을 관리.
/// </summary>

[System.Serializable]
public class RoundSetting
{
    [Header("Round")]
    public string roundName = "R1";

    [Header("BPM")]
    public float bpm = 106f;

    [Header("Target Note Count")]
    public int minTargetNoteCount = 2;
    public int maxTargetNoteCount = 4;

    [Header("Sound")]
    public SfxId roundUpSfx = SfxId.RoundStart1;
}
public class GameManager : MonoBehaviour
{
    [Header("Turn Components")]
    [SerializeField] private AttackTurn attackTurn;
    [SerializeField] private DefenseTurn defenseTurn;
    [SerializeField] private AttackTurnRenderer attackTurnRenderer;
    [Header("Generators")]
    [SerializeField] private NoteCountGenerator noteCountGenerator;
    [SerializeField] private RandomMessageProvider randomMessageProvider;

    [Header("HUD")]
    [SerializeField] private HUD hud;

    [Header("Camera")]
    [SerializeField] private GameCamera gameCamera;

    [Header("Sanity System")]
    [SerializeField] private SanitySystem sanitySystem;

    [Header("Result UI")]
    [SerializeField] private ResultPanelUI resultPanelUI;

    [Header("Round Progression")]
    [SerializeField] private int attackPhasesPerRound = 2;

    [SerializeField] private RoundSetting[] roundSettings =
    {
        new RoundSetting
        {
            roundName = "R1",
            bpm = 106f,
            minTargetNoteCount = 2,
            maxTargetNoteCount = 4,
            roundUpSfx = SfxId.RoundStart1
        },
        new RoundSetting
        {
            roundName = "R2",
            bpm = 120f,
            minTargetNoteCount = 2,
            maxTargetNoteCount = 6,
            roundUpSfx = SfxId.RoundStart2
        },
        new RoundSetting
        {
            roundName = "R3",
            bpm = 144f,
            minTargetNoteCount = 2,
            maxTargetNoteCount = 7,
            roundUpSfx = SfxId.RoundStart3
        }
    };

    [Header("Game Start Delay")]
    [SerializeField] private bool useMillisecondStartDelay = true;
    [SerializeField] private int gameStartDelayMs = 1000;

    [SerializeField] private bool useBeatStartDelay = true;
    [SerializeField] private int gameStartDelayBeats = 4;
    
    [Header("Sound")]
    [SerializeField] private bool playCoreLoopBgm = true;
    [SerializeField] private double bpmChangeBgmLeadTime = 0.1;

    [Header("Communication Success")]
    [SerializeField] private bool onlyHostSendsCommunicationSuccessPacket = true;

    // DSP 페이즈 루프
    private int    phaseIndex;
    private double nextPhaseDspTime  = double.MaxValue;  // StartGame() 호출 전 루프 실행 방지
    private double currentTurnDuration;
    private double localAttackStartDspTime;

    // 네트워크 노트 수신 추적
    private int currentTargetNoteCount;
    private int currentReceivedNoteCount;

    // noteId로 HIGH/LOW 타입을 다시 찾기 위한 캐시.
    // 상대 방어 JUDGMENT 패킷은 noteId만 들고 오므로, HitHigh/HitLow 재생에 필요하다.
    private readonly Dictionary<int, NoteType> noteTypeByNoteId = new();

    // 내가 공격자일 때 상대 방어 결과를 JUDGMENT 패킷으로 추적한다.
    // 최신 네트워크 구조에는 DEFENSE_END 패킷이 없으므로, 다음 공격 phase 진입 시 이 값으로 상대 방어 성공/실패음을 재생한다.
    private int remoteDefenseMissCount;
    private bool isWaitingRemoteDefenseResult;

    // 공격자 미러뷰에서 수신한 JUDGMENT 패킷을 noteId 순으로 보관한다.
    // 모든 판정이 도착하면 GetDefenderPanelMessage에 전달해 방어자 패널 메세지를 구성한다.
    private readonly List<Judgment> remoteJudgmentBuffer = new();

    // 로컬 모드 방어 페이즈에서 Begin()에 전달할 공격 결과
    private AttackResult? lastLocalAttackResult;

    // 패널 메세지 표시에 필요한 턴 상태 캐시
    private string currentAttackMessage = "";
    private bool currentAttackSuccess;
    private int currentJudgmentIndex;
    private int currentPhaseNoteCount; // 네트워크 공격자 클라이언트에서 방어 종료 판단에 사용

    //private int completedTurnCount;
    private int currentRoundIndex;

    private GameState currentState;
    private int attackerPlayerId = 1;
    private int myLocalPlayerId = 1; // NetworkManager.Instance에서 Awake() 시 읽어옴

    // 상대 패킷을 반영해서 정신력이 깎이는 중인지 표시.
    // true일 때 발생한 패배는 이미 상대 쪽 흐름에서 온 것이므로 GAME_END를 다시 보내지 않는다.
    private bool isApplyingRemoteGameState;

    /// <summary>
    /// 필수 참조를 확인하고 각 시스템의 이벤트를 구독한다.
    /// </summary>
    private void Awake()
    {
        if (attackTurn == null || defenseTurn == null || attackTurnRenderer == null ||
            sanitySystem == null || noteCountGenerator == null || randomMessageProvider == null)
        {
            Debug.LogError("GameManager: 필수 컴포넌트가 연결되지 않았습니다.");
            return;
        }

        // 정신력 이벤트를 HUD 갱신으로 연결
        sanitySystem.OnSanityChanged += HandleSanityChanged;
        sanitySystem.OnPlayerDefeated += HandlePlayerDefeated;

        // 공격/방어 턴 이벤트를 받아 다음 상태로 전환
        attackTurn.OnAttackEnded += HandleAttackEnded;
        attackTurn.OnAttackMessageSelected += HandleAttackMessageSelected;
        attackTurn.OnAttackProgressChanged += HandleAttackProgressChanged;
        attackTurn.OnAttackInputResolved += HandleHitResolved;

        defenseTurn.OnDefenseEnded += HandleDefenseEnded;
        defenseTurn.OnJudgment += HandleJudgment;
        defenseTurn.OnDefenseInputResolved += HandleHitResolved;

        var net = NetworkManager.Instance;
        if (net != null)
        {
            myLocalPlayerId  = net.LocalPlayerId;
            attackerPlayerId = net.FirstAttackerId;
            attackTurnRenderer?.SetLocalPlayer(myLocalPlayerId);

            net.OnNoteCreated        += HandleNetworkNoteCreated;
            net.OnJudgmentReceived   += HandleNetworkJudgment;
            net.OnSanityChange       += HandleNetworkSanityChange;
            net.OnGameEnd            += HandleNetworkGameEnd;
            net.OnDisconnected       += HandleNetworkDisconnected;
        }

        if (hud != null)
        {
            hud.SetupPlayerPerspective(myLocalPlayerId);
        }
    }

    /// <summary>
    /// 오브젝트 제거 시 구독한 이벤트 해제
    /// </summary>
    private void OnDestroy()
    {
        if (attackTurn != null)
        {
            attackTurn.OnAttackEnded -= HandleAttackEnded;
            attackTurn.OnAttackMessageSelected -= HandleAttackMessageSelected;
            attackTurn.OnAttackProgressChanged -= HandleAttackProgressChanged;
            attackTurn.OnAttackInputResolved -= HandleHitResolved;
        }

        if (defenseTurn != null)
        {
            defenseTurn.OnDefenseEnded -= HandleDefenseEnded;
            defenseTurn.OnJudgment -= HandleJudgment;
            defenseTurn.OnDefenseInputResolved -= HandleHitResolved;
        }

        if (sanitySystem != null)
        {
            sanitySystem.OnSanityChanged -= HandleSanityChanged;
            sanitySystem.OnPlayerDefeated -= HandlePlayerDefeated;
        }

        var net = NetworkManager.Instance;
        if (net != null)
        {
            net.OnNoteCreated        -= HandleNetworkNoteCreated;
            net.OnJudgmentReceived   -= HandleNetworkJudgment;
            net.OnSanityChange       -= HandleNetworkSanityChange;
            net.OnGameEnd            -= HandleNetworkGameEnd;
            net.OnDisconnected       -= HandleNetworkDisconnected;
        }
    }

    /// <summary>
    /// 네트워크 모드에서 씬 로드 직후 자동으로 게임을 시작한다.
    /// </summary>
    private void Start()
    {
        resultPanelUI?.HideAll();

        //completedTurnCount = 0;
        currentRoundIndex = 0;
        attackerPlayerId = NetworkManager.Instance?.FirstAttackerId ?? 1;

        if (NetworkManager.Instance != null)
            StartGame();
    }

    private void Update()
    {
        if (currentState == GameState.END) return;

        double now = AudioSettings.dspTime;
        if (now >= nextPhaseDspTime)
            AdvancePhase();
    }

    /// <summary>
    /// 게임을 초기화하고 DSP 페이즈 루프를 시작한다.
    /// 정신력, BPM 단계, 리듬 클락을 초기화한 뒤 첫 공격 phase를 예약한다.
    /// </summary>
    public void StartGame()
    {
        resultPanelUI?.HideAll();
        sanitySystem?.ResetSanity();
        hud?.ClearAttackProgress();
        hud?.ClearJudgments();

        phaseIndex = 0;
        currentRoundIndex = 0;

        ApplyCurrentBpm(scheduleBgm: false);
        currentTurnDuration = GetCurrentTurnDuration();

        var net = NetworkManager.Instance;
        double rhythmStart = net?.LocalGameStartDspTime ?? AudioSettings.dspTime;

        RhythmClock.Instance?.StartClock(rhythmStart);

        nextPhaseDspTime = rhythmStart + GetGameStartDelaySeconds();
        currentState     = GameState.TURN_CHANGE;
        attackerPlayerId = net?.FirstAttackerId ?? 1;

        // 첫 공격 phase 시작 시점에 현재 라운드 BGM을 맞춰 예약한다.
        ScheduleCurrentCoreLoopBgm(nextPhaseDspTime);

        // R1 시작 효과음이 필요하면 사용.
        // GameStart 사운드와 겹치면 이 줄은 빼도 된다.
        PlayRoundStartSfx();
    }

    /// <summary>
    /// 현재 게임 상태에 따라 noteType 입력을 적절한 턴 컴포넌트로 전달.
    /// </summary>
    public void OnTap(NoteType noteType)
    {
        if (currentState == GameState.ATTACK)
        {
            attackTurn.OnTap(noteType);
        }
        else if (currentState == GameState.DEFENSE)
        {
            bool isMirrorView = NetworkManager.Instance != null && attackerPlayerId == myLocalPlayerId;
            if (!isMirrorView)
                defenseTurn.OnTap(noteType);
        }
    }

    /// <summary>
    /// HIGH 노트 키 입력. HUD 또는 Input 스크립트에서 호출.
    /// </summary>
    public void OnTapHigh() => OnTap(NoteType.HIGH);

    /// <summary>
    /// LOW 노트 키 입력. HUD 또는 Input 스크립트에서 호출.
    /// </summary>
    public void OnTapLow() => OnTap(NoteType.LOW);

    // ── DSP 페이즈 루프 ──────────────────────────────────────────────────────────

    private void AdvancePhase()
    {
        double thisPhaseStart = nextPhaseDspTime;
        bool   isAttackPhase  = (phaseIndex % 2 == 0);
        int    attackPhaseIdx = phaseIndex / 2;

        // 새 공격 phase로 넘어가기 직전, 직전 방어 phase 결과를 먼저 정리한다.
        // 예정된 모든 공격 턴과 그에 대한 방어 phase가 끝났다면 다음 공격을 시작하지 않고 교신 성공으로 종료한다.
        if (isAttackPhase)
        {
            PlayPendingRemoteDefenseResultSfx();

            if (ShouldEndByCommunicationSuccess(attackPhaseIdx))
            {
                EndGameByCommunicationSuccess(ShouldSendCommunicationSuccessPacket());
                return;
            }
        }

        if (isAttackPhase)
        {
            int nextRoundIndex = GetRoundIndexByAttackPhase(attackPhaseIdx);

            if (nextRoundIndex != currentRoundIndex)
            {
                currentRoundIndex = nextRoundIndex;

                ApplyCurrentBpm();
                PlayRoundStartSfx();
            }

            currentTurnDuration     = GetCurrentTurnDuration();
            localAttackStartDspTime = thisPhaseStart;
            attackerPlayerId        = GetAttackerForPhase(attackPhaseIdx);
        }

        nextPhaseDspTime = thisPhaseStart + currentTurnDuration;

        if (isAttackPhase)
            StartAttackPhase(thisPhaseStart, attackPhaseIdx);
        else
            StartDefensePhase(thisPhaseStart);

        phaseIndex++;
    }

    private int GetAttackerForPhase(int attackPhaseIdx)
    {
        int first = NetworkManager.Instance?.FirstAttackerId ?? 1;
        int other = (first == 1) ? 2 : 1;
        return (attackPhaseIdx % 2 == 0) ? first : other;
    }

    private double GetCurrentTurnDuration()
    {
        if (RhythmClock.Instance == null) return 2.0;
        return RhythmClock.Instance.GetNoteDuration() * 8.0;
    }

    private System.Random GetSharedRng(int attackPhaseIdx)
    {
        int baseSeed = NetworkManager.Instance?.SharedSeed ?? 0;
        int seed = unchecked(baseSeed + attackPhaseIdx * 997);
        return new System.Random(seed);
    }

    // ── Attack Phase ─────────────────────────────────────────────────────────────

    private void StartAttackPhase(double phaseStartDspTime, int attackPhaseIdx)
    {
        noteTypeByNoteId.Clear();
        
        currentState = GameState.ATTACK;
        AttackSide attackerSide = GetAttackSide(attackerPlayerId);

        attackTurnRenderer?.ClearAll();
        hud?.ClearJudgments();
        hud?.ClearPanelMessages();
        gameCamera?.SetAttackView(attackerSide);

        var rng = GetSharedRng(attackPhaseIdx);
        RoundSetting setting = GetRoundSettingByAttackPhase(attackPhaseIdx);

        int noteCount = setting == null
            ? noteCountGenerator.CreateRandomNoteCount(rng)
            : noteCountGenerator.CreateRandomNoteCount(
                rng,
                setting.minTargetNoteCount,
                setting.maxTargetNoteCount);

        string msg = randomMessageProvider.GetRandomMessage(noteCount, rng);

        bool isLocalAttacker = (attackerPlayerId == myLocalPlayerId ||
                                NetworkManager.Instance == null);

        if (isLocalAttacker)
        {
            attackTurn.StartLocalPlayerAttack(attackerSide, noteCount, msg, phaseStartDspTime);
            hud?.UpdateAttackProgress(0, noteCount);
        }
        else if (NetworkManager.Instance == null)
        {
            attackTurn.StartOpponentAttackDemo(msg);
            hud?.UpdateAttackProgress(0, noteCount);
        }
        else
        {
            currentTargetNoteCount   = noteCount;
            currentReceivedNoteCount = 0;
            hud?.UpdateAttackProgress(0, noteCount);

            defenseTurn?.PrepareForIncomingAttack(attackerSide, currentTurnDuration);
            attackTurnRenderer?.BeginAttackVisual(attackerSide, phaseStartDspTime,
                currentTurnDuration, noteCount);
        }
    }

    // ── Defense Phase ─────────────────────────────────────────────────────────────

    private void StartDefensePhase(double phaseStartDspTime)
    {
        currentJudgmentIndex = 0;

        AttackSide attackerSide = GetAttackSide(attackerPlayerId);
        gameCamera?.SetDefenseView(attackerSide);
        attackTurnRenderer?.StopLine();

        float judgeLineX   = attackTurnRenderer?.GetJudgeLineX(attackerSide) ?? 0f;
        float attackStartX = attackTurnRenderer?.GetStartX(attackerSide) ?? 5f;
        float attackEndX   = attackTurnRenderer?.GetEndX(attackerSide) ?? -5f;

        bool isLocalAttacker = (attackerPlayerId == myLocalPlayerId);
        bool isNetworkMode   = NetworkManager.Instance != null;

        if (!isLocalAttacker && isNetworkMode)
        {
            currentState = GameState.DEFENSE;

            bool remoteAttackSuccess = currentReceivedNoteCount == currentTargetNoteCount;
            currentAttackSuccess = remoteAttackSuccess;
            PlayTurnResultSfx(remoteAttackSuccess);

            remoteJudgmentBuffer.Clear();

            defenseTurn?.BeginTransfer(judgeLineX, attackStartX, attackEndX,
                currentTurnDuration, NetworkManager.Instance, phaseStartDspTime);
        }
        else if (!isNetworkMode)
        {
            currentState = GameState.DEFENSE;
            BeginLocalDefense();  // lastLocalAttackResult이 이미 세팅돼 있으면 즉시 시작, 아니면 HandleAttackEnded 쪽이 처리
        }
        // isLocalAttacker && isNetworkMode: 공격자 미러뷰는 HandleAttackEnded에서 처리
    }

    // AttackTurn.Update()와 GameManager.Update() 실행 순서에 무관하게 defenseTurn.Begin()을 정확히 한 번 호출한다.
    private void BeginLocalDefense()
    {
        if (!lastLocalAttackResult.HasValue) return;

        AttackSide attackerSide = GetAttackSide(attackerPlayerId);
        float judgeLineX   = attackTurnRenderer?.GetJudgeLineX(attackerSide) ?? 0f;
        float attackStartX = attackTurnRenderer?.GetStartX(attackerSide) ?? 5f;
        float attackEndX   = attackTurnRenderer?.GetEndX(attackerSide) ?? -5f;

        defenseTurn?.Begin(
            lastLocalAttackResult.Value.Notes,
            judgeLineX, attackStartX, attackEndX,
            currentTurnDuration,
            isAiDefense: false,
            networkManager: null,
            remoteAttackStartDspTime: localAttackStartDspTime  // 로컬 모드: DSP 페이즈 루프 기준 정확한 이론 시작 시각
        );
        lastLocalAttackResult = null;
    }

    // ── Attack/Defense Event Handlers ─────────────────────────────────────────────

    /// <summary>
    /// 공격 턴 종료 시 공격자 패널티를 적용하고 네트워크 모드에서 미러뷰를 시작한다.
    /// 공격자의 정신력이 0이 되는 경우 게임 종료 처리는 SanitySystem.OnPlayerDefeated 이벤트가 담당한다.
    /// </summary>
    private void HandleAttackEnded(AttackResult attackResult)
    {
        if (currentState == GameState.END) return;

        RegisterNoteTypes(attackResult.Notes);

        bool attackSuccess = IsAttackTurnSuccess(attackResult);
        currentAttackSuccess = attackSuccess;
        PlayTurnResultSfx(attackSuccess);

        currentPhaseNoteCount = attackResult.Notes.Count;

        AttackSide attackerSide = GetAttackSide(attackerPlayerId);
        sanitySystem?.ApplyAttackResult(attackerPlayerId, attackResult);
        lastLocalAttackResult = attackResult;

        // DSP 루프가 먼저 defense phase를 시작했고 이쪽이 늦게 실행된 경우 여기서 Begin() 호출
        if (NetworkManager.Instance == null && currentState == GameState.DEFENSE)
            BeginLocalDefense();

        if (NetworkManager.Instance != null)
        {
            int penalty = sanitySystem?.CalculateAttackPenalty(attackResult) ?? 0;
            if (penalty > 0)
            {
                byte targetId = (byte)attackerPlayerId;
                NetworkManager.Instance.Send(w =>
                    PacketSerializer.WriteSanityChange(w, targetId, penalty));
            }
        }

        if (currentState == GameState.END) return;

        // 공격자 미러 뷰 (로컬 공격자가 방어 phase에서 반대편 뷰로)
        if (NetworkManager.Instance != null && attackerPlayerId == myLocalPlayerId)
        {
            remoteDefenseMissCount = 0;
            isWaitingRemoteDefenseResult = true;

            float judgeLineX   = attackTurnRenderer?.GetJudgeLineX(attackerSide) ?? 0f;
            float attackStartX = attackTurnRenderer?.GetStartX(attackerSide) ?? 5f;
            float attackEndX   = attackTurnRenderer?.GetEndX(attackerSide) ?? -5f;

            defenseTurn?.Begin(
                attackResult.Notes,
                judgeLineX,
                attackStartX,
                attackEndX,
                attackTurn.AttackDuration,
                isAiDefense: true,
                networkManager: null,
                remoteAttackStartDspTime: attackTurn.AttackStartDspTime
            );
        }
    }

    /// <summary>
    /// 실제 방어자의 노트 판정 결과를 처리한다.
    /// MISS일 경우 방어자의 정신력을 감소시키고 SANITY_CHANGE를 전송한다.
    /// </summary>
    private void HandleJudgment(Judgment judgment)
    {
        if (currentState == GameState.END) return;

        // 네트워크 모드에서 내가 공격자라면,
        // 방어 판정 표시는 상대가 보낸 JUDGMENT 패킷을 받은 HandleNetworkJudgment가 담당한다.
        if (NetworkManager.Instance != null && attackerPlayerId == myLocalPlayerId) return;

        AttackSide attackerSide = GetAttackSide(attackerPlayerId);
        hud?.ShowJudgment(judgment, attackerSide);

        if (judgment != Judgment.MISS)
            hud?.SetStarSuccess(currentJudgmentIndex);
        currentJudgmentIndex++;

        if (judgment != Judgment.MISS) return;

        int defenderPlayerId = GetDefenderPlayerId();
        int penalty = sanitySystem?.ApplyDefenseMiss(defenderPlayerId) ?? 0;

        if (NetworkManager.Instance != null && penalty > 0)
        {
            byte targetId = (byte)defenderPlayerId;
            NetworkManager.Instance.Send(w =>
                PacketSerializer.WriteSanityChange(w, targetId, penalty));
        }
    }

    /// <summary>
    /// 방어 턴 종료 이벤트를 처리한다.
    /// DSP 페이즈 루프가 다음 phase를 자동으로 시작하므로 턴 전환은 하지 않는다.
    /// 실제 방어자인 경우 방어 성공/실패 효과음만 재생한다.
    /// </summary>
    private void HandleDefenseEnded(DefenseResult result)
    {
        if (currentState == GameState.END) return;

        // 네트워크 모드에서 내가 공격자라면 이 DefenseTurn은 미러뷰/AI 처리이므로
        // 실제 상대 방어 결과음은 JUDGMENT 누적값으로 따로 재생한다.
        if (NetworkManager.Instance != null && attackerPlayerId == myLocalPlayerId)
        {
            Debug.Log($"Defense Mirror End / miss:{result.MissCount}");
            return;
        }

        bool defenseSuccess = IsDefenseTurnSuccess(result);
        PlayTurnResultSfx(defenseSuccess);

        AttackSide attackerSide = GetAttackSide(attackerPlayerId);
        string defensePanelMsg = randomMessageProvider?.GetDefenderPanelMessage(currentAttackMessage, result.Judgments, currentAttackSuccess) ?? "";
        hud?.ShowInDefenderPanel(defensePanelMsg, attackerSide);

        Debug.Log($"Defense End / miss:{result.MissCount}");
    }

    // ── Network Event Handlers ────────────────────────────────────────────────────

    /// <summary>
    /// 원격 공격자가 생성한 노트 패킷을 처리한다.
    /// 노트 타입을 캐시에 저장하고, 상대 공격 성공 입력음으로 HitHigh/HitLow를 재생한다.
    /// </summary>
    private void HandleNetworkNoteCreated(NoteCreatedPacket packet)
    {
        defenseTurn?.OnNoteReceived(packet, localAttackStartDspTime + currentTurnDuration);

        RegisterNoteType(packet.noteId, packet.noteType);

        currentReceivedNoteCount++;

        if (currentTargetNoteCount > 0)
        {
            hud?.UpdateAttackProgress(currentReceivedNoteCount, currentTargetNoteCount);
        }

        PlayHitResultSfx(packet.noteType, true);
    }

    /// <summary>
    /// 상대 클라이언트에서 전송한 SANITY_CHANGE 패킷을 처리한다.
    /// </summary>
    private void HandleNetworkSanityChange(SanityChangePacket packet)
    {
        if (currentState == GameState.END) return;
        isApplyingRemoteGameState = true;
        try
        {
            sanitySystem?.ApplyDirect(packet.targetPlayerId, packet.amount);
        }
        finally
        {
            isApplyingRemoteGameState = false;
        }
    }

    /// <summary>
    /// 상대 클라이언트에서 받은 판정 패킷을 처리한다.
    /// JUDGMENT는 시각 동기화 전용 — 정신력 처리 없음 (SANITY_CHANGE가 별도 처리).
    /// </summary>
    private void HandleNetworkJudgment(JudgmentPacket packet)
    {
        if (currentState == GameState.END) return;

        Judgment judgment = (Judgment)packet.judgment;
        AttackSide attackerSide = GetAttackSide(attackerPlayerId);

        PlayHitResultSfxByNoteId(packet.noteId, judgment);

        // noteId 순서대로 버퍼 확장 후 판정 기록
        while (remoteJudgmentBuffer.Count <= packet.noteId)
            remoteJudgmentBuffer.Add(Judgment.MISS);
        remoteJudgmentBuffer[packet.noteId] = judgment;

        if (judgment == Judgment.MISS)
        {
            remoteDefenseMissCount++;
        }
        else
        {
            hud?.SetStarSuccess(currentJudgmentIndex);
        }
        currentJudgmentIndex++;

        // 모든 판정이 도착했을 때 공격자 클라이언트의 방어자 패널(상대 패널)에 결과 메세지 표시
        if (currentPhaseNoteCount > 0 && currentJudgmentIndex >= currentPhaseNoteCount)
        {
            Judgment[] judgments = remoteJudgmentBuffer.ToArray();
            string observeMsg = randomMessageProvider?.GetDefenderPanelMessage(currentAttackMessage, judgments, currentAttackSuccess) ?? "";
            hud?.ShowInDefenderPanel(observeMsg, attackerSide);
        }

        // JUDGMENT는 시각 동기화 전용 — 정신력 처리 없음 (SANITY_CHANGE가 별도 처리)
        attackTurnRenderer?.RemoveNote(packet.noteId);  // 공격자 미러뷰에서 노트 제거
        hud?.ShowJudgment(judgment, attackerSide);
    }

    /// <summary>
    /// 특정 플레이어의 정신력이 0이 되어 게임이 종료되었을 때 호출된다.
    /// 내 화면에는 localPlayerId 기준으로 승리/패배 패널을 표시하고,
    /// 내가 직접 감지한 종료라면 상대에게 GAME_END 패킷을 전송한다.
    /// </summary>
    private void HandlePlayerDefeated(int defeatedPlayerId)
    {
        bool shouldSendNetworkPacket =
            NetworkManager.Instance != null &&
            !isApplyingRemoteGameState;

        EndGameByDefeatedPlayer(defeatedPlayerId, shouldSendNetworkPacket);
    }

    /// <summary>
    /// 상대 클라이언트에서 전송한 GAME_END 패킷을 처리한다.
    /// 패킷 내용은 공통이지만, 결과 패널은 각자의 localPlayerId 기준으로 다르게 표시된다.
    /// </summary>
    private void HandleNetworkGameEnd(GameEndPacket packet)
    {
        if (currentState == GameState.END) return;

        switch (packet.reason)
        {
            case GameEndReason.PlayerDefeated:
                EndGameByDefeatedPlayer(packet.defeatedPlayerId, shouldSendNetworkPacket: false);
                break;

            case GameEndReason.CommunicationSuccess:
                EndGameByCommunicationSuccess(shouldSendNetworkPacket: false);
                break;
        }
    }

    /// <summary>
    /// 상대방 연결이 예기치 않게 끊어졌을 때 호출.
    /// </summary>
    private void HandleNetworkDisconnected()
    {
        if (currentState == GameState.END) return;
        currentState = GameState.END;
        Debug.Log("GameManager: 상대방 연결 끊김");
    }

    // ── AttackTurn 이벤트 포워딩 ──────────────────────────────────────────────────

    /// <summary>
    /// AttackTurn에서 선택된 공격 메시지를 HUD에 표시한다.
    /// </summary>
    private void HandleAttackMessageSelected(string message, int noteCount)
    {
        currentAttackMessage = message;
    }

    /// <summary>
    /// AttackTurn의 노트 생성 진행도를 HUD의 별 UI에 반영한다.
    /// </summary>
    private void HandleAttackProgressChanged(int actualCount, int targetCount)
    {
        if (hud == null) return;

        hud.UpdateAttackProgress(actualCount, targetCount);
    }

    // ── SanitySystem 이벤트 ───────────────────────────────────────────────────────

    /// <summary>
    /// SanitySystem의 정신력 변경 이벤트를 HUD에 반영한다.
    /// </summary>
    private void HandleSanityChanged(int p1Sanity, int p2Sanity, int maxSanity)
    {
        if (hud == null) return;
        hud.UpdateSanity(p1Sanity, p2Sanity, maxSanity);
    }

    // ── Game End ─────────────────────────────────────────────────────────────────

    private void EndGameByDefeatedPlayer(int defeatedPlayerId, bool shouldSendNetworkPacket)
    {
        if (currentState == GameState.END) return;

        currentState = GameState.END;

        attackTurnRenderer?.ClearAll();
        hud?.ClearAttackProgress();
        hud?.ClearJudgments();

        GameResultType resultType = defeatedPlayerId == myLocalPlayerId
            ? GameResultType.Lose
            : GameResultType.Win;

        resultPanelUI?.Show(resultType);

        // 승리/패배 종료에서는 코어루프 BGM을 멈춘다.
        // GameWin/GameLose는 효과음이므로 별개로 재생한다.
        SoundManager.Instance?.StopBgm();

        SoundManager.Instance?.PlaySfx(
            resultType == GameResultType.Win
                ? SfxId.GameWin
                : SfxId.GameLose
        );

        Debug.Log($"Game End / P{defeatedPlayerId} sanity depleted. Result: {resultType}");

        if (shouldSendNetworkPacket && NetworkManager.Instance != null)
        {
            NetworkManager.Instance.Send(writer =>
                PacketSerializer.WriteGameEnd(
                    writer,
                    GameEndReason.PlayerDefeated,
                    defeatedPlayerId
                )
            );
        }
    }

    /// <summary>
    /// 양쪽 플레이어가 마지막까지 생존하여 교신 성공 조건을 만족했을 때 호출한다.
    /// 현재는 조건이 확정되지 않았으므로 추후 BGM 종료 또는 목표 달성 조건에서 호출한다.
    /// </summary>
    private void EndGameByCommunicationSuccess(bool shouldSendNetworkPacket)
    {
        if (currentState == GameState.END) return;

        currentState = GameState.END;

        attackTurnRenderer?.ClearAll();
        hud?.ClearAttackProgress();
        hud?.ClearJudgments();

        resultPanelUI?.Show(GameResultType.CommunicationSuccess);

        SoundManager.Instance?.PlaySfx(SfxId.HappyEnding);

        Debug.Log("Game End / Communication Success.");

        if (shouldSendNetworkPacket && NetworkManager.Instance != null)
        {
            NetworkManager.Instance.Send(writer =>
                PacketSerializer.WriteGameEnd(
                    writer,
                    GameEndReason.CommunicationSuccess,
                    defeatedPlayerId: 0
                )
            );
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// 현재 공격자의 반대 플레이어를 방어자로 반환한다.
    /// P1이 공격 중이면 P2가 방어자, P2가 공격 중이면 P1이 방어자다.
    /// </summary>
    private int GetDefenderPlayerId()
    {
        return (attackerPlayerId == 1) ? 2 : 1;
    }

    /// <summary>
    /// 플레이어 ID를 HUD와 Renderer에서 사용하는 AttackSide 값으로 변환한다.
    /// </summary>
    private AttackSide GetAttackSide(int playerId)
    {
        return playerId == 1 ? AttackSide.P1 : AttackSide.P2;
    }

    /// <summary>
    /// 현재 라운드 설정에 맞는 BPM을 RhythmClock과 HUD에 적용한다.
    /// 필요하면 SoundManager에 현재 BPM의 코어 루프 BGM 전환도 요청한다.
    /// </summary>
    private void ApplyCurrentBpm(bool scheduleBgm = true)
    {
        RoundSetting setting = GetCurrentRoundSetting();

        if (setting == null)
        {
            Debug.LogWarning("GameManager: RoundSetting이 비어 있습니다.");
            return;
        }

        float bpm = Mathf.Max(1f, setting.bpm);

        RhythmClock.Instance?.SetBpm(bpm);
        hud?.UpdateBpm(bpm);

        if (scheduleBgm)
        {
            double scheduleTime = AudioSettings.dspTime + bpmChangeBgmLeadTime;
            ScheduleCurrentCoreLoopBgm(scheduleTime);
        }

        Debug.Log($"Round Applied / round:{setting.roundName}, index:{currentRoundIndex}, bpm:{bpm}");
    }

    /// <summary>
    /// インスペクター チェックボックス設定에 따라 첫 턴 시작 전 대기 시간을 초 단위로 계산한다.
    /// ms 체크박스와 beat 체크박스가 모두 켜져 있으면 두 값을 더해서 사용한다.
    /// 둘 다 꺼져 있으면 0초를 반환한다.
    /// </summary>
    private double GetGameStartDelaySeconds()
    {
        double delaySeconds = 0.0;

        if (useMillisecondStartDelay)
        {
            delaySeconds += Mathf.Max(0, gameStartDelayMs) / 1000.0;
        }

        if (useBeatStartDelay)
        {
            delaySeconds += GetCurrentBeatDurationSeconds() * Mathf.Max(0, gameStartDelayBeats);
        }

        return delaySeconds;
    }

    /// <summary>
    /// 현재 라운드 BPM 기준으로 1박자의 길이를 초 단위로 반환한다.
    /// 게임 시작 딜레이를 beat 단위로 계산할 때 사용한다.
    /// </summary>
    private double GetCurrentBeatDurationSeconds()
    {
        float bpm = GetCurrentBpm();
        return 60.0 / Mathf.Max(1f, bpm);
    }

    /// <summary>
    /// 현재 라운드 인덱스에 해당하는 RoundSetting을 반환한다.
    /// 라운드 설정이 비어 있으면 null을 반환한다.
    /// </summary>
    private RoundSetting GetCurrentRoundSetting()
    {
        if (roundSettings == null || roundSettings.Length == 0)
        {
            return null;
        }

        int safeIndex = Mathf.Clamp(currentRoundIndex, 0, roundSettings.Length - 1);
        return roundSettings[safeIndex];
    }

    /// <summary>
    /// 공격 phase 인덱스를 기준으로 현재 라운드 인덱스를 계산한다.
    /// attackPhasesPerRound가 2이면 두 번의 공격 phase마다 다음 라운드로 넘어간다.
    /// </summary>
    private int GetRoundIndexByAttackPhase(int attackPhaseIdx)
    {
        if (roundSettings == null || roundSettings.Length == 0)
        {
            return 0;
        }

        int safePhasesPerRound = Mathf.Max(1, attackPhasesPerRound);

        return Mathf.Clamp(
            attackPhaseIdx / safePhasesPerRound,
            0,
            roundSettings.Length - 1
        );
    }

    /// <summary>
    /// 현재 라운드 설정 기준으로 전체 예정 공격 턴 수를 계산한다.
    /// roundSettings.Length * attackPhasesPerRound 만큼의 공격 턴을 전체 게임 길이로 본다.
    /// </summary>
    private int GetTotalPlannedAttackTurnCount()
    {
        if (roundSettings == null || roundSettings.Length == 0)
        {
            return 0;
        }

        int safePhasesPerRound = Mathf.Max(1, attackPhasesPerRound);
        return roundSettings.Length * safePhasesPerRound;
    }

    /// <summary>
    /// 정해진 모든 공격 턴과 그에 대한 방어 phase가 끝났는지 확인한다.
    /// attackPhaseIdx는 지금 새로 시작하려는 공격 phase 번호이므로,
    /// 전체 예정 공격 턴 수 이상이면 이전 방어 phase까지 끝난 상태로 본다.
    /// </summary>
    private bool ShouldEndByCommunicationSuccess(int attackPhaseIdx)
    {
        int totalPlannedAttackTurnCount = GetTotalPlannedAttackTurnCount();

        if (totalPlannedAttackTurnCount <= 0)
        {
            return false;
        }

        return attackPhaseIdx >= totalPlannedAttackTurnCount;
    }

    /// <summary>
    /// 교신 성공 GAME_END 패킷을 보낼지 결정한다.
    /// 양쪽 클라이언트가 같은 DSP phase 기준으로 동시에 교신 성공을 판단할 수 있으므로,
    /// 네트워크 중복 전송을 줄이기 위해 기본적으로 Host만 전송한다.
    /// </summary>
    private bool ShouldSendCommunicationSuccessPacket()
    {
        NetworkManager net = NetworkManager.Instance;

        if (net == null)
        {
            return false;
        }

        if (!onlyHostSendsCommunicationSuccessPacket)
        {
            return true;
        }

        return net.IsHost;
    }

    /// <summary>
    /// 공격 phase 인덱스를 기준으로 해당 phase에서 사용할 RoundSetting을 반환한다.
    /// 라운드별 목표 노트 수 범위를 결정할 때 사용한다.
    /// </summary>
    private RoundSetting GetRoundSettingByAttackPhase(int attackPhaseIdx)
    {
        if (roundSettings == null || roundSettings.Length == 0)
        {
            return null;
        }

        int roundIndex = GetRoundIndexByAttackPhase(attackPhaseIdx);
        return roundSettings[roundIndex];
    }

    /// <summary>
    /// 현재 라운드 BPM 값을 반환한다.
    /// 라운드 설정이 없으면 기본 BPM 120을 반환한다.
    /// </summary>
    private float GetCurrentBpm()
    {
        RoundSetting setting = GetCurrentRoundSetting();

        if (setting == null)
        {
            return 120f;
        }

        return Mathf.Max(1f, setting.bpm);
    }

    /// <summary>
    /// 현재 BPM에 매핑된 코어 루프 BGM을 지정한 DSP 시각에 예약 재생한다.
    /// SoundManager가 없거나 BGM 재생이 꺼져 있으면 아무것도 하지 않는다.
    /// </summary>
    private void ScheduleCurrentCoreLoopBgm(double dspTime)
    {
        if (!playCoreLoopBgm) return;
        if (SoundManager.Instance == null) return;

        float bpm = GetCurrentBpm();
        SoundManager.Instance.ScheduleCoreLoopBgm(bpm, dspTime);
    }

    /// <summary>
    /// 현재 라운드의 시작 효과음을 재생한다.
    /// R1, R2, R3 라운드 진입 연출에 사용한다.
    /// </summary>
    private void PlayRoundStartSfx()
    {
        RoundSetting setting = GetCurrentRoundSetting();

        if (setting == null) return;
        if (SoundManager.Instance == null) return;

        SoundManager.Instance.PlaySfx(setting.roundUpSfx);

        Debug.Log($"Round Start SFX / round:{setting.roundName}, sfx:{setting.roundUpSfx}");
    }

    /// <summary>
    /// 공격/방어 입력 결과에 따라 성공 타격음 또는 MISS음을 재생한다.
    /// AttackTurn과 DefenseTurn의 입력 결과 이벤트가 이 함수를 호출한다.
    /// </summary>
    private void HandleHitResolved(NoteType noteType, bool isSuccess)
    {
        PlayHitResultSfx(noteType, isSuccess);
    }

    /// <summary>
    /// 여러 노트의 noteId와 NoteType을 캐시에 등록한다.
    /// 상대 방어 JUDGMENT 패킷이 noteId만 들고 오기 때문에 HitHigh/HitLow 재생에 필요하다.
    /// </summary>
    private void RegisterNoteTypes(IEnumerable<NoteData> notes)
    {
        if (notes == null) return;

        foreach (NoteData note in notes)
        {
            if (note == null) continue;
            RegisterNoteType(note.noteId, note.noteType);
        }
    }

    /// <summary>
    /// noteId와 NoteType을 캐시에 등록한다.
    /// NOTE_CREATED 수신 또는 로컬 공격 결과 저장 시 호출한다.
    /// </summary>
    private void RegisterNoteType(int noteId, NoteType noteType)
    {
        noteTypeByNoteId[noteId] = noteType;
    }

    /// <summary>
    /// 입력 또는 판정 결과에 따라 HitHigh, HitLow, Miss 중 하나를 재생한다.
    /// 성공 시 NoteType에 따라 고주파/저주파 타격음을 선택한다.
    /// </summary>
    private void PlayHitResultSfx(NoteType noteType, bool isSuccess)
    {
        if (SoundManager.Instance == null) return;

        if (!isSuccess)
        {
            SoundManager.Instance.PlaySfx(SfxId.Miss);
            return;
        }

        SfxId sfxId = noteType == NoteType.HIGH
            ? SfxId.HitHigh
            : SfxId.HitLow;

        SoundManager.Instance.PlaySfx(sfxId);
    }

    /// <summary>
    /// noteId 기반 JUDGMENT 결과에 따라 상대 방어 타격음 또는 MISS음을 재생한다.
    /// 성공 판정이면 noteId로 NoteType을 찾아 HitHigh/HitLow를 재생한다.
    /// </summary>
    private void PlayHitResultSfxByNoteId(int noteId, Judgment judgment)
    {
        bool isSuccess = judgment != Judgment.MISS;

        if (!isSuccess)
        {
            SoundManager.Instance?.PlaySfx(SfxId.Miss);
            return;
        }

        if (!noteTypeByNoteId.TryGetValue(noteId, out NoteType noteType))
        {
            Debug.LogWarning($"GameManager: noteId에 해당하는 NoteType을 찾을 수 없습니다. noteId:{noteId}");
            return;
        }

        PlayHitResultSfx(noteType, true);
    }

    /// <summary>
    /// 공격 턴에서 목표 노트 수에 맞게 노트를 생성했는지 판단한다.
    /// 박자 어긋남과 중복 입력은 턴 성공/실패 기준에 포함하지 않는다.
    /// </summary>
    private bool IsAttackTurnSuccess(AttackResult result)
    {
        return result.MissingNoteCount <= 0
            && result.ExtraNoteCount <= 0;
    }

    /// <summary>
    /// 방어 턴에서 MISS 없이 모든 노트를 처리했는지 판단한다.
    /// MISS가 하나라도 있으면 방어 턴 실패로 본다.
    /// </summary>
    private bool IsDefenseTurnSuccess(DefenseResult result)
    {
        return result.MissCount <= 0;
    }

    /// <summary>
    /// 턴 성공/실패 여부에 따라 TurnSuccess 또는 TurnFail 효과음을 재생한다.
    /// 공격/방어/상대 턴 결과음에 공통으로 사용한다.
    /// </summary>
    private void PlayTurnResultSfx(bool isSuccess)
    {
        if (SoundManager.Instance == null)
        {
            Debug.LogWarning("[TurnResultSFX] SoundManager.Instance가 없습니다.");
            return;
        }

        SfxId sfxId = isSuccess
            ? SfxId.TurnSuccess
            : SfxId.TurnFail;

        Debug.Log(
            $"[TurnResultSFX] {sfxId} / success:{isSuccess}, " +
            $"state:{currentState}, attacker:P{attackerPlayerId}, local:P{myLocalPlayerId}, " +
            $"network:{NetworkManager.Instance != null}"
        );

        SoundManager.Instance.PlaySfx(sfxId);
    }

    /// <summary>
    /// DEFENSE_END 패킷이 없는 최신 네트워크 구조에서,
    /// 내가 공격자였던 직전 방어 phase의 상대 방어 성공/실패음을 재생한다.
    /// 상대가 보낸 JUDGMENT 패킷 중 MISS가 하나도 없으면 방어 성공으로 본다.
    /// </summary>
    private void PlayPendingRemoteDefenseResultSfx()
    {
        if (!isWaitingRemoteDefenseResult) return;
        if (NetworkManager.Instance == null) return;

        bool defenseSuccess = remoteDefenseMissCount <= 0;
        PlayTurnResultSfx(defenseSuccess);

        isWaitingRemoteDefenseResult = false;
        remoteDefenseMissCount = 0;
    }
}
