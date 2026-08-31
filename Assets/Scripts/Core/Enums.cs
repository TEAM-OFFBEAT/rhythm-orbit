public enum Judgment { PERFECT, GOOD, MISS }
public enum GameState { ATTACK, DEFENSE, TURN_CHANGE, END }
public enum NoteType { HIGH, LOW }
public enum GameResultType { P1Win, P2Win, CommunicationSuccess }
public enum PacketType
{
    PING, PONG,        // 클럭 동기화 — RTT 측정으로 두 기기의 dspTime 오프셋 계산
    GAME_START,        // 클럭 동기화 완료 후 양측 게임 시작 (P1/P2 역할 + firstAttackerId 포함)
    GAME_END,          // 게임 세션 종료
    NOTE_CREATED,      // 공격 노트 실시간 스트리밍 (탭 발생 즉시 전송)
    JUDGMENT,          // 방어 판정 결과 — 공격자 미러뷰 시각 동기화 전용 (정신력 처리 없음)
    SANITY_CHANGE,     // 정신력 변화량 — 공격 패널티 + 방어 미스 수치 동기화
    REPLAY_REQUEST     // 다시하기 요청 -   
}

