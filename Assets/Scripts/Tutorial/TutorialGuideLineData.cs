using UnityEngine;

/// <summary>
/// 튜토리얼 대사 1줄에 필요한 텍스트와 연출 정보를 담는다.
/// </summary>
[System.Serializable]
public class TutorialGuideLineData
{
    [TextArea(2, 4)]
    public string text;

    [Header("Rymo Portrait")]
    public Sprite rymoPortrait;

    [Header("Highlight")]
    public bool highlightStars;
    public bool highlightAttackJudgeLine;
    public bool highlightDefenseJudgeLine;
    public bool highlightHighBit;
    public bool highlightLowBit;

    [Header("Beat Demo")]
    public bool playHighThenLowDemo;

    [Header("Manual Advance")]
    [Tooltip("이 줄에서 UI 강조/데모 연출이 끝날 때까지 F/J 넘기기를 잠글지 여부.")]
    public bool lockManualAdvanceUntilVisualCueFinished;

    [Tooltip("수동 넘기기 잠금 시간. 박자 단위. 0이면 DialoguePlayer의 기본값을 사용한다.")]
    [Min(0f)]
    public float manualAdvanceUnlockDelayBeats = 1f;
}