using UnityEngine;

/// <summary>
/// World Space Canvas에서 카메라 이동으로 게임 영역을 패닝.
/// </summary>
public class GameCamera : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AttackTurnRenderer attackTurnRenderer;

    [Header("Movement")]
    [SerializeField] private float lerpSpeed = 4f;

    private float targetX;

    private void Awake()
    {
        targetX = transform.position.x;
    }

    /// <summary>
    /// 공격 턴 시작 시 GameManager에서 호출. 뷰를 공격 그리드 중앙으로 이동.
    /// </summary>
    public void SetAttackView(AttackSide attackerSide)
    {
        if (attackTurnRenderer == null) return;

        float startX = attackTurnRenderer.GetStartX(attackerSide);
        float endX = attackTurnRenderer.GetEndX(attackerSide);
        targetX = (startX + endX) * 0.5f;
    }

    /// <summary>
    /// 방어 전환 시 GameManager에서 호출. 뷰를 그리드 중앙과 판정선 사이 지점으로 이동.
    /// </summary>
    public void SetDefenseView(AttackSide attackerSide)
    {
        if (attackTurnRenderer == null) return;

        float startX = attackTurnRenderer.GetStartX(attackerSide);
        float endX = attackTurnRenderer.GetEndX(attackerSide);
        float gridCenterX = (startX + endX) * 0.5f;
        float judgeLineX = attackTurnRenderer.GetJudgeLineX(attackerSide);
        targetX = (gridCenterX + judgeLineX) * 0.5f;
    }

    private void Update()
    {
        // 카메라 이동은 시각 효과이므로 Time.deltaTime 사용 (게임 타이밍 로직 아님)
        Vector3 pos = transform.position;
        pos.x = Mathf.Lerp(pos.x, targetX, lerpSpeed * Time.deltaTime);
        transform.position = pos;
    }
}
