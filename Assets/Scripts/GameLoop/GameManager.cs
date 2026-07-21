using UnityEngine;

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
    
    [Header("BPM Progression")]
    [SerializeField] private int turnsPerBpmIncrease = 2;
    [SerializeField] private float[] bpmStages = { 106f, 120f, 144f };

    private int completedTurnCount;
    private int currentBpmStageIndex;

    private GameState currentState;
    private int attackerPlayerId = 1;
    private int myLocalPlayerId = 1; // NetworkManager.Instance에서 Awake() 시 읽어옴

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
        }
    }

    /// <summary>
    /// 네트워크 모드에서 씬 로드 직후 자동으로 게임을 시작한다.
    /// </summary>
    private void Start()
    {
        if (NetworkManager.Instance != null)
            StartGame();
    }

    /// <summary>
    /// 게임을 초기화하고 첫 공격 턴을 시작.
    /// 정신력, BPM 단계, 리듬 클락을 초기화한 뒤 P1 공격부터 시작
    /// </summary>
    public void StartGame()
    {   
        completedTurnCount = 0;
        currentBpmStageIndex = 0;
        attackerPlayerId = NetworkManager.Instance?.FirstAttackerId ?? 1;
        
        if (sanitySystem != null)
        {
            sanitySystem.ResetSanity();
        }
        

        ApplyCurrentBpm();

        if (RhythmClock.Instance != null)
        {
            double startDspTime = NetworkManager.Instance != null
                ? NetworkManager.Instance.LocalGameStartDspTime
                : AudioSettings.dspTime;
            RhythmClock.Instance.StartClock(startDspTime);
        }

        StartAttackPhase();
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
    /// 공격 턴 종료 시 방어 턴을 시작하고 HUD와 카메라를 업데이트.
    /// </summary>
    private void HandleAttackEnded(AttackResult attackResult)
    {
        currentState = GameState.DEFENSE;
        sanitySystem?.ApplyAttackResult(attackerPlayerId, attackResult);

        if (sanitySystem != null && sanitySystem.IsPlayerDefeated(attackerPlayerId))
        {
            currentState = GameState.END;
            NetworkManager.Instance?.Send(w => PacketSerializer.WriteGameEnd(w));
            Debug.Log($"Game End / P{attackerPlayerId} sanity depleted during attack.");
            return;
        }

        AttackSide attackerSide = attackerPlayerId == 1 ? AttackSide.P1 : AttackSide.P2;
        gameCamera?.SetDefenseView(attackerSide);

        float judgeLineX   = attackTurnRenderer.GetJudgeLineX(attackerSide);
        float attackStartX = attackTurnRenderer.GetStartX(attackerSide);
        float attackEndX   = attackTurnRenderer.GetEndX(attackerSide);

        if (NetworkManager.Instance != null)
        {
            // 네트워크 모드: 로컬이 공격자일 때 공격자 화면에서도 노트 이동 시작.
            // networkManager:null → DEFENSE_END 전송 안 함. 판정은 JUDGMENT 패킷이 담당.
            if (attackerPlayerId == myLocalPlayerId)
                defenseTurn.Begin(attackResult.Notes, judgeLineX, attackStartX, attackEndX,
                    attackTurn.AttackDuration, isAiDefense: false, networkManager: null);
            return;
        }

        defenseTurn.Begin(attackResult.Notes, judgeLineX, attackStartX, attackEndX,
            attackTurn.AttackDuration, isAiDefense: (attackerPlayerId == 1));
    }

    /// <summary>
    /// 방어 턴에서 노트 판정 시 HUD에 결과를 표시.
    /// </summary>
    private void HandleJudgment(Judgment judgment)
    {
        // 네트워크 모드에서 로컬이 공격자면: 판정 시각화는 JUDGMENT 패킷(HandleNetworkJudgment)이 담당
        if (NetworkManager.Instance != null && attackerPlayerId == myLocalPlayerId) return;

        AttackSide attackerSide = attackerPlayerId == 1 ? AttackSide.P1 : AttackSide.P2;
        hud?.ShowJudgment(judgment, attackerSide);

        if (judgment != Judgment.MISS) return;

        int defenderPlayerId = attackerPlayerId == 1 ? 2 : 1;
        sanitySystem?.ApplyDefenseMiss(defenderPlayerId);

        if (sanitySystem != null && sanitySystem.IsPlayerDefeated(defenderPlayerId))
        {
            currentState = GameState.END;
            Debug.Log($"Game End / P{defenderPlayerId} sanity depleted during defense.");
        }
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
    /// 플레이어 정신력이 0이 되었을 때 호출된다.
    /// 현재는 로그만 출력하며, 이후 게임 종료 UI와 연결할 수 있다.
    /// </summary>
    private void HandlePlayerDefeated(int playerId)
    {
        Debug.Log($"Player Defeated / P{playerId}");
        NetworkManager.Instance?.Send(w => PacketSerializer.WriteGameEnd(w));
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
    /// 방어자의 노트 판정 결과 수신 시 호출. 공격자 화면에서 노트 제거 및 판정 UI를 표시한다.
    /// </summary>
    private void HandleNetworkJudgment(JudgmentPacket packet)
    {
        Judgment judgment = (Judgment)packet.judgment;
        AttackSide attackerSide = attackerPlayerId == 1 ? AttackSide.P1 : AttackSide.P2;

        defenseTurn.RemoteRemoveNote(packet.noteId);
        hud?.ShowJudgment(judgment, attackerSide);

        if (judgment != Judgment.MISS) return;

        int defenderPlayerId = attackerPlayerId == 1 ? 2 : 1;
        sanitySystem?.ApplyDefenseMiss(defenderPlayerId);

        if (sanitySystem != null && sanitySystem.IsPlayerDefeated(defenderPlayerId))
        {
            currentState = GameState.END;
            Debug.Log($"Game End / P{defenderPlayerId} sanity depleted during defense.");
        }
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
    /// 원격 기기의 GAME_END 수신 시 호출.
    /// </summary>
    private void HandleNetworkGameEnd()
    {
        currentState = GameState.END;
        Debug.Log("GameManager: 상대방 게임 종료 신호 수신");
    }

}
