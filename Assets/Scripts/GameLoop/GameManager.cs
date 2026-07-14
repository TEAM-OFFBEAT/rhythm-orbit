using System.Collections.Generic;
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
    [SerializeField] private NoteCountGenerator noteCountGenerator;
    [SerializeField] private RandomMessageProvider randomMessageProvider;

    [Header("HUD")]
    [SerializeField] private HUD hud;

    [Header("Camera")]
    [SerializeField] private GameCamera gameCamera;
    
    [Header("Sanity Systems")]
    [SerializeField] private SanitySystem sanitySystem;
    [Header("BPM Progression")]
    [SerializeField] private int turnsPerBpmIncrease = 2;
    [SerializeField] private float[] bpmStages = { 106f, 120f, 144f };

    private int completedTurnCount;
    private int currentBpmStageIndex;

    private GameState currentState;
    private int attackerPlayerId = 1;

    private void Awake()
    {
        if (attackTurn == null || defenseTurn == null || attackTurnRenderer == null ||
            sanitySystem == null || noteCountGenerator == null || randomMessageProvider == null)
        {
            Debug.LogError("GameManager: 필수 컴포넌트가 연결되지 않았습니다.");
            return;
        }
        sanitySystem.OnSanityChanged += HandleSanityChanged;
        //sanitySystem.OnSanityDamaged += HandleSanityDamaged;
        sanitySystem.OnPlayerDefeated += HandlePlayerDefeated;
        
        attackTurn.OnAttackEnded += HandleAttackEnded;
        attackTurn.OnAttackMessageSelected += HandleAttackMessageSelected;
        defenseTurn.OnDefenseEnded += HandleDefenseEnded;
        defenseTurn.OnJudgment += HandleJudgment;
        
    }

    private void OnDestroy()
    {
        if (attackTurn != null)
        {
            attackTurn.OnAttackEnded -= HandleAttackEnded;
            attackTurn.OnAttackMessageSelected -= HandleAttackMessageSelected;
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
    }

    /// <summary>
    /// 게임을 초기화하고 첫 공격 턴을 시작.
    /// </summary>
    public void StartGame()
    {
        attackerPlayerId = 1;
        if (sanitySystem != null)
        {
            sanitySystem.ResetSanity();
        }
        

        ApplyCurrentBpm();

        if (RhythmClock.Instance != null)
        {
            RhythmClock.Instance.StartClockNow();
        }

        StartAttackPhase();
    }

    /// <summary>
    /// 현재 게임 상태에 따라 입력을 적절한 턴 컴포넌트로 전달.
    /// </summary>
    public void OnTap()
    {
        if (currentState == GameState.ATTACK)
            attackTurn.OnTap();
        else if (currentState == GameState.DEFENSE)
            defenseTurn.OnTap();
    }

    /// <summary>
    /// 공격 턴을 시작하고 HUD와 카메라를 업데이트.
    /// </summary>
    private void StartAttackPhase()
    {
        currentState = GameState.ATTACK;

        AttackSide attackerSide = attackerPlayerId == 1 ? AttackSide.P1 : AttackSide.P2;

        gameCamera?.SetAttackView(attackerSide);

        if (attackerPlayerId == 1)
        {
            int targetNoteCount = noteCountGenerator.CreateRandomNoteCount();
            string attackMessage = randomMessageProvider.GetRandomMessage(targetNoteCount);

            attackTurn.StartLocalPlayerAttack(targetNoteCount, attackMessage);
        }
        else
        {
            int targetNoteCount = attackTurn.OpponentDemoNoteCount;
            string attackMessage = randomMessageProvider.GetRandomMessage(targetNoteCount);

            attackTurn.StartOpponentAttackDemo(attackMessage);
        }
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
            Debug.Log($"Game End / P{attackerPlayerId} sanity depleted during attack.");
            return;
        }

        bool isAiDefense = (attackerPlayerId == 1);

        AttackSide attackerSide = attackerPlayerId == 1 ? AttackSide.P1 : AttackSide.P2;

        gameCamera?.SetDefenseView(attackerSide);

        float judgeLineX = attackTurnRenderer.GetJudgeLineX(attackerSide);
        float attackStartX = attackTurnRenderer.GetStartX(attackerSide);
        float attackEndX = attackTurnRenderer.GetEndX(attackerSide);

        defenseTurn.Begin(
            attackResult.Notes,
            judgeLineX,
            attackStartX,
            attackEndX,
            attackTurn.AttackDuration,
            isAiDefense
        );
    }
    /// <summary>
    /// 방어 턴에서 노트 판정 시 HUD에 결과를 표시.
    /// </summary>
    private void HandleJudgment(Judgment judgment)
    {
        AttackSide attackerSide = attackerPlayerId == 1 ? AttackSide.P1 : AttackSide.P2;

        if (hud != null)
        {
            hud.ShowJudgment(judgment, attackerSide);
        }

        if (judgment != Judgment.MISS) return;

        int defenderPlayerId = attackerPlayerId == 1 ? 2 : 1;

        sanitySystem?.ApplyDefenseMiss(defenderPlayerId);

        if (sanitySystem != null && sanitySystem.IsPlayerDefeated(defenderPlayerId))
        {
            currentState = GameState.END;
            Debug.Log($"Game End / P{defenderPlayerId} sanity depleted during defense.");
        }
    }

    private void HandleDefenseEnded(DefenseResult result)
{
    if (currentState == GameState.END) return;

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

    private void HandleSanityChanged(int p1Sanity, int p2Sanity, int maxSanity)
    {
        if (hud == null) return;
        hud.UpdateSanity(p1Sanity, p2Sanity, maxSanity);
    }

    /*
    private void HandleSanityDamaged(int playerId, int amount, string reason)
    {
        if (hud == null) return;
        hud.ShowSanityDamage(playerId, amount, reason);
    }
    */

    private void HandlePlayerDefeated(int playerId)
    {
        Debug.Log($"Player Defeated / P{playerId}");
    }
    private void HandleAttackMessageSelected(string message, int noteCount)
    {
        if (hud == null) return;

        AttackSide attackerSide = attackerPlayerId == 1 ? AttackSide.P1 : AttackSide.P2;
        hud.ShowAttackMessage(message, attackerSide);
    }
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
}
