using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 메인루프 진행 중 재시작/로비 이동 버튼을 관리한다.
/// 재시작은 양쪽 플레이어가 모두 선택했을 때만 진행한다.
/// </summary>
public class MainLoopControlUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameManager gameManager;

    [Header("Buttons")]
    [SerializeField] private Button restartButton;
    [SerializeField] private Button quitToLobbyButton;

    [Header("Status UI")]
    [SerializeField] private GameObject statusRoot;
    [SerializeField] private TMP_Text statusText;

    [Header("Status Messages")]
    [SerializeField] private string defaultStatusMessage = "";
    [SerializeField] private string waitingRestartMessage = "상대의 재시작 선택을 기다리는 중...";
    [SerializeField] private string remoteReadyRestartMessage = "상대가 재시작을 원합니다!";
    [SerializeField] private string bothReadyRestartMessage = "양쪽 모두 재시작을 선택했습니다.";
    [SerializeField] private string opponentDisconnectedMessage = "상대가 나갔습니다.";

    [Header("Scene")]
    [SerializeField] private string lobbySceneName = "Lobby";
    [SerializeField] private string mainLoopSceneName = "MainLoop";

    [Header("Network Restart")]
    [SerializeField, Min(0.1f)] private float networkRestartLeadTime = 1.0f;

    private bool localRestartRequested;
    private bool remoteRestartRequested;
    private bool isRestarting;
    private bool isOpponentDisconnected;

    private void Awake()
    {
        RegisterButtons();
        SubscribeNetworkEvents();
        SetStatus(defaultStatusMessage);
    }

    private void OnDestroy()
    {
        UnregisterButtons();
        UnsubscribeNetworkEvents();
    }

    private void RegisterButtons()
    {
        if (restartButton != null)
        {
            restartButton.onClick.AddListener(RequestRestart);
        }

        if (quitToLobbyButton != null)
        {
            quitToLobbyButton.onClick.AddListener(QuitToLobby);
        }
    }

    private void UnregisterButtons()
    {
        if (restartButton != null)
        {
            restartButton.onClick.RemoveListener(RequestRestart);
        }

        if (quitToLobbyButton != null)
        {
            quitToLobbyButton.onClick.RemoveListener(QuitToLobby);
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
    /// 메인루프 중 재시작 버튼 처리.
    /// 로컬 모드에서는 즉시 재시작하고,
    /// 네트워크 모드에서는 양쪽 플레이어가 모두 눌렀을 때만 재시작한다.
    /// </summary>
    private void RequestRestart()
    {
        if (isRestarting)
        {
            return;
        }

        if (isOpponentDisconnected)
        {
            SetStatus(opponentDisconnectedMessage);
            return;
        }

        if (localRestartRequested)
        {
            return;
        }

        NetworkManager net = NetworkManager.Instance;

        if (net == null || !net.IsConnected)
        {
            RestartLocalMainLoop();
            return;
        }

        localRestartRequested = true;

        if (restartButton != null)
        {
            restartButton.interactable = false;
        }

        byte requesterId = (byte)net.LocalPlayerId;

        net.Send(writer =>
            PacketSerializer.WriteReplayRequest(writer, requesterId)
        );

        if (remoteRestartRequested)
        {
            SetStatus(bothReadyRestartMessage);
        }
        else
        {
            SetStatus(waitingRestartMessage);
        }

        TryStartNetworkRestart();
    }

    private void HandleReplayRequest(ReplayRequestPacket packet)
    {
        if (isRestarting || isOpponentDisconnected)
        {
            return;
        }

        NetworkManager net = NetworkManager.Instance;

        if (net != null && packet.requesterPlayerId == net.LocalPlayerId)
        {
            return;
        }

        remoteRestartRequested = true;

        if (localRestartRequested)
        {
            SetStatus(bothReadyRestartMessage);
        }
        else
        {
            SetStatus(remoteReadyRestartMessage);
        }

        TryStartNetworkRestart();
    }

    /// <summary>
    /// 양쪽 모두 재시작을 선택했고 현재 클라이언트가 Host라면 새 GAME_START를 보낸다.
    /// Guest는 Host의 GAME_START 수신을 기다린다.
    /// </summary>
    private void TryStartNetworkRestart()
    {
        NetworkManager net = NetworkManager.Instance;

        if (net == null || !net.IsConnected)
        {
            return;
        }

        if (!localRestartRequested || !remoteRestartRequested)
        {
            return;
        }

        if (!net.IsHost)
        {
            Debug.Log("MainLoopControlUI: 양쪽 재시작 선택 완료. Client는 Host의 GAME_START를 기다림.");
            return;
        }

        StartNetworkRestartAsHost(net);
    }

    private void StartNetworkRestartAsHost(NetworkManager net)
    {
        double hostGameStartDspTime = AudioSettings.dspTime + networkRestartLeadTime;
        double clockOffset = net.TimeSync != null ? net.TimeSync.ClockOffset : 0.0;
        int sharedSeed = new System.Random().Next();

        int firstAttackerId = 1;
        int remotePlayerId = net.LocalPlayerId == 1 ? 2 : 1;

        net.SetSessionData(
            localPlayerId: net.LocalPlayerId,
            firstAttackerId: firstAttackerId,
            localGameStartDspTime: hostGameStartDspTime,
            sharedSeed: sharedSeed
        );

        net.Send(writer =>
            PacketSerializer.WriteGameStart(
                writer,
                myPlayerId: (byte)remotePlayerId,
                firstAttackerId: (byte)firstAttackerId,
                clockOffset: clockOffset,
                gameStartDspTime: hostGameStartDspTime,
                sharedSeed: sharedSeed
            )
        );

        RestartLocalMainLoop();
    }

    /// <summary>
    /// Guest가 Host의 GAME_START를 받았을 때 메인루프를 다시 로드한다.
    /// </summary>
    private void HandleNetworkGameStart(GameStartPacket packet)
    {
        if (isRestarting)
        {
            return;
        }

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

        RestartLocalMainLoop();
    }

    private void RestartLocalMainLoop()
    {
        isRestarting = true;

        Time.timeScale = 1f;
        gameManager?.StopCurrentMainLoopForSceneChange();

        SceneManager.LoadScene(mainLoopSceneName);
    }

    /// <summary>
    /// 메인루프 중 게임을 중단하고 로비로 돌아간다.
    /// 네트워크 연결은 끊는다.
    /// </summary>
    private void QuitToLobby()
    {
        isRestarting = true;

        Time.timeScale = 1f;
        gameManager?.StopCurrentMainLoopForSceneChange();

        NetworkManager.Instance?.Disconnect();

        SceneManager.LoadScene(lobbySceneName);
    }

    private void HandleNetworkDisconnected()
    {
        if (isRestarting)
        {
            return;
        }

        isOpponentDisconnected = true;
        remoteRestartRequested = false;

        SetStatus(opponentDisconnectedMessage);

        if (restartButton != null)
        {
            restartButton.interactable = false;
        }

        Debug.Log("MainLoopControlUI: 상대 연결 종료 감지.");
    }

    private void SetStatus(string message)
    {
        bool hasMessage = !string.IsNullOrEmpty(message);

        if (statusRoot != null)
        {
            statusRoot.SetActive(hasMessage);
        }

        if (statusText != null)
        {
            statusText.text = message ?? "";
        }
    }
}