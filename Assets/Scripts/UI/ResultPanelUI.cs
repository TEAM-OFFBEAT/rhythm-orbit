using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 게임 종료 시 표시되는 결과 패널 전체를 관리한다.
/// Win / Lose / CommunicationSuccess 중 하나의 패널만 활성화하고,
/// 로비 이동 및 다시하기 버튼을 처리한다.
/// </summary>
public class ResultPanelUI : MonoBehaviour
{
    [Header("Result Panels")]
    [SerializeField] private GameObject winPanel;
    [SerializeField] private GameObject losePanel;
    [SerializeField] private GameObject successPanel;

    [Header("Buttons")]
    [SerializeField] private Button[] lobbyButtons;
    [SerializeField] private Button[] replayButtons;

    [Header("Scene")]
    [SerializeField] private string lobbySceneName = "Lobby";
    [SerializeField] private string mainLoopSceneName = "MainLoop";

    private void Awake()
    {
        RegisterButtons();
        HideChildPanels();
    }

    private void OnDestroy()
    {
        UnregisterButtons();
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
                SetActivePanel(successPanel);
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
        if (winPanel != null)
        {
            winPanel.SetActive(false);
        }

        if (losePanel != null)
        {
            losePanel.SetActive(false);
        }

        if (successPanel != null)
        {
            successPanel.SetActive(false);
        }
    }

    private void SetActivePanel(GameObject panel)
    {
        if (panel == null)
        {
            Debug.LogWarning("ResultPanelUI: 표시할 결과 패널이 연결되지 않음.");
            return;
        }

        panel.SetActive(true);
    }

    private void RegisterButtons()
    {
        if (lobbyButtons != null)
        {
            foreach (Button button in lobbyButtons)
            {
                if (button == null)
                {
                    continue;
                }

                button.onClick.AddListener(ReturnToLobby);
            }
        }

        if (replayButtons != null)
        {
            foreach (Button button in replayButtons)
            {
                if (button == null)
                {
                    continue;
                }

                button.onClick.AddListener(RestartGame);
            }
        }
    }

    private void UnregisterButtons()
    {
        if (lobbyButtons != null)
        {
            foreach (Button button in lobbyButtons)
            {
                if (button == null)
                {
                    continue;
                }

                button.onClick.RemoveListener(ReturnToLobby);
            }
        }

        if (replayButtons != null)
        {
            foreach (Button button in replayButtons)
            {
                if (button == null)
                {
                    continue;
                }

                button.onClick.RemoveListener(RestartGame);
            }
        }
    }

    /// <summary>
    /// 현재 네트워크 연결을 정리하고 로비 씬으로 돌아간다.
    /// 결과 패널의 로비 복귀 버튼에서 호출된다.
    /// </summary>
    private void ReturnToLobby()
    {
        NetworkManager.Instance?.Disconnect();

        Time.timeScale = 1f;
        SceneManager.LoadScene(lobbySceneName);
    }

    /// <summary>
    /// 메인 루프 씬을 다시 로드한다.
    /// 결과 패널의 다시하기 버튼에서 호출된다.
    /// </summary>
    private void RestartGame()
    {
        NetworkManager.Instance?.Disconnect();

        Time.timeScale = 1f;
        SceneManager.LoadScene(mainLoopSceneName);
    }
}