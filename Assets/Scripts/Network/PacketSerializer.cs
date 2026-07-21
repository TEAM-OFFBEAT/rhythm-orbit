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
    /// </summary>
    public static void WriteGameStart(BinaryWriter w, byte myPlayerId, byte firstAttackerId, double clockOffset, double gameStartDspTime)
    {
        w.Write((byte)PacketType.GAME_START);
        w.Write(myPlayerId);
        w.Write(firstAttackerId);
        w.Write(clockOffset);
        w.Write(gameStartDspTime);
    }

    /// <summary>
    /// 게임 세션 종료 신호. 페이로드 없음.
    /// </summary>
    public static void WriteGameEnd(BinaryWriter w)
        => w.Write((byte)PacketType.GAME_END);

    /// <summary>
    /// 공격 시작 알림. 방어자가 실시간 노트 시각화를 준비하는 데 사용.
    /// </summary>
    public static void WriteAttackStart(BinaryWriter w, byte attackerPlayerId, double attackDuration, double attackStartDspTime)
    {
        w.Write((byte)PacketType.ATTACK_START);
        w.Write(attackerPlayerId);
        w.Write(attackDuration);
        w.Write(attackStartDspTime);
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
    /// 공격 종료 신호. attackStartDspTime을 포함해 방어자가 judgeTime을 계산할 수 있게 한다.
    /// </summary>
    public static void WriteAttackEnd(BinaryWriter w, byte attackerPlayerId, double attackStartDspTime, AttackResult result)
    {
        w.Write((byte)PacketType.ATTACK_END);
        w.Write(attackerPlayerId);
        w.Write(attackStartDspTime);
        w.Write(result.BadTimingInputCount);
        w.Write(result.MissingNoteCount);
        w.Write(result.ExtraNoteCount);
        w.Write(result.DuplicateInputCount);
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
    /// 방어 종료 신호. missCount를 포함해 양측이 SanitySystem을 동기화한다.
    /// </summary>
    public static void WriteDefenseEnd(BinaryWriter w, int missCount)
    {
        w.Write((byte)PacketType.DEFENSE_END);
        w.Write(missCount);
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
            gameStartDspTime = r.ReadDouble()
        };

    /// <summary>
    /// ATTACK_START 페이로드.
    /// </summary>
    public static AttackStartPacket ReadAttackStart(BinaryReader r)
        => new AttackStartPacket
        {
            attackerPlayerId   = r.ReadByte(),
            attackDuration     = r.ReadDouble(),
            attackStartDspTime = r.ReadDouble()
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
    /// ATTACK_END 페이로드.
    /// </summary>
    public static AttackEndPacket ReadAttackEnd(BinaryReader r)
        => new AttackEndPacket
        {
            attackerPlayerId      = r.ReadByte(),
            attackStartDspTime    = r.ReadDouble(),
            badTimingInputCount   = r.ReadInt32(),
            missingNoteCount      = r.ReadInt32(),
            extraNoteCount        = r.ReadInt32(),
            duplicateInputCount   = r.ReadInt32()
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
    /// DEFENSE_END 페이로드.
    /// </summary>
    public static DefenseEndPacket ReadDefenseEnd(BinaryReader r)
        => new DefenseEndPacket { missCount = r.ReadInt32() };
}
