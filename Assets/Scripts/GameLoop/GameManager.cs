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

    [Header("HUD")]
    [SerializeField] private HUD hud;

    [Header("Camera")]
    [SerializeField] private GameCamera gameCamera;

    private GameState currentState;
    private int attackerPlayerId = 1;

    private void Awake()
    {
        if (attackTurn == null || defenseTurn == null || attackTurnRenderer == null)
        {
            Debug.LogError("GameManager: 필수 컴포넌트가 연결되지 않았습니다.");
            return;
        }

        attackTurn.OnAttackEnded += HandleAttackEnded;
        defenseTurn.OnDefenseEnded += HandleDefenseEnded;
        defenseTurn.OnJudgment += HandleJudgment;
    }

    private void OnDestroy()
    {
        if (attackTurn != null) attackTurn.OnAttackEnded -= HandleAttackEnded;
        if (defenseTurn != null)
        {
            defenseTurn.OnDefenseEnded -= HandleDefenseEnded;
            defenseTurn.OnJudgment -= HandleJudgment;
        }
    }

    /// <summary>
    /// 게임을 초기화하고 첫 공격 턴을 시작.
    /// </summary>
    public void StartGame()
    {
        attackerPlayerId = 1;
        if (RhythmClock.Instance != null) RhythmClock.Instance.StartClockNow();
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

        // attackTurn.RegisterView(attackTurnRenderer, null, null);

        if (attackerPlayerId == 1)
            attackTurn.StartLocalPlayerAttack();
        else
            attackTurn.StartOpponentAttackDemo();
    }

    /// <summary>
    /// 공격 턴 종료 시 방어 턴을 시작하고 HUD와 카메라를 업데이트.
    /// </summary>
    private void HandleAttackEnded(IReadOnlyList<NoteData> notes)
    {
        currentState = GameState.DEFENSE;

        bool isAiDefense = (attackerPlayerId == 1);

        AttackSide attackerSide = attackerPlayerId == 1 ? AttackSide.P1 : AttackSide.P2;

        gameCamera?.SetDefenseView(attackerSide);

        float judgeLineX = attackTurnRenderer.GetJudgeLineX(attackerSide);
        float attackStartX = attackTurnRenderer.GetStartX(attackerSide);
        float attackEndX = attackTurnRenderer.GetEndX(attackerSide);

        defenseTurn.Begin(notes, judgeLineX, attackStartX, attackEndX, attackTurn.AttackDuration, isAiDefense);
    }
    /// <summary>
    /// 방어 턴에서 노트 판정 시 HUD에 결과를 표시.
    /// </summary>
    private void HandleJudgment(Judgment judgment)
    {
        if (hud == null) return;
        AttackSide attackerSide = attackerPlayerId == 1 ? AttackSide.P1 : AttackSide.P2;
        hud.ShowJudgment(judgment, attackerSide);
    }

    private void HandleDefenseEnded(DefenseResult result)
    {
        Debug.Log($"Turn End / attacker:P{attackerPlayerId}, miss:{result.MissCount}");
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
}
