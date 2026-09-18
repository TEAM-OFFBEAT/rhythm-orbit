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

    [Tooltip("리모 프로필로 사용할 Image. HUD 패널의 프로필 Image를 연결한다.")]
    [SerializeField] private Image rymoPortraitImage;

    [Header("Highlight UI")]
    [SerializeField] private TutorialStarHighlightView starHighlightView;
    [SerializeField] private GameObject attackJudgeLineHighlightRoot;
    [SerializeField] private GameObject defenseJudgeLineHighlightRoot;
    [SerializeField] private GameObject highBitHighlightRoot;
    [SerializeField] private GameObject lowBitHighlightRoot;

    [Header("Typewriter")]
    [SerializeField] private TypewriterText typewriterText;
    [SerializeField] private bool useTypewriter = true;

    [Tooltip("문장 타이핑이 끝난 뒤 다음 문장으로 넘어가기 전에 유지할 시간.")]
    [SerializeField, Min(0f)] private float holdSecondsAfterTyping = 0.6f;

    [Tooltip("문장이 너무 짧아도 최소한 이 시간 이상은 타이핑에 사용한다.")]
    [SerializeField, Min(0.01f)] private float minimumTypingSeconds = 0.15f;

    [Header("Manual Advance")]
    [SerializeField] private GameObject manualAdvanceHintRoot;
    [SerializeField] private TMP_Text manualAdvanceHintText;
    private bool externalManualAdvanceLocked;
    [SerializeField] private string manualAdvanceHintMessage = "(F/J로 대사 넘기기)";
    [Tooltip("연출이 있는 줄에서 별도 시간이 0으로 설정되어 있을 때 사용할 기본 잠금 시간. 박자 단위.")]
    [SerializeField, Min(0f)] private float defaultManualAdvanceLockBeats = 1f;
    [Tooltip("일반 대사도 너무 빨리 넘기지 않도록, 수동 넘기기 힌트를 띄우기 전 최소 대기 시간.")]
    [SerializeField, Min(0f)] private float minimumManualAdvanceDelaySeconds = 0.5f;
    [Tooltip("켜면 타이핑 중 F/J로 전체 문장을 즉시 표시한다. 끄면 타이핑이 끝난 뒤에만 다음 대사로 넘길 수 있다.")]
    [SerializeField] private bool allowSkipTypingWithManualAdvance = true;

    private bool isManualLineActive;
    private bool isTypingCurrentLine;
    private bool isCurrentLineFullyVisible;
    private bool manualAdvanceUnlocked;
    private bool skipTypingRequested;
    private bool nextLineRequested;

    [Header("Timing")]
    [SerializeField] private float fallbackBpm = 90f;
    [SerializeField] private bool hideOnAwake = true;

    [Header("Line Blink")]
    [SerializeField] private bool useBlinkBetweenLines = true;

    [Tooltip("다음 문장으로 넘어가기 직전 깜빡이는 시간. 0.08~0.15초 추천.")]
    [SerializeField, Min(0f)] private float blinkSecondsBetweenLines = 0.1f;

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

        ClearGuideText();
        ClearHighlights();
        SetManualAdvanceHintVisible(false);

        if (hideOnAwake)
        {
            SetGuidePanelVisible(false);
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
    /// TutorialManager가 F/J 입력을 받았을 때 호출한다.
    /// 타이핑 중 스킵이 허용되어 있으면 전체 문장을 즉시 표시하고,
    /// 이미 전체 문장이 보이는 상태면 다음 대사로 진행한다.
    /// UI 강조/데모/외부 연출 잠금 중이면 입력을 무시한다.
    /// </summary>
    public bool RequestManualAdvance()
    {
        if (!isManualLineActive)
        {
            return false;
        }

        if (!manualAdvanceUnlocked)
        {
            return false;
        }

        if (externalManualAdvanceLocked)
        {
            return false;
        }

        if (isTypingCurrentLine)
        {
            if (!allowSkipTypingWithManualAdvance)
            {
                return false;
            }

            skipTypingRequested = true;
            return true;
        }

        if (isCurrentLineFullyVisible)
        {
            nextLineRequested = true;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 대사 외부에서 실행되는 연출이 끝날 때까지 수동 넘기기를 잠근다.
    /// 예: 방어턴 설명 중 실제 방어 데모가 재생되는 동안.
    /// </summary>
    public void BeginExternalManualAdvanceLock()
    {
        externalManualAdvanceLocked = true;
        RefreshManualAdvanceHint();
    }

    /// <summary>
    /// 외부 연출 잠금을 해제한다.
    /// 현재 대사가 이미 넘길 수 있는 상태라면 힌트를 다시 표시한다.
    /// </summary>
    public void EndExternalManualAdvanceLock()
    {
        externalManualAdvanceLocked = false;
        RefreshManualAdvanceHint();
    }

    /// <summary>
    /// 리모 프로필 이미지를 즉시 변경한다.
    /// 리모 프로필은 HUD에 있으므로 대사 패널 활성/비활성과 별도로 유지된다.
    /// </summary>
    public void SetRymoPortrait(Sprite portrait)
    {
        if (rymoPortraitImage == null || portrait == null)
        {
            return;
        }

        rymoPortraitImage.sprite = portrait;
    }

    /// <summary>
    /// 일반 대사/문구를 표시한다.
    /// 텍스트가 비어 있으면 대사 패널을 끈다.
    /// </summary>
    public void Show(string text)
    {
        StopTemporaryMessage();

        if (string.IsNullOrWhiteSpace(text))
        {
            ClearGuideText();
            SetGuidePanelVisible(false);
            return;
        }

        ShowTyped(text, CalculateTypingSeconds(GetBeatSeconds(), blinkSeconds: 0f));
    }

    /// <summary>
    /// 대사를 즉시 표시한다.
    /// 텍스트가 비어 있으면 대사 패널을 끈다.
    /// </summary>
    private void ShowInstant(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            ClearGuideText();
            SetGuidePanelVisible(false);
            return;
        }

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

        SetGuidePanelVisible(true);

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

    /// <summary>
    /// 대사를 타자 효과로 표시한다.
    /// 텍스트가 비어 있으면 대사 패널을 끈다.
    /// </summary>
    private void ShowTyped(string text, float durationSeconds)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            ClearGuideText();
            SetGuidePanelVisible(false);
            return;
        }

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

        SetGuidePanelVisible(true);

        if (useTypewriter && typewriterText != null)
        {
            typewriterText.Play(text, durationSeconds);
        }
        else
        {
            ShowInstant(text);
        }
    }

    /// <summary>
    /// 대사 구간을 완전히 숨긴다.
    /// 패널과 하이라이트를 모두 정리한다.
    /// </summary>
    public void Hide()
    {
        StopTemporaryMessage();
        ResetManualAdvanceState();

        ClearGuideText();
        SetGuidePanelVisible(false);
        ClearHighlights();
    }

    /// <summary>
    /// 기존 string[] 대사용.
    /// 이제 대사는 자동으로 넘어가지 않고, F/J 입력으로만 진행된다.
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
            if (hideWhenFinished)
            {
                Hide();
            }

            yield break;
        }

        StopTemporaryMessage();

        int safeBeats = Mathf.Max(1, beatsPerLine);
        float beatSeconds = GetBeatSeconds();

        double dialogueStartDspTime =
            forcedStartDspTime ?? AudioSettings.dspTime;

        double visualShowDspTime =
            dialogueStartDspTime - dialogueVisualLeadSeconds;

        yield return WaitUntilDspTime(visualShowDspTime);

        for (int i = 0; i < lines.Length; i++)
        {
            string lineText = lines[i];
            bool hasNextLine = i < lines.Length - 1;

            ClearHighlights();

            if (string.IsNullOrWhiteSpace(lineText))
            {
                ClearGuideText();
                SetGuidePanelVisible(false);
                continue;
            }

            float typingSeconds = CalculateTypingSeconds(
                beatSeconds * safeBeats + dialogueVisualLeadSeconds,
                blinkSeconds: 0f
            );

            ShowTyped(lineText, typingSeconds);
            onLineStarted?.Invoke(i, lineText);

            // string[] 대사는 별도 연출 정보가 없으므로 즉시 수동 넘기기 가능.
            yield return WaitManualLineAdvance(
                lineText,
                typingSeconds,
                GetMinimumManualAdvanceDelaySeconds()
            );

            if (useBlinkBetweenLines && hasNextLine && blinkSecondsBetweenLines > 0f)
            {
                ShowBlinkBlank();
                yield return new WaitForSecondsRealtime(blinkSecondsBetweenLines);
            }
        }

        if (hideWhenFinished)
        {
            Hide();
        }
        else
        {
            ClearGuideText();
            SetGuidePanelVisible(false);
            ClearHighlights();
            ResetManualAdvanceState();
        }
    }

    /// <summary>
    /// TutorialGuideLineData[] 대사용.
    /// 대사 텍스트, 리모 프로필, 강조 연출을 줄 단위로 적용한다.
    /// 이제 대사는 자동으로 넘어가지 않고, F/J 입력으로만 진행된다.
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
            if (hideWhenFinished)
            {
                Hide();
            }

            yield break;
        }

        StopTemporaryMessage();

        int safeBeats = Mathf.Max(1, beatsPerLine);
        float beatSeconds = GetBeatSeconds();

        double dialogueStartDspTime =
            forcedStartDspTime ?? AudioSettings.dspTime;

        double visualShowDspTime =
            dialogueStartDspTime - dialogueVisualLeadSeconds;

        yield return WaitUntilDspTime(visualShowDspTime);

        for (int i = 0; i < lines.Length; i++)
        {
            TutorialGuideLineData line = lines[i];
            bool hasNextLine = i < lines.Length - 1;

            if (line == null || string.IsNullOrWhiteSpace(line.text))
            {
                ClearGuideText();
                SetGuidePanelVisible(false);
                ClearHighlights();
                continue;
            }

            ApplyLineVisual(line);

            float typingSeconds = CalculateTypingSeconds(
                beatSeconds * safeBeats + dialogueVisualLeadSeconds,
                blinkSeconds: 0f
            );

            ShowTyped(line.text, typingSeconds);
            onLineStarted?.Invoke(i, line);

            float manualUnlockDelaySeconds =
                GetManualAdvanceUnlockDelaySeconds(line);

            yield return WaitManualLineAdvance(
                line.text,
                typingSeconds,
                manualUnlockDelaySeconds
            );

            if (useBlinkBetweenLines && hasNextLine && blinkSecondsBetweenLines > 0f)
            {
                ShowBlinkBlank();
                yield return new WaitForSecondsRealtime(blinkSecondsBetweenLines);
            }
        }

        if (hideWhenFinished)
        {
            Hide();
        }
        else
        {
            ClearGuideText();
            SetGuidePanelVisible(false);
            ClearHighlights();
            ResetManualAdvanceState();
        }
    }

    /// <summary>
    /// 현재 대사 한 줄이 F/J 입력으로 넘어갈 때까지 기다린다.
    /// 입력 동작:
    /// - allowSkipTypingWithManualAdvance가 true면, 타이핑 중 F/J로 전체 문장 표시
    /// - false면, 타이핑이 끝난 뒤에만 F/J 넘기기 가능
    /// - 외부 연출 잠금 중이면 F/J 입력을 무시
    /// - 전체 문장 표시 후 F/J: 다음 대사로 진행
    /// </summary>
    private IEnumerator WaitManualLineAdvance(
        string fullText,
        float typingSeconds,
        float manualUnlockDelaySeconds
    )
    {
        // onLineStarted에서 외부 연출 잠금이 걸렸을 수 있으므로
        // externalManualAdvanceLocked는 유지한다.
        ResetManualAdvanceState(clearExternalLock: false);

        isManualLineActive = true;
        isTypingCurrentLine = useTypewriter && typewriterText != null;
        isCurrentLineFullyVisible = !isTypingCurrentLine;

        float elapsed = 0f;
        float safeTypingSeconds = Mathf.Max(0.01f, typingSeconds);
        float safeUnlockDelaySeconds = Mathf.Max(0f, manualUnlockDelaySeconds);

        manualAdvanceUnlocked = false;
        RefreshManualAdvanceHint();

        while (isTypingCurrentLine && elapsed < safeTypingSeconds)
        {
            elapsed += Time.unscaledDeltaTime;

            if (!manualAdvanceUnlocked && elapsed >= safeUnlockDelaySeconds)
            {
                manualAdvanceUnlocked = true;
                RefreshManualAdvanceHint();
            }

            if (skipTypingRequested &&
                manualAdvanceUnlocked &&
                !externalManualAdvanceLocked &&
                allowSkipTypingWithManualAdvance)
            {
                ShowInstant(fullText);
                break;
            }

            yield return null;
        }

        // 자연 종료든 스킵이든, 여기부터는 전체 문장이 보이는 상태로 고정한다.
        ShowInstant(fullText);
        isTypingCurrentLine = false;
        isCurrentLineFullyVisible = true;
        skipTypingRequested = false;

        // 타이핑은 끝났지만 최소 대기/연출 잠금 시간이 아직 남아 있으면 더 기다린다.
        while (!manualAdvanceUnlocked)
        {
            elapsed += Time.unscaledDeltaTime;

            if (elapsed >= safeUnlockDelaySeconds)
            {
                manualAdvanceUnlocked = true;
                break;
            }

            yield return null;
        }

        RefreshManualAdvanceHint();

        while (!nextLineRequested)
        {
            yield return null;
        }

        ResetManualAdvanceState();
    }

    /// <summary>
    /// 이 줄에서 수동 넘기기를 잠글 시간을 초 단위로 계산한다.
    /// 일반 대사도 최소 0.5초는 기다린 뒤 넘길 수 있게 하고,
    /// UI 강조나 F/J 데모 같은 연출이 있는 줄은 더 긴 잠금 시간을 적용한다.
    /// </summary>
    private float GetManualAdvanceUnlockDelaySeconds(TutorialGuideLineData line)
    {
        float minimumDelaySeconds = GetMinimumManualAdvanceDelaySeconds();

        if (!ShouldLockManualAdvance(line))
        {
            return minimumDelaySeconds;
        }

        float lockBeats = line.manualAdvanceUnlockDelayBeats;

        if (lockBeats <= 0f)
        {
            lockBeats = defaultManualAdvanceLockBeats;
        }

        float visualLockSeconds = GetBeatSeconds() * Mathf.Max(0f, lockBeats);

        // 일반 최소 딜레이보다 연출 잠금이 짧으면 의미가 없으므로 더 긴 쪽을 사용한다.
        return Mathf.Max(minimumDelaySeconds, visualLockSeconds);
    }

    /// <summary>
    /// 모든 대사에 공통으로 적용할 최소 수동 넘기기 대기 시간.
    /// 너무 빠른 연타로 대사가 즉시 넘어가는 것을 막는다.
    /// </summary>
    private float GetMinimumManualAdvanceDelaySeconds()
    {
        return Mathf.Max(0f, minimumManualAdvanceDelaySeconds);
    }

    /// <summary>
    /// 수동 넘기기를 잠가야 하는 줄인지 확인한다.
    /// 인스펙터에서 직접 잠금을 켰거나, 하이라이트/데모 연출이 있으면 잠금 대상으로 본다.
    /// </summary>
    private bool ShouldLockManualAdvance(TutorialGuideLineData line)
    {
        if (line == null)
        {
            return false;
        }

        if (line.lockManualAdvanceUntilVisualCueFinished)
        {
            return true;
        }

        return line.highlightStars
            || line.highlightAttackJudgeLine
            || line.highlightDefenseJudgeLine
            || line.highlightHighBit
            || line.highlightLowBit
            || line.playHighThenLowDemo
            || line.playDefenseTurnDemo;
    }

    /// <summary>
    /// 수동 넘기기 상태를 초기화하고 힌트를 숨긴다.
    /// clearExternalLock이 false면 외부 연출 잠금은 유지한다.
    /// </summary>
    private void ResetManualAdvanceState(bool clearExternalLock = true)
    {
        isManualLineActive = false;
        isTypingCurrentLine = false;
        isCurrentLineFullyVisible = false;
        manualAdvanceUnlocked = false;
        skipTypingRequested = false;
        nextLineRequested = false;

        if (clearExternalLock)
        {
            externalManualAdvanceLocked = false;
        }

        SetManualAdvanceHintVisible(false);
    }

    /// <summary>
    /// 현재 상태에 맞게 수동 넘기기 힌트를 표시하거나 숨긴다.
    /// 외부 연출 잠금 중이면 무조건 숨긴다.
    /// </summary>
    private void RefreshManualAdvanceHint()
    {
        bool canShowHint =
            isManualLineActive &&
            manualAdvanceUnlocked &&
            !externalManualAdvanceLocked &&
            (!isTypingCurrentLine || allowSkipTypingWithManualAdvance);

        SetManualAdvanceHintVisible(canShowHint);
}

    /// <summary>
    /// "(F/J로 대사 넘기기)" 힌트를 켜거나 끈다.
    /// Root가 연결되어 있으면 Root를 우선 제어하고,
    /// Root가 없으면 TMP_Text 오브젝트를 직접 제어한다.
    /// </summary>
    private void SetManualAdvanceHintVisible(bool visible)
    {
        if (manualAdvanceHintText != null)
        {
            manualAdvanceHintText.text = manualAdvanceHintMessage;
        }

        if (manualAdvanceHintRoot != null)
        {
            manualAdvanceHintRoot.SetActive(visible);
            return;
        }

        if (manualAdvanceHintText != null)
        {
            manualAdvanceHintText.gameObject.SetActive(visible);
        }
    }

    /// <summary>
    /// 현재 대사 줄에 맞는 프로필과 하이라이트를 적용한다.
    /// 연속된 대사에서 같은 하이라이트가 true면 중간에 깜빡이지 않는다.
    /// 방어 데모 줄에서는 기존 판정선 강조 대신 실제 방어 데모를 사용한다.
    /// </summary>
    private void ApplyLineVisual(TutorialGuideLineData line)
    {
        if (line == null)
        {
            ClearHighlights();
            return;
        }

        SetRymoPortrait(line.rymoPortrait);

        bool showDefenseJudgeLineHighlight =
            line.highlightDefenseJudgeLine && !line.playDefenseTurnDemo;

        SetStarHighlight(line.highlightStars);
        SetActiveSafe(attackJudgeLineHighlightRoot, line.highlightAttackJudgeLine);
        SetActiveSafe(defenseJudgeLineHighlightRoot, showDefenseJudgeLineHighlight);
        SetActiveSafe(highBitHighlightRoot, line.highlightHighBit);
        SetActiveSafe(lowBitHighlightRoot, line.highlightLowBit);
    }

    /// <summary>
    /// 모든 튜토리얼 하이라이트를 끈다.
    /// 대사 사이 깜빡임에서는 호출하지 않는다.
    /// </summary>
    public void ClearHighlights()
    {
        SetStarHighlight(false);
        SetActiveSafe(attackJudgeLineHighlightRoot, false);
        SetActiveSafe(defenseJudgeLineHighlightRoot, false);
        SetActiveSafe(highBitHighlightRoot, false);
        SetActiveSafe(lowBitHighlightRoot, false);
    }

    public void ShowReactionForBeats(string text, int beats, bool hideWhenFinished = true)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            ClearGuideText();
            SetGuidePanelVisible(false);
            return;
        }

        float seconds = GetBeatSeconds() * Mathf.Max(1, beats);
        ShowTemporary(text, seconds, hideWhenFinished);
    }

    public void ShowTemporary(string text, float seconds, bool hideWhenFinished = true)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            ClearGuideText();
            SetGuidePanelVisible(false);
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
            Hide();
        }
        else
        {
            ClearGuideText();
            SetGuidePanelVisible(false);
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

        ResetManualAdvanceState();
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

    /// <summary>
    /// 대사 사이의 빈 타이밍 처리.
    /// 패널만 끄고, 하이라이트는 유지한다.
    /// </summary>
    private void ShowBlinkBlank()
    {
        if (typewriterText != null)
        {
            typewriterText.Stop();
        }

        ClearGuideText();
        SetGuidePanelVisible(false);
    }

    private IEnumerator WaitUntilDspTime(double targetDspTime)
    {
        while (AudioSettings.dspTime < targetDspTime)
        {
            yield return null;
        }
    }

    /// <summary>
    /// 대사 패널만 켜거나 끈다.
    /// 리모 프로필과 하이라이트는 건드리지 않는다.
    /// </summary>
    private void SetGuidePanelVisible(bool visible)
    {
        if (guidePanel != null)
        {
            guidePanel.SetActive(visible);
        }

        if (guideText != null)
        {
            guideText.gameObject.SetActive(visible);
        }
    }

    /// <summary>
    /// 대사 텍스트만 비운다.
    /// 패널 활성 상태와 하이라이트는 건드리지 않는다.
    /// </summary>
    private void ClearGuideText()
    {
        if (guideText != null)
        {
            guideText.text = string.Empty;
            guideText.maxVisibleCharacters = 0;
        }
    }

    private void SetStarHighlight(bool active)
    {
        if (starHighlightView != null)
        {
            starHighlightView.SetVisible(active);
        }
    }

    private void SetActiveSafe(GameObject target, bool active)
    {
        if (target != null)
        {
            target.SetActive(active);
        }
    }
}