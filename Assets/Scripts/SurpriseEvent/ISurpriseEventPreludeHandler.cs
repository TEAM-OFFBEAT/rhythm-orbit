/// <summary>
/// 기습 이벤트 중 적용 페이즈 이전부터 선행 연출이 필요한 이벤트가 구현한다.
/// 예: DEF_02 음향 수신의 공격턴 시작 암전 연출.
/// </summary>
public interface ISurpriseEventPreludeHandler
{
    void BeginPrelude(SurpriseEventContext context);
    void EndPrelude(SurpriseEventContext context);
}