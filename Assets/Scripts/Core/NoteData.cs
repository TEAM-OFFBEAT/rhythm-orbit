public class NoteData
{
    public int noteId;
    public NoteType noteType;       // 기본값 NoteType.HIGH(0). 네트워크 역직렬화 시 명시적으로 설정할 것.
    public double noteRelativeTime; // 네트워크로 전송되는 값 (탭 시각 - 공격 시작 시각)
    public double judgeTime;        // transfer 신호 수신 시 설정 (defenseStartDspTime + noteRelativeTime)
}

/// <summary>
/// EVT_DEF_01 유령 신호 전용 노트 데이터.
/// 일반 방어 결과 Judgment에는 포함하지 않는다.
/// </summary>
public class GhostNoteData
{
    public int noteId;
    public NoteType noteType;
    public double noteRelativeTime;
    public double judgeTime;
}