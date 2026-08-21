using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// BPM 값을 fill 게이지로 시각화한다.
/// 라운드 전환 시 fill 애니메이션, 진행 중 끝단 맥동 효과를 제공한다.
/// </summary>
public class BpmGaugeUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image fillImage;

    [Header("BPM Range")]
    [SerializeField] private float minBpm = 100f;
    [SerializeField] private float maxBpm = 144f;

    [Header("Fill Animation")]
    [SerializeField] private float animDuration = 0.6f;

    [Header("Pulse Effect")]
    [SerializeField] private float pulseAmplitude = 6f;
    [SerializeField] private float pulseFrequency = 5f;

    private float currentFill;
    private Coroutine animCoroutine;

    private void Awake()
    {
        currentFill = 0f;
        if (fillImage != null)
            fillImage.fillAmount = 0f;
    }

    private void Update()
    {
        if (fillImage == null) return;

        float barWidth = fillImage.rectTransform.rect.width;
        if (barWidth <= 0f) return;

        float pulseOffset = Mathf.Sin(Time.time * pulseFrequency * Mathf.PI * 2f) * (pulseAmplitude / barWidth);
        fillImage.fillAmount = Mathf.Clamp01(currentFill + pulseOffset);
    }

    /// <summary>
    /// 지정한 BPM에 맞는 fill 값으로 애니메이션을 시작한다.
    /// minBpm 이하면 0, maxBpm 이상이면 1로 클램프된다.
    /// </summary>
    public void SetBpm(float bpm)
    {
        float target = Mathf.InverseLerp(minBpm, maxBpm, bpm);

        if (animCoroutine != null)
            StopCoroutine(animCoroutine);
        animCoroutine = StartCoroutine(AnimateFill(target));
    }

    private IEnumerator AnimateFill(float target)
    {
        float start = currentFill;
        float elapsed = 0f;

        while (elapsed < animDuration)
        {
            elapsed += Time.deltaTime;
            currentFill = Mathf.Lerp(start, target, elapsed / animDuration);
            yield return null;
        }

        currentFill = target;
        animCoroutine = null;
    }
}
