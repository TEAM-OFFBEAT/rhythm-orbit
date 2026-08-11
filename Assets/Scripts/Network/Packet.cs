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
    public int sharedSeed;             // 양측 RNG 동기화용 공유 시드 (Host가 생성해 전송)
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
/// 방어자가 노트를 판정할 때마다 즉시 전송. 공격자 화면에서 동일한 판정 결과를 표시한다.
/// </summary>
public struct JudgmentPacket
{
    public int noteId;
    public byte judgment; // Judgment enum 값
}

/// <summary>
/// 정신력 변화 발생 시 상대에게 전송. 공격 패널티와 방어 미스 양쪽이 사용한다.
/// </summary>
public struct SanityChangePacket
{
    public byte targetPlayerId; // 정신력이 감소하는 플레이어 ID
    public int amount;          // 감소량 (항상 양수)
}

public enum GameEndReason
{
    PlayerDefeated,
    CommunicationSuccess
}

public struct GameEndPacket
{
    public GameEndReason reason;

    // reason이 PlayerDefeated일 때만 사용한다.
    // P1이 패배하면 1, P2가 패배하면 2.
    // CommunicationSuccess일 때는 0으로 둔다.
    public byte defeatedPlayerId;
}
