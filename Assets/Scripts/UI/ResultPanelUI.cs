using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// 게임 종료 시 표시되는 결과 패널 전체를 관리한다.
/// Win / Lose / CommunicationSuccess 중 하나의 패널만 활성화하고,
/// 로비 이동 및 양쪽 동의 기반 다시하기를 처리한다.
/// </summary>
public class ResultPanelUI : MonoBehaviour
{
    [Header("Result Panels")]
    [FormerlySerializedAs("winPanel")]
    [SerializeField] private GameObject p1WinPanel;
    [FormerlySerializedAs("losePanel")]
    [SerializeField] private GameObject p2WinPanel;
    [SerializeField] private GameObject successPanel;

    [Header("Buttons")]
    [SerializeField] private Button[] lobbyButtons;
    [SerializeField] private Button[] replayButtons;

    [Header("Replay Status")]
    [SerializeField] private TMP_Text[] replayStatusTexts;
    [SerializeField] private string opponentDisconnectedStatusMessage = "상대가 나갔습니다.";
    [SerializeField] private string defaultReplayStatusMessage = "";
    [SerializeField] private string waitingReplayStatusMessage = "상대의 다시하기 선택을 기다리는 중...";
    [SerializeField] private string remoteReadyReplayStatusMessage = "상대가 다시하기를 선택했습니다.";
    [SerializeField] private string bothReadyReplayStatusMessage = "양쪽 모두 다시하기를 선택했습니다.";

    [Header("Scene")]
    [SerializeField] private string lobbySceneName = "Lobby";
    [SerializeField] private string mainLoopSceneName = "MainLoop";

    [Header("Network Replay")]
    [SerializeField, Min(0.1f)] private float networkReplayLeadTime = 1.0f;

    private bool localReplayRequested;
    private bool remoteReplayRequested;
    private bool isOpponentDisconnected;

    private void Awake()
    {
        RegisterButtons();
        SubscribeNetworkEvents();
        HideChildPanels();
        SetReplayStatus(defaultReplayStatusMessage);
    }

    private void OnDestroy()
    {
        UnregisterButtons();
        UnsubscribeNetworkEvents();
    }

