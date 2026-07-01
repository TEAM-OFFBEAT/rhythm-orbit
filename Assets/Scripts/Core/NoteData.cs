public class NoteData
{
    public int noteId;
    public double noteRelativeTime; // noteCreateTime - attackStartTime; 네트워크로 상대 오프셋으로 전송
    public SnapType snapType;
    public double judgeTime; // DefenseTurn.Begin()에서 설정: defenseStartTime + noteRelativeTime


    /// <summary>화면 표시 시작 시각을 반환. NoteRenderer에서 호출.</summary>
    public double GetDisplayTime(double travelTime) => judgeTime - travelTime;
}
