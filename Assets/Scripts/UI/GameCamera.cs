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
        EnforceAspectRatio(GetComponent<Camera>());
    }

    /// <summary>
    /// 16:9 비율을 강제한다. 비율이 다른 화면에서는 letterbox/pillarbox를 적용한다.
    /// </summary>
    public static void EnforceAspectRatio(Camera cam, float target = 16f / 9f)
    {
        float current = (float)Screen.width / Screen.height;
        float scale = current / target;

        // scale < 1: 화면이 target보다 좁음 → 레터박스(상하 바), 뷰포트 = 전체 너비 × scale 높이
        // scale > 1: 화면이 target보다 넓음 → 필라박스(좌우 바), 뷰포트 = (1/scale) 너비 × 전체 높이
        // 두 경우 모두 camera.aspect = target(16:9)으로 유지됨
        bool isLetterbox = scale < 1f;
        if (isLetterbox)
            cam.rect = new Rect(0f, (1f - scale) / 2f, 1f, scale);
        else
        {
            float w = 1f / scale;
            cam.rect = new Rect((1f - w) / 2f, 0f, w, 1f);
        }

        // CanvasScaler가 Screen 전체 크기를 기준으로 스케일을 계산하므로,
        // 레터박스(상하 바)이면 너비 기준, 필라박스(좌우 바)이면 높이 기준으로 맞춰야 Canvas가 왜곡되지 않음
        foreach (var scaler in UnityEngine.Object.FindObjectsOfType<UnityEngine.UI.CanvasScaler>())
            scaler.matchWidthOrHeight = isLetterbox ? 0f : 1f;
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
    /// 라운드 인트로 시 GameManager에서 호출. 뷰를 화면 중앙으로 복귀.
    /// </summary>
    public void SetCenterView()
    {
        targetX = 0f;
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
