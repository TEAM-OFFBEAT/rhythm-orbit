using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 게임 종료 시 표시되는 결과 패널 전체를 관리하는 UI 컴포넌트.
/// 승리, 패배, 교신 성공 패널 중 하나만 활성화하고 로비 복귀 버튼을 처리한다.
/// </summary>
public class ResultPanelUI : MonoBehaviour
{
    [Header("Result Panels")]
    [SerializeField] private GameObject winPanel;
    [SerializeField] private GameObject losePanel;
    [SerializeField] private GameObject communicationSuccessPanel;

    [Header("Buttons")]
    [SerializeField] private Button[] lobbyButtons;

    [Header("Scene")]
    [SerializeField] private string lobbySceneName = "Lobby";

    private void Awake()
    {
        RegisterLobbyButtons();
        HideAll();
    }

    private void OnDestroy()
    {
        UnregisterLobbyButtons();
    }

    /// <summary>
    /// 결과 타입에 맞는 패널 하나만 표시한다.
    /// GameManager가 게임 종료 결과를 결정한 뒤 호출한다.
    /// </summary>
    public void Show(GameResultType resultType)
    {
        gameObject.SetActive(true);
        HideChildPanels();

        switch (resultType)
        {
            case GameResultType.Win:
                SetActivePanel(winPanel);
                break;

            case GameResultType.Lose:
                SetActivePanel(losePanel);
                break;

            case GameResultType.CommunicationSuccess:
                SetActivePanel(communicationSuccessPanel);
                break;
        }
    }

    /// <summary>
    /// 결과 패널 전체를 숨긴다.
    /// 게임 시작 시 초기화 용도로 GameManager가 호출한다.
    /// </summary>
    public void HideAll()
    {
        HideChildPanels();
        gameObject.SetActive(false);
    }

    private void HideChildPanels()
    {
        if (winPanel != null) winPanel.SetActive(false);
        if (losePanel != null) losePanel.SetActive(false);
        if (communicationSuccessPanel != null) communicationSuccessPanel.SetActive(false);
    }

    private void SetActivePanel(GameObject panel)
    {
        if (panel == null) return;

        panel.SetActive(true);
    }

    private void RegisterLobbyButtons()
    {
        foreach (Button button in lobbyButtons)
        {
            if (button == null) continue;
            button.onClick.AddListener(ReturnToLobby);
        }
    }

    private void UnregisterLobbyButtons()
    {
        foreach (Button button in lobbyButtons)
        {
            if (button == null) continue;
            button.onClick.RemoveListener(ReturnToLobby);
        }
    }

    /// <summary>
    /// 현재 네트워크 연결을 정리하고 로비 씬으로 돌아간다.
    /// 결과 패널의 로비 복귀 버튼에서 호출된다.
    /// </summary>
    private void ReturnToLobby()
    {
        NetworkManager.Instance?.Disconnect();
        SceneManager.LoadScene(lobbySceneName);
    }
}