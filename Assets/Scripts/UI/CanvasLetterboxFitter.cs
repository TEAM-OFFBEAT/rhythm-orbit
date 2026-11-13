using UnityEngine;

// Screen Space - Overlay Canvas는 카메라 뷰포트 rect의 영향을 받지 않는다.
// 이 스크립트를 Overlay Canvas 바로 아래의 "루트 패널" RectTransform에 붙이면
// GameCamera.EnforceAspectRatio()가 계산한 letterbox/pillarbox 영역에 맞춰
// anchorMin/anchorMax를 조정해 UI가 게임 뷰포트 안에만 표시되도록 한다.
//
// 사용법:
// 1. Overlay Canvas 하위에 UI 전체를 감싸는 빈 RectTransform(Panel)을 하나 둔다.
//    (Stretch/Stretch, 여백 0으로 캔버스 전체를 채우도록 세팅해둔다.)
// 2. 그 오브젝트에 이 스크립트를 붙인다.
// 3. 실제 UI 요소들은 전부 이 패널의 자식으로 두고 그 안에서 앵커링한다.
[RequireComponent(typeof(RectTransform))]
public class CanvasLetterboxFitter : MonoBehaviour
{
    private RectTransform rectTransform;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    private void OnEnable()
    {
        GameCamera.LetterboxChanged += Apply;
        Apply(GameCamera.CurrentLetterboxRect);
    }

    private void OnDisable()
    {
        GameCamera.LetterboxChanged -= Apply;
    }

    private void Apply(Rect rect)
    {
        if (rect.width <= 0f || rect.height <= 0f)
            return;

        rectTransform.anchorMin = new Vector2(rect.x, rect.y);
        rectTransform.anchorMax = new Vector2(rect.x + rect.width, rect.y + rect.height);
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }
}
