using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 튜토리얼 공격 설명용 노트 예시 UI를 표시한다.
/// 스크립트가 붙은 오브젝트는 항상 켜두고,
/// VisualRoot 자식만 켜고 끄는 방식으로 관리한다.
/// </summary>
public class TutorialNoteExampleView : MonoBehaviour
{
    [Header("Visual Root")]
    [SerializeField] private GameObject visualRoot;

    [Header("Images")]
    [SerializeField] private Image glowImage;
    [SerializeField] private Image noteImage;
    [SerializeField] private Image keyImage;

    [Header("Glow Pulse")]
    [SerializeField, Min(0f)] private float pulseSpeed = 8f;
    [SerializeField, Range(0f, 1f)] private float minGlowAlpha = 0.25f;
    [SerializeField, Range(0f, 1f)] private float maxGlowAlpha = 1f;
    [SerializeField] private bool pulseScale = true;
    [SerializeField] private float minGlowScale = 0.95f;
    [SerializeField] private float maxGlowScale = 1.08f;

    private bool isNoteVisible;
    private bool isGlowVisible;
    private float glowStartTime;

    private void Awake()
    {
        Hide();
    }

    private void Update()
    {
        if (!isNoteVisible || !isGlowVisible || glowImage == null)
        {
            return;
        }

        float elapsed = Mathf.Max(0f, Time.unscaledTime - glowStartTime);
        float wave = (Mathf.Sin(elapsed * pulseSpeed) + 1f) * 0.5f;

        float alpha = Mathf.Lerp(minGlowAlpha, maxGlowAlpha, wave);

        Color glowColor = glowImage.color;
        glowColor.a = alpha;
        glowImage.color = glowColor;

        if (pulseScale)
        {
            float scale = Mathf.Lerp(minGlowScale, maxGlowScale, wave);
            glowImage.rectTransform.localScale = Vector3.one * scale;
        }
    }

    /// <summary>
    /// 노트 본체와 Key 이미지만 표시한다.
    /// Glow는 켜지지 않는다.
    /// </summary>
    public void ShowNote()
    {
        isNoteVisible = true;

        if (visualRoot != null)
        {
            visualRoot.SetActive(true);
        }

        SetImageVisible(noteImage, true);
        SetImageVisible(keyImage, true);
        HideGlow();

        Debug.Log($"TutorialNoteExampleView: ShowNote / object:{name}");
    }

    /// <summary>
    /// 이미 표시된 노트의 Glow를 같은 기준 시간으로 반짝이게 한다.
    /// </summary>
    public void ShowGlowSynced(float startTime)
    {
        if (!isNoteVisible)
        {
            Debug.LogWarning(
                $"TutorialNoteExampleView: ShowGlowSynced 호출됨. " +
                $"하지만 아직 ShowNote가 호출되지 않음. object:{name}"
            );
            return;
        }

        isGlowVisible = true;
        glowStartTime = startTime;

        if (visualRoot != null)
        {
            visualRoot.SetActive(true);
        }

        SetImageVisible(glowImage, true);

        if (glowImage != null)
        {
            glowImage.rectTransform.localScale = Vector3.one;

            Color color = glowImage.color;
            color.a = minGlowAlpha;
            glowImage.color = color;
        }

        Debug.Log($"TutorialNoteExampleView: ShowGlowSynced / object:{name}, start:{startTime:0.000}");
    }

    /// <summary>
    /// Glow만 숨긴다.
    /// </summary>
    public void HideGlow()
    {
        isGlowVisible = false;

        SetImageVisible(glowImage, false);

        if (glowImage != null)
        {
            glowImage.rectTransform.localScale = Vector3.one;
        }
    }

    /// <summary>
    /// 노트 예시 전체를 숨긴다.
    /// </summary>
    public void Hide()
    {
        isNoteVisible = false;
        isGlowVisible = false;

        SetImageVisible(glowImage, false);
        SetImageVisible(noteImage, false);
        SetImageVisible(keyImage, false);

        if (glowImage != null)
        {
            glowImage.rectTransform.localScale = Vector3.one;
        }

        if (visualRoot != null)
        {
            visualRoot.SetActive(false);
        }
    }

    private void SetImageVisible(Image image, bool visible)
    {
        if (image == null)
        {
            return;
        }

        if (image.gameObject != null)
        {
            image.gameObject.SetActive(visible);
        }

        image.enabled = visible;
    }
}