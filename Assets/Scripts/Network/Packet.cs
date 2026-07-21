// PacketType은 Enums.cs에 정의

/// <summary>
/// 클럭 동기화 완료 후 게임 시작 시 양측에 전달되는 역할 배정 데이터.
/// </summary>
public struct GameStartPacket
{
    public byte myPlayerId;            // 수신자의 플레이어 ID (1 or 2)
    public byte firstAttackerId;       // 첫 번째 공격자 ID (1 or 2)
    public double clockOffset;         // guest_clock - host_clock (Host가 측정한 오프셋)
    public double gameStartDspTime;    // Host의 AudioSettings.dspTime (RhythmClock 동기화 기준)
}

/// <summary>
/// 공격 시작 시 방어자에게 전송. 방어자가 실시간 시각화를 준비하는 데 필요한 컨텍스트.
/// </summary>
public struct AttackStartPacket
{
    public byte attackerPlayerId;
    public double attackDuration;     // 방어자가 NOTE_CREATED 스폰 위치 계산에 사용
    public double attackStartDspTime; // 방어자가 BeginAttackVisual 즉시 시작에 사용
}

/// <summary>
/// 공격자가 탭할 때마다 즉시 전송되는 노트 스트리밍 데이터.
/// </summary>
public struct NoteCreatedPacket
{
    public int noteId;
    public double noteRelativeTime; // 탭 시각 - attackStartDspTime
    public NoteType noteType;       // HIGH or LOW
}

/// <summary>
/// 공격 종료 시 방어 시작 트리거로 전송되는 데이터.
/// attackStartDspTime을 포함해 방어자 측에서 judgeTime을 계산할 수 있게 한다.
/// </summary>
public struct AttackEndPacket
{
    public byte attackerPlayerId;
    public double attackStartDspTime; // 방어자가 judgeTime 계산에 사용
    public int badTimingInputCount;
    public int missingNoteCount;
    public int extraNoteCount;
    public int duplicateInputCount;
}

/// <summary>
/// 방어자가 노트를 판정할 때마다 즉시 전송. 공격자 화면에서 동일한 판정 결과를 표시한다.
/// </summary>
public struct JudgmentPacket
{
    public int noteId;
    public byte judgment; // Judgment enum 값
}

/// <summary>
/// 방어 종료 시 다음 공격 시작 트리거로 전송되는 데이터.
/// </summary>
public struct DefenseEndPacket
{
    public int missCount;
}
