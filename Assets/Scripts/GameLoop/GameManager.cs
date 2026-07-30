using UnityEngine;
using System.Collections;
/// <summary>
/// 게임 상태 머신 + 컴포넌트 간 이벤트 중계자 역할.
/// 턴 흐름 제어, 입력 라우팅, 이벤트 중계, 카메라 뷰 전환 등 게임 루프 전반을 관리.
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

    private Coroutine gameStartDelayCoroutine;

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

            net.OnAttackStart        += HandleNetworkAttackStart;
            net.OnNoteCreated        += defenseTurn.OnNoteReceived;
            net.OnAttackEnd          += HandleNetworkAttackEnd;
            net.OnJudgmentReceived   += HandleNetworkJudgment;
            net.OnDefenseEnd         += HandleNetworkDefenseEnd;
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
            // sanitySystem.OnSanityDamaged -= HandleSanityDamaged;
            sanitySystem.OnPlayerDefeated -= HandlePlayerDefeated;
        }

        var net = NetworkManager.Instance;
        if (net != null)
        {
            net.OnAttackStart        -= HandleNetworkAttackStart;
            net.OnNoteCreated        -= defenseTurn.OnNoteReceived;
            net.OnAttackEnd          -= HandleNetworkAttackEnd;
            net.OnJudgmentReceived   -= HandleNetworkJudgment;
            net.OnDefenseEnd         -= HandleNetworkDefenseEnd;
            net.OnGameEnd            -= HandleNetworkGameEnd;
            net.OnDisconnected       -= HandleNetworkDisconnected;
        }
    }

    /// <summary>
    /// 네트워크 모드에서 씬 로드 직후 자동으로 게임을 시작한다.
    /// </summary>
    private void Start()
    {   
        //결과 패널 숨기기
        resultPanelUI?.HideAll();

        completedTurnCount = 0;
        currentBpmStageIndex = 0;
        attackerPlayerId = NetworkManager.Instance?.FirstAttackerId ?? 1;

        if (NetworkManager.Instance != null)
            StartGame();
    }

    /// <summary>
    /// 게임을 초기화하고 첫 공격 턴을 시작.
    /// 정신력, BPM 단계, 리듬 클락을 초기화한 뒤 P1 공격부터 시작
    /// </summary>
    public void StartGame()
    {   
        resultPanelUI?.HideAll();

        completedTurnCount = 0;
        currentBpmStageIndex = 0;
        attackerPlayerId = NetworkManager.Instance?.FirstAttackerId ?? 1;
        
        // 첫 턴 시작 전 대기 중에는 입력이 공격/방어로 들어가면 안 되므로 TURN_CHANGE 상태로 둔다.
        currentState = GameState.TURN_CHANGE;

        if (sanitySystem != null)
        {
            sanitySystem.ResetSanity();
        }
        
        ApplyCurrentBpm();

        double rhythmStartDspTime = AudioSettings.dspTime;

        if (RhythmClock.Instance != null)
        {
            double startDspTime = NetworkManager.Instance != null
                ? NetworkManager.Instance.LocalGameStartDspTime
                : AudioSettings.dspTime;
            RhythmClock.Instance.StartClock(rhythmStartDspTime);
        }
        if (gameStartDelayCoroutine != null)
        {
            StopCoroutine(gameStartDelayCoroutine);
        }

        gameStartDelayCoroutine = StartCoroutine(StartFirstTurnAfterDelay(rhythmStartDspTime));
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

    /// <summary>
    /// 공격 턴을 시작하고 HUD와 카메라를 업데이트.
    /// </summary>
    private void StartAttackPhase()
    {
        currentState = GameState.ATTACK;
        AttackSide attackerSide = attackerPlayerId == 1 ? AttackSide.P1 : AttackSide.P2;
        gameCamera?.SetAttackView(attackerSide);

        bool isNetworkMode   = NetworkManager.Instance != null;
        bool isLocalAttacker = !isNetworkMode
            ? attackerPlayerId == 1
            : attackerPlayerId == myLocalPlayerId;

        if (isLocalAttacker)
        {
            int targetNoteCount  = noteCountGenerator.CreateRandomNoteCount();
            string attackMessage = randomMessageProvider.GetRandomMessage(targetNoteCount);
            attackTurn.StartLocalPlayerAttack(attackerSide, targetNoteCount, attackMessage);
        }
        else if (!isNetworkMode)
        {
            // 로컬 전용: 상대 공격은 데모로 처리
            int targetNoteCount  = attackTurn.OpponentDemoNoteCount;
            string attackMessage = randomMessageProvider.GetRandomMessage(targetNoteCount);
            attackTurn.StartOpponentAttackDemo(attackMessage);
        }
        // 네트워크 원격 공격: NOTE_CREATED / ATTACK_END 수신 대기, 로컬 AttackTurn 실행 안 함
    }

    /// <summary>
    /// 공격 턴 종료 시 공격자 패널티를 적용하고 방어 턴을 시작한다.
    /// 공격자의 정신력이 0이 되는 경우 게임 종료 처리는 SanitySystem.OnPlayerDefeated 이벤트가 담당한다.
    /// </summary>
    private void HandleAttackEnded(AttackResult attackResult)
    {
        if (currentState == GameState.END) return;

        currentState = GameState.DEFENSE;

        // 공격 실패 패널티 적용.
        // 이 과정에서 정신력이 0이 되면 SanitySystem.OnPlayerDefeated가 발생한다.
        sanitySystem?.ApplyAttackResult(attackerPlayerId, attackResult);

        // ApplyAttackResult 중 게임이 종료되었으면 방어 턴을 시작하지 않는다.
        if (currentState == GameState.END) return;

        AttackSide attackerSide = GetAttackSide(attackerPlayerId);
        gameCamera?.SetDefenseView(attackerSide);

        float judgeLineX = attackTurnRenderer.GetJudgeLineX(attackerSide);
        float attackStartX = attackTurnRenderer.GetStartX(attackerSide);
        float attackEndX = attackTurnRenderer.GetEndX(attackerSide);

        if (NetworkManager.Instance != null)
        {
            // 네트워크 모드에서 로컬이 공격자라면,
            // 공격자 화면에서도 방어 노트 이동을 미러뷰로 보여준다.
            // networkManager:null → DEFENSE_END 전송 안 함.
            // 실제 판정 결과는 상대가 보낸 JUDGMENT 패킷으로 처리한다.
            if (attackerPlayerId == myLocalPlayerId)
            {
                defenseTurn.Begin(
                    attackResult.Notes,
                    judgeLineX,
                    attackStartX,
                    attackEndX,
                    attackTurn.AttackDuration,
                    isAiDefense: false,
                    networkManager: null,
                    remoteAttackStartDspTime: attackTurn.AttackStartDspTime
                );
            }

            return;
        }

        // 로컬 테스트 모드.
        defenseTurn.Begin(
            attackResult.Notes,
            judgeLineX,
            attackStartX,
            attackEndX,
            attackTurn.AttackDuration,
            isAiDefense: attackerPlayerId == 1
        );
    }

    /// <summary>
    /// 실제 방어자의 노트 판정 결과를 처리한다.
    /// MISS일 경우 방어자의 정신력을 감소시킨다.
    /// 정신력이 0이 되는 순간의 게임 종료 처리는 SanitySystem.OnPlayerDefeated 이벤트가 담당한다.
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

        // 여기서는 정신력만 깎는다.
        // 정신력이 0이 되면 SanitySystem 내부에서 OnPlayerDefeated 이벤트가 발생한다.
        sanitySystem?.ApplyDefenseMiss(defenderPlayerId);
    }

    /// <summary>
    /// 특정 플레이어의 정신력이 0이 되어 게임이 종료되었을 때 호출한다.
    /// 내 화면에는 localPlayerId 기준으로 승리/패배 패널을 표시하고,
    /// 내가 직접 감지한 종료라면 상대에게 GAME_END 패킷을 전송한다.
    /// </summary>
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
    /// 현재 공격자의 반대 플레이어를 방어자로 반환한다.
    /// P1이 공격 중이면 P2가 방어자, P2가 공격 중이면 P1이 방어자다.
    /// </summary>
    private int GetDefenderPlayerId()
    {
        return attackerPlayerId == 1 ? 2 : 1;
    }

    /// <summary>
    /// 플레이어 ID를 HUD와 Renderer에서 사용하는 AttackSide 값으로 변환한다.
    /// </summary>
    private AttackSide GetAttackSide(int playerId)
    {
        return playerId == 1 ? AttackSide.P1 : AttackSide.P2;
    }
    /// <summary>
    /// 방어 턴의 개별 노트 판정 이벤트 처리.
    /// 판정 결과를 HUD에 표시하고, MISS인 경우 방어자의 정신력을 즉시 감소시킨다.
    /// </summary>
    private void HandleDefenseEnded(DefenseResult result)
    {
        if (currentState == GameState.END) return;

        if (NetworkManager.Instance != null)
        {
            // 공격자 미러 뷰(networkManager:null로 Begin된 경우)는 무시
            // 로컬이 실제 방어자인 경우에만 턴 전환 (DEFENSE_END는 DefenseTurn이 이미 전송)
            if (attackerPlayerId == myLocalPlayerId) return;
            completedTurnCount++;
            UpdateBpmByTurnCount();
            SwitchTurn();
            return;
        }

        int defenderPlayerId = attackerPlayerId == 1 ? 2 : 1;
        Debug.Log($"Turn End / attacker:P{attackerPlayerId}, defender:P{defenderPlayerId}, miss:{result.MissCount}");
        completedTurnCount++;
        UpdateBpmByTurnCount();
        SwitchTurn();
    }

    /// <summary>
    /// 턴을 전환하고 다음 공격 턴을 시작.
    /// </summary>
    private void SwitchTurn()
    {
        currentState = GameState.TURN_CHANGE;
        attackTurnRenderer.ClearAll();
        if (hud != null) hud.ClearJudgments();
        attackerPlayerId = attackerPlayerId == 1 ? 2 : 1;
        StartAttackPhase();
    }

    /// <summary>
    /// SanitySystem의 정신력 변경 이벤트를 HUD에 반영한다.
    /// </summary>
    private void HandleSanityChanged(int p1Sanity, int p2Sanity, int maxSanity)
    {
        if (hud == null) return;
        hud.UpdateSanity(p1Sanity, p2Sanity, maxSanity);
    }

    /// <summary>
    /// SanitySystem에서 플레이어 정신력이 0이 되었을 때 호출된다.
    /// 로컬 플레이어 기준으로 승리/패배 결과를 표시하고,
    /// 내가 직접 만든 게임 종료라면 상대에게 GAME_END 패킷을 전송한다.
    /// </summary>
    private void HandlePlayerDefeated(int defeatedPlayerId)
    {
        bool shouldSendNetworkPacket =
            NetworkManager.Instance != null &&
            !isApplyingRemoteGameState;

        EndGameByDefeatedPlayer(defeatedPlayerId, shouldSendNetworkPacket);
    }

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
    /// 완료 턴 수를 기준으로 BPM 단계 상승 여부를 확인한다.
    /// turnsPerBpmIncrease마다 다음 BPM 단계로 이동한다.
    /// </summary>
    private void UpdateBpmByTurnCount()
    {
        if (turnsPerBpmIncrease <= 0) return;
        if (bpmStages == null || bpmStages.Length == 0) return;

        int nextStageIndex = completedTurnCount / turnsPerBpmIncrease;
        nextStageIndex = Mathf.Clamp(nextStageIndex, 0, bpmStages.Length - 1);

        if (nextStageIndex == currentBpmStageIndex) return;

        currentBpmStageIndex = nextStageIndex;
        ApplyCurrentBpm();
    }

    /// <summary>
    /// 원격 공격자의 ATTACK_START 수신 시 호출. 방어자 화면 준비 및 공격 컨텍스트를 DefenseTurn에 전달한다.
    /// </summary>
    private void HandleNetworkAttackStart(AttackStartPacket packet)
    {
        AttackSide attackerSide = packet.attackerPlayerId == 1 ? AttackSide.P1 : AttackSide.P2;
        gameCamera?.SetAttackView(attackerSide);

        var net = NetworkManager.Instance;
        if (net != null && attackTurnRenderer != null)
        {
            double correctedStart = net.TimeSync.CorrectTime(packet.attackStartDspTime);
            Debug.Log($"[Net] ATTACK_START 수신 / attacker:P{packet.attackerPlayerId}, correctedStart:{correctedStart:F3}, now:{AudioSettings.dspTime:F3}, lag:{(AudioSettings.dspTime - correctedStart) * 1000:F1}ms");
            attackTurnRenderer.BeginAttackVisual(attackerSide, correctedStart, packet.attackDuration, 0);
        }

        defenseTurn.OnAttackStartReceived(packet);
    }

    /// <summary>
    /// 원격 공격자의 ATTACK_END 수신 시 호출. 로컬에서 방어 턴을 시작한다.
    /// </summary>
    private void HandleNetworkAttackEnd(AttackEndPacket packet)
    {
        var net = NetworkManager.Instance;
        if (net == null) return;

        currentState = GameState.DEFENSE;
        AttackSide attackerSide = packet.attackerPlayerId == 1 ? AttackSide.P1 : AttackSide.P2;
        gameCamera?.SetDefenseView(attackerSide);

        attackTurnRenderer?.StopLine();

        if (attackTurnRenderer == null) return;

        float judgeLineX   = attackTurnRenderer.GetJudgeLineX(attackerSide);
        float attackStartX = attackTurnRenderer.GetStartX(attackerSide);
        float attackEndX   = attackTurnRenderer.GetEndX(attackerSide);

        defenseTurn.OnAttackEndReceived(packet, judgeLineX, attackStartX, attackEndX, net);
    }

    /// <summary>
    /// 원격 방어자의 DEFENSE_END 수신 시 호출. 턴을 전환한다.
    /// </summary>
    private void HandleNetworkDefenseEnd(DefenseEndPacket packet)
    {
        if (currentState == GameState.END) return;
        completedTurnCount++;
        UpdateBpmByTurnCount();
        SwitchTurn();
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
    /// 상대 클라이언트에서 받은 판정 패킷을 처리한다.
    /// MISS일 경우 로컬 정신력 상태도 맞춰주지만,
    /// 이 과정에서 발생한 패배는 다시 GAME_END로 전송하지 않는다.
    /// </summary>
    private void HandleNetworkJudgment(JudgmentPacket packet)
    {
        if (currentState == GameState.END) return;

        Judgment judgment = (Judgment)packet.judgment;
        AttackSide attackerSide = GetAttackSide(attackerPlayerId);

        defenseTurn?.RemoteRemoveNote(packet.noteId);
        hud?.ShowJudgment(judgment, attackerSide);

        if (judgment != Judgment.MISS) return;

        int defenderPlayerId = GetDefenderPlayerId();

        isApplyingRemoteGameState = true;

        try
        {
            // 상대에게 받은 판정 결과를 내 로컬 정신력 상태에도 반영한다.
            // 이 과정에서 OnPlayerDefeated가 발생해도 GAME_END를 다시 보내면 안 된다.
            sanitySystem?.ApplyDefenseMiss(defenderPlayerId);
        }
        finally
        {
            isApplyingRemoteGameState = false;
        }
    }
    /// <summary>
    /// 리듬 클락 시작 시각을 기준으로 첫 공격 턴 시작 시간을 계산하고,
    /// 해당 시간이 될 때까지 기다린 뒤 첫 공격 턴을 시작한다.
    /// </summary>
    private IEnumerator StartFirstTurnAfterDelay(double rhythmStartDspTime)
    {
        double delaySeconds = GetGameStartDelaySeconds();

        // 딜레이가 0이면 다음 프레임까지 기다리지 않고 바로 첫 공격 턴을 시작한다.
        if (delaySeconds <= 0.0)
        {
            gameStartDelayCoroutine = null;
            StartAttackPhase();
            yield break;
        }

        double firstTurnStartDspTime = rhythmStartDspTime + delaySeconds;

        Debug.Log(
            $"Game Start Delay / msEnabled:{useMillisecondStartDelay}, ms:{gameStartDelayMs}, " +
            $"beatEnabled:{useBeatStartDelay}, beats:{gameStartDelayBeats}, " +
            $"totalDelay:{delaySeconds:F3}s, firstTurnStart:{firstTurnStartDspTime:F3}"
        );

        while (AudioSettings.dspTime < firstTurnStartDspTime)
        {
            if (currentState == GameState.END)
            {
                gameStartDelayCoroutine = null;
                yield break;
            }

            yield return null;
        }

        gameStartDelayCoroutine = null;

        if (currentState == GameState.END) yield break;

        StartAttackPhase();
    }
    /// <summary>
    /// 인스펙터 체크박스 설정에 따라 첫 턴 시작 전 대기 시간을 초 단위로 계산한다.
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
