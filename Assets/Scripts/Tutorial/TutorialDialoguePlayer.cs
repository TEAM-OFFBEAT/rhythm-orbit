using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 튜토리얼 안내/대사/반응 패널만 담당한다.
/// 공격/방어 턴 진행은 TutorialManager가 담당한다.
/// </summary>
public class TutorialDialoguePlayer : MonoBehaviour
{
    [Header("Guide UI")]
    [SerializeField] private GameObject guidePanel;
    [SerializeField] private TMP_Text guideText;

    [Tooltip("리모 프로필로 사용할 Image. 기존 플레이어 패널의 프로필 Image를 연결한다.")]
    [SerializeField] private Image rymoPortraitImage;

    [Header("Highlight UI")]
    [SerializeField] private GameObject starHighlightRoot;
    [SerializeField] private GameObject attackJudgeLineHighlightRoot;
    [SerializeField] private GameObject defenseJudgeLineHighlightRoot;
    [SerializeField] private GameObject highBitHighlightRoot;
    [SerializeField] private GameObject lowBitHighlightRoot;

    [Header("Typewriter")]
    [SerializeField] private TypewriterText typewriterText;
    [SerializeField] private bool useTypewriter = true;

    [Tooltip("문장 타이핑이 끝난 뒤 다음 문장으로 넘어가기 전에 유지할 시간.")]
    [SerializeField, Min(0f)] private float holdSecondsAfterTyping = 0.6f;

    [Tooltip("문장이 너무 길어도 최소한 이 시간 이상은 타이핑에 사용한다.")]
    [SerializeField, Min(0.01f)] private float minimumTypingSeconds = 0.15f;

    [Header("Timing")]
    [SerializeField] private float fallbackBpm = 90f;
    [SerializeField] private bool hideOnAwake = true;

    [Header("Line Blink")]
    [SerializeField] private bool useBlinkBetweenLines = true;

    [Tooltip("다음 문장으로 넘어가기 직전 깜빡이는 시간. 0.08~0.15초 추천.")]
    [SerializeField, Min(0f)] private float blinkSecondsBetweenLines = 0.1f;

    [Tooltip("깜빡임 동안 패널 전체를 숨길지 여부. 꺼두면 글자만 사라진다.")]
    [SerializeField] private bool hidePanelDuringBlink = false;

    [Header("Dialogue Timing")]
    [SerializeField, Min(0f)] private float dialogueVisualLeadSeconds = 0.04f;

    private float currentBpm;
    private Coroutine temporaryMessageCoroutine;

    private void Awake()
    {
        currentBpm = fallbackBpm;

        if (typewriterText == null && guideText != null)
        {
            typewriterText = guideText.GetComponent<TypewriterText>();
        }

        ClearHighlights();

        if (hideOnAwake)
        {
            Hide();
        }
    }

    public void SetBpm(float bpm)
    {
        if (bpm <= 0f)
        {
            Debug.LogWarning("TutorialDialoguePlayer: BPM은 0보다 커야 함.");
            return;
        }

        currentBpm = bpm;
    }

    /// <summary>
    /// 리모 프로필 이미지를 즉시 변경한다.
    /// </summary>
    public void SetRymoPortrait(Sprite portrait)
    {
        if (rymoPortraitImage == null || portrait == null)
        {
            return;
        }

        rymoPortraitImage.sprite = portrait;
    }

    public void Show(string text)
    {
        ClearHighlights();
        ShowInstant(text);
    }

    private void ShowInstant(string text)
    {
        if (guidePanel == null)
        {
            Debug.LogWarning("TutorialDialoguePlayer: guidePanel이 연결되지 않음.");
            return;
        }

        if (guideText == null)
        {
            Debug.LogWarning("TutorialDialoguePlayer: guideText가 연결되지 않음.");
            return;
        }

        guidePanel.SetActive(true);
        guideText.gameObject.SetActive(true);

        if (typewriterText != null)
        {
            typewriterText.SetInstant(text);
        }
        else
        {
            guideText.text = text;
            guideText.maxVisibleCharacters = int.MaxValue;
        }
    }

    private void ShowTyped(string text, float durationSeconds)
    {
        if (guidePanel == null)
        {
            Debug.LogWarning("TutorialDialoguePlayer: guidePanel이 연결되지 않음.");
            return;
        }

        if (guideText == null)
        {
            Debug.LogWarning("TutorialDialoguePlayer: guideText가 연결되지 않음.");
            return;
        }

        guidePanel.SetActive(true);
        guideText.gameObject.SetActive(true);

        if (useTypewriter && typewriterText != null)
        {
            typewriterText.Play(text, durationSeconds);
        }
        else
        {
            ShowInstant(text);
        }
    }

