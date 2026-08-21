using System.IO;

/// <summary>
/// 모든 패킷의 BinaryWriter 직렬화 / BinaryReader 역직렬화를 담당하는 정적 유틸 클래스.
/// 패킷 타입 헤더(byte) → 페이로드 순서로 쓰고 읽는다.
/// </summary>
public static class PacketSerializer
{
    // ── 송신 (Write) ──────────────────────────────────────────────────────────

    /// <summary>
    /// 클럭 동기화 요청. 페이로드 없음.
    /// </summary>
    public static void WritePing(BinaryWriter w)
        => w.Write((byte)PacketType.PING);

    /// <summary>
    /// 클럭 동기화 응답. 응답 시각의 dspTime을 담는다.
    /// </summary>
    public static void WritePong(BinaryWriter w, double sentDspTime)
    {
        w.Write((byte)PacketType.PONG);
        w.Write(sentDspTime);
    }

    /// <summary>
    /// 클럭 동기화 완료 후 Host → Guest 전송.
    /// myPlayerId는 수신자(Guest)의 ID, firstAttackerId는 첫 공격자 ID.
    /// clockOffset은 Host가 측정한 guest_clock - host_clock, gameStartDspTime은 Host의 AudioSettings.dspTime.
    /// sharedSeed는 Host가 생성한 RNG 공유 시드 — 양측 노트 수/메시지 동기화에 사용.
    /// </summary>
    public static void WriteGameStart(BinaryWriter w, byte myPlayerId, byte firstAttackerId, double clockOffset, double gameStartDspTime, int sharedSeed)
    {
        w.Write((byte)PacketType.GAME_START);
        w.Write(myPlayerId);
        w.Write(firstAttackerId);
        w.Write(clockOffset);
        w.Write(gameStartDspTime);
        w.Write(sharedSeed);
    }

    /// <summary>
    /// 공격 노트 실시간 스트리밍. 탭 발생 즉시 전송.
    /// </summary>
    public static void WriteNoteCreated(BinaryWriter w, int noteId, double noteRelativeTime, NoteType noteType)
    {
        w.Write((byte)PacketType.NOTE_CREATED);
        w.Write(noteId);
        w.Write(noteRelativeTime);
        w.Write((byte)noteType);
    }

    /// <summary>
    /// 방어자 노트 판정 결과 실시간 전송. 공격자 화면 동기화에 사용.
    /// </summary>
    public static void WriteJudgment(BinaryWriter w, int noteId, Judgment judgment)
    {
        w.Write((byte)PacketType.JUDGMENT);
        w.Write(noteId);
        w.Write((byte)judgment);
    }

    /// <summary>
    /// 정신력 변화를 상대에게 전송한다.
    /// </summary>
    public static void WriteSanityChange(BinaryWriter w, byte targetPlayerId, int amount)
    {
        w.Write((byte)PacketType.SANITY_CHANGE);
        w.Write(targetPlayerId);
        w.Write(amount);
    }

    // ── 수신 (Read) ──────────────────────────────────────────────────────────

    /// <summary>
    /// 스트림에서 패킷 타입 헤더 1바이트를 읽는다.
    /// </summary>
    public static PacketType PeekType(BinaryReader r)
        => (PacketType)r.ReadByte();

    /// <summary>
    /// PONG 페이로드: sentDspTime.
    /// </summary>
    public static double ReadPong(BinaryReader r)
        => r.ReadDouble();

    /// <summary>
    /// GAME_START 페이로드.
    /// </summary>
    public static GameStartPacket ReadGameStart(BinaryReader r)
        => new GameStartPacket
        {
            myPlayerId       = r.ReadByte(),
            firstAttackerId  = r.ReadByte(),
            clockOffset      = r.ReadDouble(),
            gameStartDspTime = r.ReadDouble(),
            sharedSeed       = r.ReadInt32()
        };

    /// <summary>
    /// NOTE_CREATED 페이로드.
    /// </summary>
    public static NoteCreatedPacket ReadNoteCreated(BinaryReader r)
        => new NoteCreatedPacket
        {
            noteId             = r.ReadInt32(),
            noteRelativeTime   = r.ReadDouble(),
            noteType           = (NoteType)r.ReadByte()
        };

    /// <summary>
    /// JUDGMENT 페이로드.
    /// </summary>
    public static JudgmentPacket ReadJudgment(BinaryReader r)
        => new JudgmentPacket
        {
            noteId   = r.ReadInt32(),
            judgment = r.ReadByte()
        };

    /// <summary>
    /// SANITY_CHANGE 페이로드.
    /// </summary>
    public static SanityChangePacket ReadSanityChange(BinaryReader r)
        => new SanityChangePacket
        {
            targetPlayerId = r.ReadByte(),
            amount         = r.ReadInt32()
        };

    /// <summary>
    /// 게임 종료 패킷을 기록한다.
    /// 종료 사유와 패배한 플레이어 ID를 함께 전송한다.
    /// </summary>

    public static void WriteGameEnd(BinaryWriter writer, GameEndReason reason, int defeatedPlayerId = 0)
    {
        writer.Write((byte)PacketType.GAME_END);
        writer.Write((byte)reason);
        writer.Write(ClampPlayerIdToByte(defeatedPlayerId));
    }

    /// <summary>
    /// 게임 종료 패킷의 본문을 읽는다.
    /// 패킷 타입 byte는 ReceiveLoop에서 이미 읽었으므로 여기서는 본문만 읽는다.
    /// </summary>
    public static GameEndPacket ReadGameEnd(BinaryReader reader)
    {
        return new GameEndPacket
        {
            reason = (GameEndReason)reader.ReadByte(),
            defeatedPlayerId = reader.ReadByte()
        };
    }

    private static byte ClampPlayerIdToByte(int playerId)
    {
        if (playerId < 0) return 0;
        if (playerId > 2) return 2;

        return (byte)playerId;
    }    

    public static void WriteReplayRequest(BinaryWriter writer, byte requesterPlayerId)
    {
        writer.Write((byte)PacketType.REPLAY_REQUEST);
        writer.Write(requesterPlayerId);
    }

    public static ReplayRequestPacket ReadReplayRequest(BinaryReader reader)
    {
        return new ReplayRequestPacket
        {
            requesterPlayerId = reader.ReadByte()
        };
    }
}
