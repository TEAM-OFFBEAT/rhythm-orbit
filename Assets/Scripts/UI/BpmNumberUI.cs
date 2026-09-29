using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// BPM 값을 3자리 오도미터 스타일 숫자 스프라이트로 표시한다.
/// BPM 변경 시 전체 BPM을 1씩 증가/감소시키며 자릿수별 독립 속도로 스크롤된다.
/// </summary>
public class BpmNumberUI : MonoBehaviour
{
    [Header("Digit Sprites (index = digit 0~9)")]
    [SerializeField] private Sprite[] digitSprites;

    [Header("Digit Slots (0=hundreds, 1=tens, 2=ones)")]
    [SerializeField] private RectTransform[] digitContainers;

    [Header("Animation")]
    [SerializeField] private float minStepDuration = 0.03f;
    [SerializeField] private float maxStepDuration = 0.12f;
    // 각 자릿수 스크롤 지속 시간 배율 (hundreds, tens, ones)
    [SerializeField] private float[] slotDurationMultipliers = { 9f, 5f, 1f };

    private Image[] curImgs = new Image[3];
    private Image[] nxtImgs = new Image[3];
    private Coroutine[] digitCoroutines = new Coroutine[3];
    private int currentBpm = -1;
    private Coroutine rollCoroutine;

    private void EnsureInit()
    {
        if (curImgs[0] != null) return;
        for (int i = 0; i < 3; i++)
        {
            curImgs[i] = digitContainers[i].GetChild(0).GetComponent<Image>();
            nxtImgs[i] = digitContainers[i].GetChild(1).GetComponent<Image>();
            curImgs[i].preserveAspect = false;
            curImgs[i].color = Color.clear;
            nxtImgs[i].preserveAspect = false;
            nxtImgs[i].color = Color.clear;
            nxtImgs[i].rectTransform.anchoredPosition = new Vector2(0f, -9999f);
        }
    }

    /// <summary>
    /// BPM 값을 설정한다. 첫 호출은 즉시 표시, 이후 호출은 오도미터 애니메이션.
    /// </summary>
    public void SetBpm(int bpm)
    {
        EnsureInit();
        if (currentBpm < 0) { SnapTo(bpm); return; }
        if (rollCoroutine != null) StopCoroutine(rollCoroutine);
        rollCoroutine = StartCoroutine(DoRoll(currentBpm, bpm));
    }

    /// <summary>float BPM을 정수로 변환해 SetBpm(int)에 위임한다.</summary>
    public void SetBpm(float bpm) => SetBpm(Mathf.RoundToInt(bpm));

    /// <summary>현재 BPM에서 목표 BPM까지 카운팅 애니메이션 총 시간을 반환한다.</summary>
    public float GetAnimationDuration(float targetBpm)
    {
        int from = currentBpm < 0 ? Mathf.RoundToInt(targetBpm) : currentBpm;
        int to = Mathf.RoundToInt(targetBpm);
        int steps = Mathf.Abs(to - from);
        if (steps <= 1) return 0f;
        float total = 0f;
        for (int s = 0; s < steps; s++)
        {
            float t = (float)s / (steps - 1);
            total += Mathf.Lerp(minStepDuration, maxStepDuration, t * t);
        }
        return total;
    }

    private void SnapTo(int bpm)
    {
        currentBpm = bpm;
        for (int i = 0; i < 3; i++)
        {
            curImgs[i].sprite = digitSprites[Digit(bpm, i)];
            curImgs[i].color = Color.white;
            curImgs[i].rectTransform.anchoredPosition = Vector2.zero;
        }
    }

    private IEnumerator DoRoll(int from, int to)
    {
        int dir = to > from ? 1 : -1;
        int steps = Mathf.Abs(to - from);

        for (int s = 0; s < steps; s++)
        {
            int a = from + dir * s, b = a + dir;
            float t = steps > 1 ? (float)s / (steps - 1) : 1f;
            float stepDur = Mathf.Lerp(minStepDuration, maxStepDuration, t * t);

            for (int i = 0; i < 3; i++)
            {
                if (Digit(a, i) != Digit(b, i))
                {
                    float slotDur = stepDur * slotDurationMultipliers[i];
                    if (digitCoroutines[i] != null) StopCoroutine(digitCoroutines[i]);
                    int slot = i, fromD = Digit(a, i), toD = Digit(b, i);
                    digitCoroutines[slot] = StartCoroutine(DoDigitRoll(slot, fromD, toD, slotDur));
                }
            }

            currentBpm = b;
            yield return new WaitForSeconds(stepDur);
        }

        rollCoroutine = null;
    }

    private IEnumerator DoDigitRoll(int slot, int fromDigit, int toDigit, float duration)
    {
        float h = digitContainers[slot].rect.height;
        curImgs[slot].sprite = digitSprites[fromDigit];
        curImgs[slot].color = Color.white;
        nxtImgs[slot].sprite = digitSprites[toDigit];
        nxtImgs[slot].color = Color.white;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            curImgs[slot].rectTransform.anchoredPosition = new Vector2(0f, t * h);
            nxtImgs[slot].rectTransform.anchoredPosition = new Vector2(0f, t * h - h);
            elapsed += Time.deltaTime;
            yield return null;
        }

        curImgs[slot].sprite = digitSprites[toDigit];
        curImgs[slot].rectTransform.anchoredPosition = Vector2.zero;
        nxtImgs[slot].rectTransform.anchoredPosition = new Vector2(0f, -(h + 10f));
        digitCoroutines[slot] = null;
    }

    private static int Digit(int bpm, int slot) =>
        slot == 0 ? bpm / 100 : slot == 1 ? (bpm / 10) % 10 : bpm % 10;
}