    public void Hide()
    {
        if (temporaryMessageCoroutine != null)
        {
            StopCoroutine(temporaryMessageCoroutine);
            temporaryMessageCoroutine = null;
        }

        if (typewriterText != null)
        {
            typewriterText.Stop();
        }

        ClearHighlights();

        if (guidePanel != null)
        {
            guidePanel.SetActive(false);
        }
    }

    /// <summary>
    /// 기존 string[] 대사용. 임시 호환용으로 남겨둔다.
    /// </summary>
    public IEnumerator PlayLines(
        string[] lines,
        int beatsPerLine,
        bool hideWhenFinished = true,
        Action<int, string> onLineStarted = null,
        double? forcedStartDspTime = null
    )
    {
        if (lines == null || lines.Length == 0)
        {
            yield break;
        }

        StopTemporaryMessage();

        int safeBeats = Mathf.Max(1, beatsPerLine);
        float beatSeconds = GetBeatSeconds();
        float lineSeconds = beatSeconds * safeBeats;

        double dialogueStartDspTime = forcedStartDspTime ?? AudioSettings.dspTime;

        for (int i = 0; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                continue;
            }

            double lineStartDspTime = dialogueStartDspTime + i * lineSeconds;
            double nextLineStartDspTime = dialogueStartDspTime + (i + 1) * lineSeconds;
            double visualShowDspTime = lineStartDspTime - dialogueVisualLeadSeconds;

            bool hasNextLine = i < lines.Length - 1;

            float blinkSeconds = useBlinkBetweenLines && hasNextLine
                ? Mathf.Clamp(blinkSecondsBetweenLines, 0f, lineSeconds * 0.5f)
                : 0f;

            float totalVisibleSeconds = lineSeconds + dialogueVisualLeadSeconds;
            float typingSeconds = CalculateTypingSeconds(totalVisibleSeconds, blinkSeconds);

            yield return WaitUntilDspTime(visualShowDspTime);

            ClearHighlights();
            ShowTyped(lines[i], typingSeconds);
            onLineStarted?.Invoke(i, lines[i]);

            double blinkStartDspTime = nextLineStartDspTime - blinkSeconds;

            yield return WaitUntilDspTime(blinkStartDspTime);

            if (blinkSeconds > 0f)
            {
                ShowBlinkBlank();
            }

            yield return WaitUntilDspTime(nextLineStartDspTime);
        }

        ClearHighlights();