    /// <summary>
    /// 결과 타입에 맞는 패널 하나만 표시한다.
    /// GameManager가 게임 종료 결과를 결정한 뒤 호출한다.
    /// </summary>
    public void Show(GameResultType resultType)
    {
        gameObject.SetActive(true);
        HideChildPanels();

        localReplayRequested = false;
        remoteReplayRequested = false;
        isOpponentDisconnected = false;

        SetReplayButtonsInteractable(true);
        SetReplayStatus(defaultReplayStatusMessage);

        switch (resultType)
        {
            case GameResultType.P1Win:
                SetActivePanel(p1WinPanel);
                break;

            case GameResultType.P2Win:
                SetActivePanel(p2WinPanel);
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
        if (p1WinPanel != null) p1WinPanel.SetActive(false);
        if (p2WinPanel != null) p2WinPanel.SetActive(false);
        if (successPanel != null) successPanel.SetActive(false);
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

                button.onClick.AddListener(RequestReplay);
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

                button.onClick.RemoveListener(RequestReplay);
            }
        }
    }

    private void SubscribeNetworkEvents()
    {
        NetworkManager net = NetworkManager.Instance;

        if (net == null)
        {
            return;
        }

        net.OnReplayRequest += HandleReplayRequest;
        net.OnGameStart += HandleNetworkGameStart;
        net.OnDisconnected += HandleNetworkDisconnected;
    }

    private void UnsubscribeNetworkEvents()
    {
        NetworkManager net = NetworkManager.Instance;

        if (net == null)
        {
            return;
        }

        net.OnReplayRequest -= HandleReplayRequest;
        net.OnGameStart -= HandleNetworkGameStart;
        net.OnDisconnected -= HandleNetworkDisconnected;
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
    /// 다시하기 버튼 처리.
    /// 로컬 모드에서는 즉시 재시작하고,
    /// 네트워크 모드에서는 양쪽 플레이어가 모두 다시하기를 눌렀을 때만 Host가 새 GAME_START를 보낸다.
    /// </summary>
    private void RequestReplay()
    {
        if (isOpponentDisconnected)
        {
            SetReplayStatus(opponentDisconnectedStatusMessage);
            return;
        }
        
        if (localReplayRequested)
        {
            return;
        }

        NetworkManager net = NetworkManager.Instance;

        if (net == null || !net.IsConnected)
        {
            RestartLocalGame();
            return;
        }

        localReplayRequested = true;
        SetReplayButtonsInteractable(false);

        byte requesterId = (byte)net.LocalPlayerId;

        net.Send(writer =>
            PacketSerializer.WriteReplayRequest(writer, requesterId)
        );

        if (remoteReplayRequested)
        {
            SetReplayStatus(bothReadyReplayStatusMessage);
        }
        else
        {
            SetReplayStatus(waitingReplayStatusMessage);
        }

        TryStartNetworkReplay();
    }

    private void HandleReplayRequest(ReplayRequestPacket packet)
    {
        if (isOpponentDisconnected)
        {
            return;
        }

        remoteReplayRequested = true;

        if (localReplayRequested)
        {
            SetReplayStatus(bothReadyReplayStatusMessage);
        }
        else
        {
            SetReplayStatus(remoteReadyReplayStatusMessage);
        }

        TryStartNetworkReplay();
    }

    /// <summary>
    /// 양쪽 모두 다시하기를 선택했고, 현재 클라이언트가 Host라면 새 게임 시작 패킷을 전송한다.
    /// Client는 GAME_START 수신을 기다린다.
    /// </summary>
    private void TryStartNetworkReplay()
    {   
        if (isOpponentDisconnected)
        {
            SetReplayStatus(opponentDisconnectedStatusMessage);
            return;
        }
        
        NetworkManager net = NetworkManager.Instance;

        if (net == null || !net.IsConnected)
        {
            return;
        }

        if (!localReplayRequested || !remoteReplayRequested)
        {
            return;
        }

        if (!net.IsHost)
        {
            Debug.Log("ResultPanelUI: 양쪽 다시하기 선택 완료. Client는 Host의 GAME_START를 기다림.");
            return;
        }

        StartNetworkReplayAsHost(net);
    }

    private void RestartLocalGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainLoopSceneName);
    }

    /// <summary>
    /// Host가 새 세션 데이터를 만들고 Guest에게 GAME_START 패킷을 보내 다시 시작한다.
    /// </summary>
    private void StartNetworkReplayAsHost(NetworkManager net)
    {
        double hostGameStartDspTime = AudioSettings.dspTime + networkReplayLeadTime;
        double clockOffset = net.TimeSync != null ? net.TimeSync.ClockOffset : 0.0;
        int sharedSeed = new System.Random().Next();

        net.SetSessionData(
            localPlayerId: 1,
            firstAttackerId: 1,
            localGameStartDspTime: hostGameStartDspTime,
            sharedSeed: sharedSeed
        );

        net.Send(writer =>
            PacketSerializer.WriteGameStart(
                writer,
                myPlayerId: 2,
                firstAttackerId: 1,
                clockOffset: clockOffset,
                gameStartDspTime: hostGameStartDspTime,
                sharedSeed: sharedSeed
            )
        );

        Time.timeScale = 1f;
        SceneManager.LoadScene(mainLoopSceneName);
    }

    /// <summary>
    /// Guest가 Host의 GAME_START 패킷을 받았을 때 MainLoop로 재진입한다.
    /// </summary>
    private void HandleNetworkGameStart(GameStartPacket packet)
    {
        NetworkManager net = NetworkManager.Instance;

        if (net == null)
        {
            return;
        }

        if (net.IsHost)
        {
            return;
        }

        if (net.TimeSync != null)
        {
            net.TimeSync.SetGuestOffset(packet.clockOffset);
        }

        double guestGameStartDspTime = packet.gameStartDspTime + packet.clockOffset;

        net.SetSessionData(
            localPlayerId: packet.myPlayerId,
            firstAttackerId: packet.firstAttackerId,
            localGameStartDspTime: guestGameStartDspTime,
            sharedSeed: packet.sharedSeed
        );

        Time.timeScale = 1f;
        SceneManager.LoadScene(mainLoopSceneName);
    }

    private void SetReplayButtonsInteractable(bool interactable)
    {
        if (replayButtons == null)
        {
            return;
        }

        foreach (Button button in replayButtons)
        {
            if (button == null)
            {
                continue;
            }

            button.interactable = interactable;
        }
    }

    private void SetReplayStatus(string message)
    {
        if (replayStatusTexts == null)
        {
            return;
        }

        foreach (TMP_Text text in replayStatusTexts)
        {
            if (text == null)
            {
                continue;
            }

            text.text = message;
        }
    }

    /// <summary>
    /// 상대가 결과 화면 또는 게임 중 연결을 종료했을 때 호출된다.
    /// 같은 replay 상태 텍스트에 안내를 표시하고 다시하기 버튼을 비활성화한다.
    /// </summary>
    private void HandleNetworkDisconnected()
    {
        isOpponentDisconnected = true;
        remoteReplayRequested = false;

        SetReplayStatus(opponentDisconnectedStatusMessage);
        SetReplayButtonsInteractable(false);

        Debug.Log("ResultPanelUI: 상대 연결 종료 감지. 다시하기 비활성화.");
    }
    
}