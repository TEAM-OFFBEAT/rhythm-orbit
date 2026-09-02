using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// TMP_Text에 타이핑 효과를 적용하는 공용 컴포넌트.
/// 튜토리얼 대사뿐 아니라 HUD, 결과 패널 등 다른 텍스트에도 재사용할 수 있다.
/// </summary>
public class TypewriterText : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private TMP_Text targetText;

    [Header("Settings")]
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField, Min(0f)] private float minimumDuration = 0.05f;

    private Coroutine typingCoroutine;

    private void Awake()
    {
        if (targetText == null)
        {
            targetText = GetComponent<TMP_Text>();
        }
    }

    /// <summary>
    /// 지정한 시간 안에 text 전체가 보이도록 타이핑 효과를 재생한다.
    /// durationSeconds가 길면 천천히, 짧으면 빠르게 출력된다.
    /// </summary>
    public void Play(string text, float durationSeconds)
    {
        if (targetText == null)
        {
            return;
        }

        Stop();

        typingCoroutine = StartCoroutine(
            PlayRoutine(text ?? string.Empty, Mathf.Max(minimumDuration, durationSeconds))
        );
    }

    /// <summary>
    /// 타이핑 효과 없이 즉시 전체 문장을 표시한다.
    /// </summary>
    public void SetInstant(string text)
    {
        if (targetText == null)
        {
            return;
        }

        Stop();

        targetText.text = text ?? string.Empty;
        targetText.ForceMeshUpdate();
        targetText.maxVisibleCharacters = targetText.textInfo.characterCount;
    }

    /// <summary>
    /// 진행 중인 타이핑 효과를 중단한다.
    /// </summary>
    public void Stop()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }
    }

    /// <summary>
    /// 텍스트를 비운다.
    /// </summary>
    public void Clear()
    {
        Stop();

        if (targetText == null)
        {
            return;
        }

        targetText.text = string.Empty;
        targetText.maxVisibleCharacters = 0;
    }

    private IEnumerator PlayRoutine(string text, float durationSeconds)
    {
        targetText.text = text;
        targetText.ForceMeshUpdate();

        int characterCount = targetText.textInfo.characterCount;

        if (characterCount <= 0)
        {
            targetText.maxVisibleCharacters = 0;
            typingCoroutine = null;
            yield break;
        }

        targetText.maxVisibleCharacters = 0;

        float elapsed = 0f;

        while (elapsed < durationSeconds)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

            float t = Mathf.Clamp01(elapsed / durationSeconds);
            int visibleCount = Mathf.CeilToInt(characterCount * t);

            targetText.maxVisibleCharacters = visibleCount;

            yield return null;
        }

        targetText.maxVisibleCharacters = characterCount;
        typingCoroutine = null;
    }
}