        if (hideWhenFinished)
        {
            Hide();
        }
    }

    /// <summary>
    /// TutorialGuideLineData[] 대사용.
    /// 대사 텍스트, 리모 프로필, 강조 연출을 줄 단위로 함께 적용한다.
    /// </summary>
    public IEnumerator PlayLines(
        TutorialGuideLineData[] lines,
        int beatsPerLine,
        bool hideWhenFinished = true,
        Action<int, TutorialGuideLineData> onLineStarted = null,
        double? forcedStartDspTime = null
    )
    {
        if (lines == null || lines.Length == 0)
        {
            yield break;
        }

        StopTemporaryMessage();

        int safeBeats = Mathf.Max(1, beatsPerLine);
        float beatSeconds = GetBeatSeconds();
        float lineSeconds = beatSeconds * safeBeats;

        double dialogueStartDspTime = forcedStartDspTime ?? AudioSettings.dspTime;

        for (int i = 0; i < lines.Length; i++)
        {
            TutorialGuideLineData line = lines[i];

            if (line == null || string.IsNullOrWhiteSpace(line.text))
            {
                continue;
            }

            double lineStartDspTime = dialogueStartDspTime + i * lineSeconds;
            double nextLineStartDspTime = dialogueStartDspTime + (i + 1) * lineSeconds;
            double visualShowDspTime = lineStartDspTime - dialogueVisualLeadSeconds;

            bool hasNextLine = i < lines.Length - 1;

            float blinkSeconds = useBlinkBetweenLines && hasNextLine
                ? Mathf.Clamp(blinkSecondsBetweenLines, 0f, lineSeconds * 0.5f)
                : 0f;

            float totalVisibleSeconds = lineSeconds + dialogueVisualLeadSeconds;
            float typingSeconds = CalculateTypingSeconds(totalVisibleSeconds, blinkSeconds);

            yield return WaitUntilDspTime(visualShowDspTime);

            ApplyLineVisual(line);
            ShowTyped(line.text, typingSeconds);
            onLineStarted?.Invoke(i, line);

            double blinkStartDspTime = nextLineStartDspTime - blinkSeconds;

            yield return WaitUntilDspTime(blinkStartDspTime);

            if (blinkSeconds > 0f)
            {
                ShowBlinkBlank();
            }

            yield return WaitUntilDspTime(nextLineStartDspTime);
        }

        ClearHighlights();

        if (hideWhenFinished)
        {
            Hide();
        }
    }

    private void ApplyLineVisual(TutorialGuideLineData line)
    {
        ClearHighlights();

        if (line == null)
        {
            return;
        }

        SetRymoPortrait(line.rymoPortrait);

        SetActiveSafe(starHighlightRoot, line.highlightStars);
        SetActiveSafe(attackJudgeLineHighlightRoot, line.highlightAttackJudgeLine);
        SetActiveSafe(defenseJudgeLineHighlightRoot, line.highlightDefenseJudgeLine);
        SetActiveSafe(highBitHighlightRoot, line.highlightHighBit);
        SetActiveSafe(lowBitHighlightRoot, line.highlightLowBit);
    }

    public void ClearHighlights()
    {
        SetActiveSafe(starHighlightRoot, false);
        SetActiveSafe(attackJudgeLineHighlightRoot, false);
        SetActiveSafe(defenseJudgeLineHighlightRoot, false);
        SetActiveSafe(highBitHighlightRoot, false);
        SetActiveSafe(lowBitHighlightRoot, false);
    }

    private void SetActiveSafe(GameObject target, bool active)
    {
        if (target != null)
        {
            target.SetActive(active);
        }
    }

    public void ShowReactionForBeats(string text, int beats, bool hideWhenFinished = true)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        float seconds = GetBeatSeconds() * Mathf.Max(1, beats);
        ShowTemporary(text, seconds, hideWhenFinished);
    }

    public void ShowTemporary(string text, float seconds, bool hideWhenFinished = true)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        StopTemporaryMessage();

        temporaryMessageCoroutine = StartCoroutine(
            ShowTemporaryRoutine(text, seconds, hideWhenFinished)
        );
    }

    private IEnumerator ShowTemporaryRoutine(string text, float seconds, bool hideWhenFinished)
    {
        Debug.Log($"Tutorial Reaction: {text}");

        ClearHighlights();

        float safeSeconds = Mathf.Max(0.1f, seconds);
        float typingSeconds = CalculateTypingSeconds(safeSeconds, blinkSeconds: 0f);

        ShowTyped(text, typingSeconds);

        yield return new WaitForSecondsRealtime(safeSeconds);

        if (hideWhenFinished)
        {
            if (guidePanel != null)
            {
                guidePanel.SetActive(false);
            }
        }

        temporaryMessageCoroutine = null;
    }

    private void StopTemporaryMessage()
    {
        if (temporaryMessageCoroutine != null)
        {
            StopCoroutine(temporaryMessageCoroutine);
            temporaryMessageCoroutine = null;
        }

        if (typewriterText != null)
        {
            typewriterText.Stop();
        }
    }

    private float GetBeatSeconds()
    {
        if (RhythmClock.Instance != null)
        {
            return (float)RhythmClock.Instance.GetBeatDuration();
        }

        return 60f / Mathf.Max(1f, currentBpm);
    }

    private float CalculateTypingSeconds(float totalVisibleSeconds, float blinkSeconds)
    {
        float availableSeconds = Mathf.Max(0.05f, totalVisibleSeconds - blinkSeconds);

        float safeMinimumTypingSeconds = Mathf.Min(
            Mathf.Max(0.01f, minimumTypingSeconds),
            availableSeconds
        );

        float maxHoldSeconds = Mathf.Max(0f, availableSeconds - safeMinimumTypingSeconds);
        float safeHoldSeconds = Mathf.Min(holdSecondsAfterTyping, maxHoldSeconds);

        return Mathf.Max(
            safeMinimumTypingSeconds,
            availableSeconds - safeHoldSeconds
        );
    }

    private void ShowBlinkBlank()
    {
        if (typewriterText != null)
        {
            typewriterText.Stop();
        }

        ClearHighlights();

        if (hidePanelDuringBlink)
        {
            if (guidePanel != null)
            {
                guidePanel.SetActive(false);
            }

            return;
        }

        if (guidePanel != null)
        {
            guidePanel.SetActive(true);
        }

        if (guideText != null)
        {
            guideText.text = string.Empty;
            guideText.maxVisibleCharacters = 0;
            guideText.gameObject.SetActive(false);
        }
    }

    private IEnumerator WaitUntilDspTime(double targetDspTime)
    {
        while (AudioSettings.dspTime < targetDspTime)
        {
            yield return null;
        }
    }
}