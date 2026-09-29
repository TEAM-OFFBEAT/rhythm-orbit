using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// BPM 값을 fill 게이지로 시각화한다.
/// 라운드 전환 시 ease-out fill 애니메이션을 제공한다.
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
    [SerializeField] public float animDuration = 1f;

    [Header("Pulse Effect (끝점 흔들림)")]
    [SerializeField] private float pulseAmplitude = 0.015f;
    [SerializeField] private float pulseSpeed = 15f;

    [Header("Arc Fill Settings")]
    [SerializeField] private float arcStartOffset = 0f;    // Left(9시) 기준 아크 시작점까지 CCW 비율 (0~1)
    [SerializeField] private float arcSpan = 0.38f;        // 아크 전체 스팬 비율 (0~1)

    private float currentFill = 0f;
    private float targetFill = 0f;
    private bool pulseActive = false;
    private Coroutine animCoroutine;

    private void Awake()
    {
        if (fillImage != null)
        {
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Radial360;
            fillImage.fillAmount = 0f;
        }
    }

    private void Update()
    {
        if (fillImage == null) return;

        float pulseOffset = pulseActive ? Mathf.Sin(Time.time * pulseSpeed) * pulseAmplitude : 0f;
        float normalizedFill = Mathf.Clamp01(currentFill + pulseOffset);
        fillImage.fillAmount = arcStartOffset + normalizedFill * arcSpan;
    }

    /// <summary>
    /// 지정한 BPM에 맞는 fill 값으로 ease-out 애니메이션을 시작한다.
    /// 처음 호출 시 pulse가 활성화된다.
    /// minBpm 이하면 0, maxBpm 이상이면 1로 클램프된다.
    /// </summary>
    public void SetBpm(float bpm)
    {
        targetFill = Mathf.InverseLerp(minBpm, maxBpm, bpm);
        pulseActive = true;

        if (animCoroutine != null) StopCoroutine(animCoroutine);
        if (!gameObject.activeInHierarchy) { currentFill = targetFill; return; }
        animCoroutine = StartCoroutine(AnimateFill());
    }

    private IEnumerator AnimateFill()
    {
        float start = currentFill;
        float elapsed = 0f;

        while (elapsed < animDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / animDuration);
            currentFill = Mathf.Lerp(start, targetFill, 1f - Mathf.Pow(1f - t, 3f));
            yield return null;
        }

        currentFill = targetFill;
        animCoroutine = null;
    }
}
