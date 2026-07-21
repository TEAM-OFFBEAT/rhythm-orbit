public enum Judgment { PERFECT, GOOD, MISS }
public enum GameState { ATTACK, DEFENSE, TURN_CHANGE, END }
public enum NoteType { HIGH, LOW }
public enum PacketType
{
    PING, PONG,          // 클럭 동기화 — RTT 측정으로 두 기기의 dspTime 오프셋 계산
    GAME_START,          // 클럭 동기화 완료 후 양측 게임 시작 (P1/P2 역할 + firstAttackerId 포함)
    GAME_END,            // 게임 세션 종료 (패배 확정 또는 연결 끊김)
    ATTACK_START,        // 공격 시작 알림 → 방어자가 실시간 시각화 준비 (attackerPlayerId + attackDuration)
    NOTE_CREATED,        // 공격 노트 실시간 스트리밍 (탭 발생 즉시 전송, 방어자 화면에 즉시 스폰)
    ATTACK_END,          // 공격 종료 → 방어 시작 트리거 (attackerPlayerId + attackStartDspTime + penalties)
    JUDGMENT,            // 방어자 노트 판정 결과 실시간 전송 → 공격자 화면 동기화 (noteId + judgment)
    DEFENSE_END,         // 방어 종료 → 다음 공격 시작 트리거 (missCount)
}
