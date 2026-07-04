public class NoteData
{
    public int noteId;
    public double noteRelativeTime; // 네트워크로 전송되는 값 (탭 시각 - 공격 시작 시각)
    public double judgeTime;        // transfer 신호 수신 시 설정 (defenseStartDspTime + noteRelativeTime)
}
