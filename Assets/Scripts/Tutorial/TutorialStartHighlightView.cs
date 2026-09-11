using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 튜토리얼 별 강조 UI를 담당한다.
/// 별 이미지는 고정하고, 테두리 하이라이트 이미지만 밝기 변화 효과를 준다.
/// </summary>
public class TutorialStarHighlightView : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject root;

    [Header("Images")]
    [SerializeField] private Image starImage;
    [SerializeField] private Image outlineHighlightImage;

    [Header("Highlight Pulse")]
    [SerializeField] private bool pulseHighlight = true;
    [SerializeField] private Color highlightColor = Color.white;
    [SerializeField, Range(0f, 1f)] private float minHighlightAlpha = 0.25f;
    [SerializeField, Range(0f, 1f)] private float maxHighlightAlpha = 0.85f;
    [SerializeField, Min(0.01f)] private float pulseSpeed = 2.5f;

    private bool isVisible;
    private float pulseTime;
    private Color originalOutlineColor;

    private void Awake()
    {
        if (root == null)
        {
            root = gameObject;
        }

        if (outlineHighlightImage != null)
        {
            originalOutlineColor = outlineHighlightImage.color;
        }

        SetRaycastTargetOff();
    }

    private void Update()
    {
        if (!isVisible)
        {
            return;
        }

        if (!pulseHighlight)
        {
            return;
        }

        if (outlineHighlightImage == null)
        {
            return;
        }

        pulseTime += Time.unscaledDeltaTime;

        float t = (Mathf.Sin(pulseTime * pulseSpeed) + 1f) * 0.5f;
        float alpha = Mathf.Lerp(minHighlightAlpha, maxHighlightAlpha, t);

        SetOutlineAlpha(alpha);

        Color color = originalOutlineColor;
        color.a = alpha;
        outlineHighlightImage.color = color;
    }

    /// <summary>
    /// 별 강조 UI를 켜거나 끈다.
    /// </summary>
    public void SetVisible(bool visible)
    {
        isVisible = visible;

        if (root != null)
        {
            root.SetActive(visible);
        }

        if (starImage != null)
        {
            starImage.gameObject.SetActive(visible);
        }

        if (outlineHighlightImage != null)
        {
            outlineHighlightImage.gameObject.SetActive(visible);
        }

        if (visible)
        {
            pulseTime = 0f;
            SetOutlineAlpha(maxHighlightAlpha);
        }
        else
        {
            SetOutlineAlpha(0f);
        }
    }

    private void SetOutlineAlpha(float alpha)
    {
        if (outlineHighlightImage == null)
        {
            return;
        }

        Color color = highlightColor;
        color.a = Mathf.Clamp01(alpha);
        outlineHighlightImage.color = color;
    }

    private void SetRaycastTargetOff()
    {
        if (starImage != null)
        {
            starImage.raycastTarget = false;
        }

        if (outlineHighlightImage != null)
        {
            outlineHighlightImage.raycastTarget = false;
        }
    }
}