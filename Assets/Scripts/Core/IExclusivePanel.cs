/// <summary>
/// 한 번에 하나만 열릴 수 있는 전용 패널 계약.
/// SceneInputRouter의 exclusivePanels 목록에 등록하면
/// 다른 패널이 열려 있는 동안 입력이 자동으로 차단된다.
/// </summary>
public interface IExclusivePanel
{
    bool IsOpen { get; }
    void Close();
}
