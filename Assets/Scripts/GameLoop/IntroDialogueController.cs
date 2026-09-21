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

    [Header("Dialogue")]
    [SerializeField] private IntroGuideLineData[] introLines;
    [SerializeField, Min(1)] private int beatsPerLine = 4;

    [Header("HUD Panel Glow")]
    [SerializeField] private HUDPlayerSlotUI p1Slot;
    [SerializeField] private HUDPlayerSlotUI p2Slot;

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

    private IEnumerator RunIntro(double startDspTime)
    {
        if (dialoguePlayer == null || introLines == null || introLines.Length == 0)
            yield break;

        TutorialGuideLineData[] dialogueLines = ExtractDialogueLines();

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

    private void HandleLineStarted(int lineIndex, TutorialGuideLineData _)
    {
        if (lineIndex < 0 || lineIndex >= introLines.Length)
            return;

        IntroGuideLineData line = introLines[lineIndex];
        p1Slot?.SetActiveState(line.highlightP1Panel, dimWhenInactive: false);
        p2Slot?.SetActiveState(line.highlightP2Panel, dimWhenInactive: false);
    }

    private void ResetPanelGlow()
    {
        p1Slot?.SetActiveState(false, dimWhenInactive: false);
        p2Slot?.SetActiveState(false, dimWhenInactive: false);
    }

    private TutorialGuideLineData[] ExtractDialogueLines()
    {
        var result = new TutorialGuideLineData[introLines.Length];
        for (int i = 0; i < introLines.Length; i++)
            result[i] = introLines[i]?.dialogue;
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
