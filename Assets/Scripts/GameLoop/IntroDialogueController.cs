using System.Collections;
using UnityEngine;

/// <summary>
/// 코어 루프 씬 인트로 구간에 대사 패널과 플레이어 슬롯 글로우 효과를 표시한다.
/// TutorialDialoguePlayer를 재사용하는 얇은 컨트롤러.
/// GameManager.StartGame()에서 StartIntro(rhythmStart)를 호출한다.
/// </summary>
public class IntroDialogueController : MonoBehaviour
{
    [SerializeField] private TutorialDialoguePlayer dialoguePlayer;

    [Header("Dialogue - 4/4")]
    [SerializeField] private IntroGuideLineData[] introLines;
    [Header("Dialogue - 3/4")]
    [SerializeField] private IntroGuideLineData[] introLines34;
    [SerializeField, Min(1)] private int beatsPerLine = 4;
    private bool useThreeFourLines;

    [Header("HUD Panel Glow")]
    [SerializeField] private HUDPlayerSlotUI p1Slot;
    [SerializeField] private HUDPlayerSlotUI p2Slot;

    [Header("Player Label")]
    [SerializeField] private Sprite meSprite;
    [SerializeField] private Sprite youSprite;
    private int localPlayerId = 1;

    private Coroutine introCoroutine;

    private void OnDisable()
    {
        Stop();
    }

    /// <summary>
    /// 인트로 대사를 지정한 DSP 시각에 시작한다.
    /// GameManager.StartGame()에서 nextPhaseDspTime을 넘겨 호출한다.
    /// </summary>
    public void StartIntro(double startDspTime)
    {
        Stop();
        introCoroutine = StartCoroutine(RunIntro(startDspTime));
    }

    /// <summary>
    /// 진행 중인 인트로 대사 자동 진행을 일시정지한다. 현재 줄 텍스트는 유지된다.
    /// </summary>
    public void Pause()
    {
        dialoguePlayer?.SetPaused(true);
    }

    /// <summary>
    /// 진행 중인 인트로 대사와 글로우 효과를 즉시 중단한다.
    /// </summary>
    public void Stop()
    {
        if (introCoroutine != null)
        {
            StopCoroutine(introCoroutine);
            introCoroutine = null;
        }

        dialoguePlayer?.Hide();
        ResetPanelGlow();
    }

    /// <summary>
    /// 박자 모드 변경 시 GameManager가 호출해 대사 줄 표시 간격과 사용 배열을 갱신한다.
    /// </summary>
    public void SetBeatsPerLine(int beats)
    {
        beatsPerLine = Mathf.Max(1, beats);
    }

    /// <summary>
    /// 3/4박자 여부를 설정한다. true면 introLines34 배열을 사용한다.
    /// </summary>
    public void SetThreeFourMode(bool isThreeFour)
    {
        useThreeFourLines = isThreeFour;
    }

    /// <summary>
    /// 로컬 플레이어 ID(1 또는 2)를 설정한다. ME/YOU 라벨 배치에 사용한다.
    /// GameManager.Awake()에서 호출한다.
    /// </summary>
    public void SetLocalPlayerId(int id)
    {
        localPlayerId = id;
    }

    private IEnumerator RunIntro(double startDspTime)
    {
        IntroGuideLineData[] activeLines = useThreeFourLines && introLines34?.Length > 0
            ? introLines34 : introLines;

        if (dialoguePlayer == null || activeLines == null || activeLines.Length == 0)
            yield break;

        currentActiveLines = activeLines;
        TutorialGuideLineData[] dialogueLines = ExtractDialogueLines(activeLines);

        ShowPlayerLabels();

        yield return dialoguePlayer.PlayLines(
            dialogueLines,
            beatsPerLine,
            hideWhenFinished: true,
            onLineStarted: HandleLineStarted,
            forcedStartDspTime: startDspTime,
            autoAdvance: true
        );

        ResetPanelGlow();
        introCoroutine = null;
    }

    private IntroGuideLineData[] currentActiveLines;

    private void HandleLineStarted(int lineIndex, TutorialGuideLineData _)
    {
        if (currentActiveLines == null || lineIndex < 0 || lineIndex >= currentActiveLines.Length)
            return;

        IntroGuideLineData line = currentActiveLines[lineIndex];
        p1Slot?.SetActiveState(line.highlightP1Panel);
        p2Slot?.SetActiveState(line.highlightP2Panel);
    }

    private void ResetPanelGlow()
    {
        p1Slot?.SetActiveState(false);
        p2Slot?.SetActiveState(false);
        HidePlayerLabels();
    }

    private void ShowPlayerLabels()
    {
        bool localIsP1 = localPlayerId == 1;
        p1Slot?.ShowLabel(localIsP1 ? meSprite : youSprite);
        p2Slot?.ShowLabel(localIsP1 ? youSprite : meSprite);
    }

    private void HidePlayerLabels()
    {
        p1Slot?.HideLabel();
        p2Slot?.HideLabel();
    }

    private TutorialGuideLineData[] ExtractDialogueLines(IntroGuideLineData[] lines)
    {
        var result = new TutorialGuideLineData[lines.Length];
        for (int i = 0; i < lines.Length; i++)
            result[i] = lines[i]?.dialogue;
        return result;
    }
}

/// <summary>
/// 코어 루프 인트로 대사 1줄의 텍스트, 연출, HUD 슬롯 글로우를 함께 담는다.
/// </summary>
[System.Serializable]
public class IntroGuideLineData
{
    public TutorialGuideLineData dialogue;

    [Header("HUD Panel Glow")]
    public bool highlightP1Panel;
    public bool highlightP2Panel;
}
