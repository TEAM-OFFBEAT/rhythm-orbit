using System.Collections;
using TMPro;
using UnityEngine;
using System;

/// <summary>
/// 튜토리얼 안내/대사/반응 패널만 담당한다.
/// 공격/방어 턴 진행은 TutorialManager가 담당한다.
/// </summary>
public class TutorialDialoguePlayer : MonoBehaviour
{
    [Header("Guide UI")]
    [SerializeField] private GameObject guidePanel;
    [SerializeField] private TMP_Text guideText;

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

        if (hideOnAwake)
        {
            Hide();
        }
    }

    /// <summary>
    /// TutorialManager가 현재 튜토리얼 BPM을 전달한다.
    /// </summary>
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
    /// 안내 패널에 문장 하나를 표시한다.
    /// </summary>
    public void Show(string text)
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
        guideText.text = text;
    }

    /// <summary>
    /// 안내 패널을 숨긴다.
    /// </summary>
    public void Hide()
    {
        if (temporaryMessageCoroutine != null)
        {
            StopCoroutine(temporaryMessageCoroutine);
            temporaryMessageCoroutine = null;
        }

        if (guidePanel != null)
        {
            guidePanel.SetActive(false);
        }
    }

    /// <summary>
    /// 여러 안내 문장을 BPM 박자 단위로 순서대로 출력한다.
    /// 각 문장이 시작될 때 onLineStarted 콜백을 호출할 수 있다.
    /// lineIndex는 0부터 시작한다.
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

            yield return WaitUntilDspTime(visualShowDspTime);

            Show(lines[i]);
            onLineStarted?.Invoke(i, lines[i]);

            bool hasNextLine = i < lines.Length - 1;

            float blinkSeconds = useBlinkBetweenLines && hasNextLine
                ? Mathf.Clamp(blinkSecondsBetweenLines, 0f, lineSeconds * 0.5f)
                : 0f;

            double blinkStartDspTime = nextLineStartDspTime - blinkSeconds;

            yield return WaitUntilDspTime(blinkStartDspTime);

            if (blinkSeconds > 0f)
            {
                ShowBlinkBlank();
            }

            yield return WaitUntilDspTime(nextLineStartDspTime);
        }

        if (hideWhenFinished)
        {
            Hide();
        }
    }
    /// <summary>
    /// 튜토리얼 반응 문장을 지정한 박자 동안 표시한다.
    /// 턴 진행을 막지 않기 위해 코루틴을 내부에서 실행하고 바로 반환한다.
    /// </summary>
    public void ShowReactionForBeats(string text, int beats, bool hideWhenFinished = true)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        float seconds = GetBeatSeconds() * Mathf.Max(1, beats);
        ShowTemporary(text, seconds, hideWhenFinished);
    }

    /// <summary>
    /// 튜토리얼 반응 문장을 지정한 초 동안 표시한다.
    /// 턴 진행을 막지 않는다.
    /// </summary>
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

        Show(text);

        yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, seconds));

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
    }

    private float GetBeatSeconds()
    {
        if (RhythmClock.Instance != null)
        {
            return (float)RhythmClock.Instance.GetBeatDuration();
        }

        return 60f / Mathf.Max(1f, currentBpm);
    }

    private void ShowBlinkBlank()
    {
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