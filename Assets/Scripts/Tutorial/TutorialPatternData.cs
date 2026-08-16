using UnityEngine;

/// <summary>
/// 튜토리얼에서 사용할 메시지와 노트 패턴을 함께 담는다.
/// NoteCount는 실제 생성할 노트 수이자 HUD 별 개수 기준이다.
/// </summary>
[System.Serializable]
public class TutorialPatternData
{
    public string message;
    public NoteType[] notes;

    public int NoteCount => notes == null ? 0 : notes.Length;

    public TutorialPatternData(string message, NoteType[] notes)
    {
        this.message = message;
        this.notes = notes;
    }
}