using UnityEngine;

/// <summary>
/// World Space Canvas에서 카메라 이동으로 게임 영역을 패닝.
/// </summary>
public class GameCamera : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AttackTurnRenderer attackTurnRenderer;

    [Header("Movement")]
    [SerializeField] private float smoothTime = 0.15f;

    private float targetX;
    private float velocityX;
    private int lastScreenWidth;
    private int lastScreenHeight;

    public static event System.Action<Rect> LetterboxChanged;
    public static Rect CurrentLetterboxRect { get; private set; } = new Rect(0f, 0f, 1f, 1f);

    private static GameObject letterboxOverlay;
    private static float lastOverlayScale = -1f;

    private void Awake()
    {
        targetX = transform.position.x;
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
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

        CurrentLetterboxRect = cam.rect;
        LetterboxChanged?.Invoke(cam.rect);

        // CanvasScaler가 Screen 전체 크기를 기준으로 스케일을 계산하므로,
        // 레터박스(상하 바)이면 너비 기준, 필라박스(좌우 바)이면 높이 기준으로 맞춰야 Canvas가 왜곡되지 않음
        foreach (var scaler in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.CanvasScaler>())
            scaler.matchWidthOrHeight = isLetterbox ? 0f : 1f;

        UpdateLetterboxOverlay(scale, isLetterbox);
    }

    // cam.rect는 뷰포트 밖 영역을 클리어하지 않아 씬 전환 후 이전 프레임버퍼가 남는다.
    // DontDestroyOnLoad ScreenSpaceOverlay로 바 영역을 항상 덮어 이 문제를 해결한다.
    // bar 오브젝트를 Destroy/재생성하면 1프레임 공백이 생길 수 있으므로
    // 4개를 고정으로 유지하고 anchor만 갱신한다.
    private static void UpdateLetterboxOverlay(float scale, bool isLetterbox)
    {
        if (letterboxOverlay != null && Mathf.Abs(scale - lastOverlayScale) < 0.001f) return;
        lastOverlayScale = scale;

        if (letterboxOverlay == null)
        {
            letterboxOverlay = new GameObject("LetterboxOverlay");
            var canvas = letterboxOverlay.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue - 1;
            UnityEngine.Object.DontDestroyOnLoad(letterboxOverlay);

            for (int i = 0; i < 4; i++) // top, bottom, left, right
            {
                var bar = new GameObject("Bar", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                bar.transform.SetParent(letterboxOverlay.transform, false);
                var img = bar.GetComponent<UnityEngine.UI.Image>();
                img.color = Color.black;
                img.raycastTarget = false;
            }
        }

        var t = letterboxOverlay.transform;
        bool needsBars = Mathf.Abs(scale - 1f) >= 0.001f;
        float barFrac = needsBars ? (isLetterbox ? (1f - scale) / 2f : (1f - 1f / scale) / 2f) : 0f;
        float tb = isLetterbox ? barFrac : 0f;   // 상하 바 높이 비율
        float lr = isLetterbox ? 0f : barFrac;   // 좌우 바 너비 비율

        // letterbox: 상하 바 활성, 좌우 비활성 / pillarbox: 좌우 바 활성, 상하 비활성
        SetBar(t.GetChild(0), new Vector2(0f, 1f - tb), Vector2.one);        // top
        SetBar(t.GetChild(1), Vector2.zero, new Vector2(1f, tb));             // bottom
        SetBar(t.GetChild(2), Vector2.zero, new Vector2(lr, 1f));             // left
        SetBar(t.GetChild(3), new Vector2(1f - lr, 0f), Vector2.one);        // right
    }

    private static void SetBar(Transform bar, Vector2 anchorMin, Vector2 anchorMax)
    {
        var rect = (RectTransform)bar;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
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

    private void LateUpdate()
    {
        if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
        {
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            EnforceAspectRatio(GetComponent<Camera>());
        }

        Vector3 pos = transform.position;
        pos.x = Mathf.SmoothDamp(pos.x, targetX, ref velocityX, smoothTime);
        transform.position = pos;
    }

    /// <summary>
    /// 중앙 복귀를 마무리하고 이동 관성을 초기화한다.
    /// </summary>
    public void SnapToCenterView()
    {
        targetX = 0f;
        velocityX = 0f;

        Vector3 pos = transform.position;
        pos.x = 0f;
        transform.position = pos;
    }

    
}
