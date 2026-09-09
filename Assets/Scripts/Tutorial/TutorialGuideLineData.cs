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
}