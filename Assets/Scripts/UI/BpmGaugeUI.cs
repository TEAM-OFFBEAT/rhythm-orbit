using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// BPM 값을 fill 게이지로 시각화한다.
/// 라운드 전환 시 8박 ease-out fill 애니메이션을 제공한다.
/// SetBpm 호출 이후에만 pulse 효과가 활성화된다.
/// </summary>
public class BpmGaugeUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image fillImage;

    [Header("BPM Range")]
    [SerializeField] private float minBpm = 100f;
    [SerializeField] private float maxBpm = 144f;

    [Header("Fill Animation")]
    [SerializeField] private float animBeats = 8f;

    [Header("Pulse Effect")]
    [SerializeField] private float pulseAmplitude = 2f;
    [SerializeField] private float pulseFrequency = 5f;

    private float currentFill;
    private float currentBpm;
    private bool pulseActive;
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

        if (!pulseActive)
        {
            fillImage.fillAmount = currentFill;
            return;
        }

        float barWidth = fillImage.rectTransform.rect.width;
        if (barWidth <= 0f) return;

        float pulseOffset = Mathf.Sin(Time.time * pulseFrequency * Mathf.PI * 2f) * (pulseAmplitude / barWidth);
        fillImage.fillAmount = Mathf.Clamp01(currentFill + pulseOffset);
    }

    /// <summary>
    /// 지정한 BPM에 맞는 fill 값으로 8박 ease-out 애니메이션을 시작한다.
    /// 처음 호출 시 pulse가 활성화된다.
    /// minBpm 이하면 0, maxBpm 이상이면 1로 클램프된다.
    /// </summary>
    public void SetBpm(float bpm)
    {
        currentBpm = bpm;
        pulseActive = true;

        float target = Mathf.InverseLerp(minBpm, maxBpm, bpm);
        if (animCoroutine != null) StopCoroutine(animCoroutine);
        animCoroutine = StartCoroutine(AnimateFill(target));
    }

    private IEnumerator AnimateFill(float target)
    {
        float start = currentFill;
        float duration = animBeats * 60f / currentBpm;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float tEased = 1f - Mathf.Pow(1f - t, 3f);
            currentFill = Mathf.Lerp(start, target, tEased);
            yield return null;
        }

        currentFill = target;
        animCoroutine = null;
    }
}
