using UnityEngine;
/// <summary>
/// 게임 상태 머신 + 컴포넌트 간 이벤트 중계자 역할.
/// DSP 페이즈 루프로 턴 흐름을 제어하고, 입력 라우팅, 이벤트 중계, 카메라 뷰 전환 등 게임 루프 전반을 관리.
/// </summary>
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

    [Header("BPM Progression")]
    [SerializeField] private int turnsPerBpmIncrease = 2;
    [SerializeField] private float[] bpmStages = { 106f, 120f, 144f };

    [Header("Game Start Delay")]
    [SerializeField] private bool useMillisecondStartDelay = true;
    [SerializeField] private int gameStartDelayMs = 1000;

    [SerializeField] private bool useBeatStartDelay = true;
    [SerializeField] private int gameStartDelayBeats = 4;

    // DSP 페이즈 루프
    private int    phaseIndex;
    private double nextPhaseDspTime;
    private double currentTurnDuration;
    private double localAttackStartDspTime;

    // 네트워크 노트 수신 추적
    private int currentTargetNoteCount;
    private int currentReceivedNoteCount;

    private int completedTurnCount;
    private int currentBpmStageIndex;

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
        defenseTurn.OnDefenseEnded += HandleDefenseEnded;
        defenseTurn.OnJudgment += HandleJudgment;

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
        }

        if (defenseTurn != null)
        {
            defenseTurn.OnDefenseEnded -= HandleDefenseEnded;
            defenseTurn.OnJudgment -= HandleJudgment;
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

        completedTurnCount = 0;
        currentBpmStageIndex = 0;
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

        phaseIndex           = 0;
        currentBpmStageIndex = 0;
        ApplyCurrentBpm();
        currentTurnDuration  = GetCurrentTurnDuration();

        var net = NetworkManager.Instance;
        double rhythmStart = net?.LocalGameStartDspTime ?? AudioSettings.dspTime;

        RhythmClock.Instance?.StartClock(rhythmStart);

        nextPhaseDspTime = rhythmStart + GetGameStartDelaySeconds();
        currentState     = GameState.TURN_CHANGE;
        attackerPlayerId = net?.FirstAttackerId ?? 1;
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

        if (isAttackPhase)
        {
            int nextStage = Mathf.Clamp(attackPhaseIdx / turnsPerBpmIncrease, 0, bpmStages.Length - 1);
            if (nextStage != currentBpmStageIndex)
            {
                currentBpmStageIndex = nextStage;
                ApplyCurrentBpm();
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
        double t = NetworkManager.Instance?.LocalGameStartDspTime ?? 0.0;
        int seed = unchecked((int)(t * 1000) + attackPhaseIdx * 997);
        return new System.Random(seed);
    }

    // ── Attack Phase ─────────────────────────────────────────────────────────────

    private void StartAttackPhase(double phaseStartDspTime, int attackPhaseIdx)
    {
        currentState = GameState.ATTACK;
        AttackSide attackerSide = GetAttackSide(attackerPlayerId);

        attackTurnRenderer?.ClearAll();
        hud?.ClearJudgments();
        gameCamera?.SetAttackView(attackerSide);

        var rng       = GetSharedRng(attackPhaseIdx);
        int noteCount = noteCountGenerator.CreateRandomNoteCount(rng);
        string msg    = randomMessageProvider.GetRandomMessage(noteCount, rng);

        bool isLocalAttacker = (attackerPlayerId == myLocalPlayerId ||
                                NetworkManager.Instance == null);

        if (isLocalAttacker)
        {
            attackTurn.StartLocalPlayerAttack(attackerSide, noteCount, msg, phaseStartDspTime);
            hud?.ShowAttackMessage(msg, attackerSide);
            hud?.UpdateAttackProgress(0, noteCount);
        }
        else if (NetworkManager.Instance == null)
        {
            attackTurn.StartOpponentAttackDemo(msg);
            hud?.ShowAttackMessage(msg, attackerSide);
            hud?.UpdateAttackProgress(0, noteCount);
        }
        else
        {
            currentTargetNoteCount   = noteCount;
            currentReceivedNoteCount = 0;
            hud?.ShowAttackMessage(msg, attackerSide);
            hud?.UpdateAttackProgress(0, noteCount);

            defenseTurn?.PrepareForIncomingAttack(attackerSide, currentTurnDuration);
            attackTurnRenderer?.BeginAttackVisual(attackerSide, phaseStartDspTime,
                currentTurnDuration, noteCount);
        }
    }

    // ── Defense Phase ─────────────────────────────────────────────────────────────

    private void StartDefensePhase(double phaseStartDspTime)
    {
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
            defenseTurn?.BeginTransfer(judgeLineX, attackStartX, attackEndX,
                currentTurnDuration, NetworkManager.Instance, phaseStartDspTime);
        }
        else if (!isNetworkMode)
        {
            currentState = GameState.DEFENSE;
        }
        // isLocalAttacker && isNetworkMode: 공격자 미러뷰는 HandleAttackEnded에서 처리
    }

    // ── Attack/Defense Event Handlers ─────────────────────────────────────────────

    /// <summary>
    /// 공격 턴 종료 시 공격자 패널티를 적용하고 네트워크 모드에서 미러뷰를 시작한다.
    /// 공격자의 정신력이 0이 되는 경우 게임 종료 처리는 SanitySystem.OnPlayerDefeated 이벤트가 담당한다.
    /// </summary>
    private void HandleAttackEnded(AttackResult attackResult)
    {
        if (currentState == GameState.END) return;

        sanitySystem?.ApplyAttackResult(attackerPlayerId, attackResult);

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
            AttackSide attackerSide = GetAttackSide(attackerPlayerId);
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
    /// 방어 턴 종료 이벤트. DSP 페이즈 루프가 자동으로 다음 phase를 시작하므로 별도 전환 불필요.
    /// </summary>
    private void HandleDefenseEnded(DefenseResult result)
    {
        if (currentState == GameState.END) return;
        // DSP 페이즈 루프가 자동으로 다음 phase를 시작 — SwitchTurn 불필요
        Debug.Log($"Defense End / miss:{result.MissCount}");
    }

    // ── Network Event Handlers ────────────────────────────────────────────────────

    /// <summary>
    /// 원격 공격자가 생성한 노트 패킷을 처리한다.
    /// DefenseTurn에는 노트 데이터를 전달하고, HUD에는 수신된 노트 개수 진행도를 표시한다.
    /// </summary>
    private void HandleNetworkNoteCreated(NoteCreatedPacket packet)
    {
        defenseTurn?.OnNoteReceived(packet, localAttackStartDspTime);
        currentReceivedNoteCount++;
        if (currentTargetNoteCount > 0)
            hud?.UpdateAttackProgress(currentReceivedNoteCount, currentTargetNoteCount);
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
        if (hud == null) return;

        AttackSide attackerSide = attackerPlayerId == 1 ? AttackSide.P1 : AttackSide.P2;
        hud.ShowAttackMessage(message, attackerSide);
    }

    /// <summary>
    /// AttackTurn의 노트 생성 진행도를 HUD의 별 UI에 반영한다.
    /// </summary>
    private void HandleAttackProgressChanged(int currentCount, int targetCount)
    {
        if (hud == null) return;

        hud.UpdateAttackProgress(currentCount, targetCount);
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
    /// 현재 BPM 단계 인덱스에 해당하는 BPM을 RhythmClock과 HUD에 적용한다.
    /// </summary>
    private void ApplyCurrentBpm()
    {
        if (bpmStages == null || bpmStages.Length == 0)
        {
            Debug.LogWarning("BPM Stages가 비어 있습니다.");
            return;
        }

        currentBpmStageIndex = Mathf.Clamp(currentBpmStageIndex, 0, bpmStages.Length - 1);
        float bpm = bpmStages[currentBpmStageIndex];

        if (RhythmClock.Instance != null)
        {
            RhythmClock.Instance.SetBpm(bpm);
        }

        if (hud != null)
        {
            hud.UpdateBpm(bpm);
        }

        Debug.Log($"BPM Changed / stage:{currentBpmStageIndex}, bpm:{bpm}");
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
    /// 현재 BPM 단계 기준으로 1박자의 길이를 초 단위로 반환한다.
    /// 첫 턴 시작 전에는 ApplyCurrentBpm()으로 currentBpmStageIndex가 먼저 설정되어 있어야 한다.
    /// </summary>
    private double GetCurrentBeatDurationSeconds()
    {
        if (bpmStages == null || bpmStages.Length == 0)
        {
            return 60.0 / 120.0;
        }

        int safeIndex = Mathf.Clamp(currentBpmStageIndex, 0, bpmStages.Length - 1);
        float bpm = Mathf.Max(1f, bpmStages[safeIndex]);

        return 60.0 / bpm;
    }
}
