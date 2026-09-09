using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI 이미지, TMP 텍스트, 월드 스프라이트에 간단한 글로우 펄스 효과를 준다.
/// 글로우용 복제 오브젝트에 붙여서 사용한다.
/// </summary>
public class SimpleGlowPulse : MonoBehaviour
{
    [Header("Pulse")]
    [SerializeField] private bool playOnAwake = true;
    [SerializeField, Min(0f)] private float minScale = 1.05f;
    [SerializeField, Min(0f)] private float maxScale = 1.2f;
    [SerializeField, Range(0f, 1f)] private float minAlpha = 0.15f;
    [SerializeField, Range(0f, 1f)] private float maxAlpha = 0.45f;
    [SerializeField, Min(0.01f)] private float pulseSpeed = 2.5f;

    private Vector3 originalScale;
    private bool isPlaying;

    private Graphic[] graphics;
    private SpriteRenderer[] spriteRenderers;
    private TMP_Text[] tmpTexts;

    private void Awake()
    {
        originalScale = transform.localScale;

        graphics = GetComponentsInChildren<Graphic>(true);
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        tmpTexts = GetComponentsInChildren<TMP_Text>(true);

        isPlaying = playOnAwake;
    }

    private void OnEnable()
    {
        if (playOnAwake)
        {
            Play();
        }
    }

    private void OnDisable()
    {
        transform.localScale = originalScale;
    }

    private void Update()
    {
        if (!isPlaying)
        {
            return;
        }

        float t = (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f;

        float scale = Mathf.Lerp(minScale, maxScale, t);
        float alpha = Mathf.Lerp(minAlpha, maxAlpha, t);

        transform.localScale = originalScale * scale;
        SetAlpha(alpha);
    }

    public void Play()
    {
        isPlaying = true;
        gameObject.SetActive(true);
    }

    public void Stop()
    {
        isPlaying = false;
        transform.localScale = originalScale;
        SetAlpha(0f);
    }

    private void SetAlpha(float alpha)
    {
        float safeAlpha = Mathf.Clamp01(alpha);

        foreach (Graphic graphic in graphics)
        {
            if (graphic == null) continue;

            Color color = graphic.color;
            color.a = safeAlpha;
            graphic.color = color;
        }

        foreach (SpriteRenderer spriteRenderer in spriteRenderers)
        {
            if (spriteRenderer == null) continue;

            Color color = spriteRenderer.color;
            color.a = safeAlpha;
            spriteRenderer.color = color;
        }

        foreach (TMP_Text tmpText in tmpTexts)
        {
            if (tmpText == null) continue;

            Color color = tmpText.color;
            color.a = safeAlpha;
            tmpText.color = color;
        }
    }
}