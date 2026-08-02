using System.Net;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Net.NetworkInformation;

/// <summary>
/// 로비 씬의 방 생성/입장 흐름을 제어한다.
/// PING/PONG 클럭 동기화 완료 후 세션 데이터를 NetworkManager에 저장하고 MainLoop 씬으로 전환한다.
/// Host = P1 (첫 공격자), Guest = P2.
/// </summary>
public class LobbyController : MonoBehaviour
{
    [Header("Network")]
    [SerializeField] private NetworkManager networkManager;

    [Header("Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject hostWaitingPanel;
    [SerializeField] private GameObject joinPanel;

    [Header("Main Menu")]
    [SerializeField] private Button createRoomButton;
    [SerializeField] private Button joinRoomButton;

    [Header("Host — 대기 화면")]
    [SerializeField] private TMP_Text roomCodeText;
    [SerializeField] private TMP_Text waitingStatusText;
    [SerializeField] private Button cancelHostButton;

    [Header("Join Room")]
    [SerializeField] private TMP_InputField codeInputField;
    [SerializeField] private Button connectButton;
    [SerializeField] private Button cancelJoinButton;
    [SerializeField] private TMP_Text joinStatusText;

    [SerializeField] private string mainLoopSceneName = "MainLoop";

    private void Awake()
    {
        createRoomButton.onClick.AddListener(OnCreateRoom);
        joinRoomButton.onClick.AddListener(OnJoinRoom);
        cancelHostButton.onClick.AddListener(OnCancel);
        connectButton.onClick.AddListener(OnConnect);
        cancelJoinButton.onClick.AddListener(OnCancel);

        networkManager.OnConnected += HandleConnected;
        networkManager.OnConnectionFailed += HandleConnectionFailed;
        networkManager.OnDisconnected += HandleLobbyDisconnected;
        networkManager.OnGameStart += HandleGameStart;

        ShowMainMenu();
    }

    private void OnDestroy()
    {
        if (networkManager == null) return;
        networkManager.OnConnected -= HandleConnected;
        networkManager.OnConnectionFailed -= HandleConnectionFailed;
        networkManager.OnDisconnected -= HandleLobbyDisconnected;
        networkManager.OnGameStart -= HandleGameStart;
    }

    // ── 버튼 핸들러 ──────────────────────────────────────────────────────────

    private void OnCreateRoom()
    {
        string localIP = GetLocalIPAddress();
        networkManager.StartHost();

        mainMenuPanel.SetActive(false);
        hostWaitingPanel.SetActive(true);
        roomCodeText.text = localIP;
        waitingStatusText.text = "플레이어 대기 중...";
    }

    private void OnJoinRoom()
    {
        mainMenuPanel.SetActive(false);
        joinPanel.SetActive(true);
        joinStatusText.text = "";
    }

    private void OnConnect()
    {
        string ip = codeInputField.text.Trim();
        if (string.IsNullOrEmpty(ip)) return;

        connectButton.interactable = false;
        joinStatusText.text = "연결 중...";
        networkManager.Connect(ip);
    }

    private void OnCancel()
    {
        networkManager?.Disconnect();
        ShowMainMenu();
    }

    // ── 네트워크 이벤트 ──────────────────────────────────────────────────────

    private void HandleConnectionFailed()
    {
        joinStatusText.text = "연결에 실패했습니다. IP 주소를 확인해 주세요.";
        connectButton.interactable = true;
    }

    private void HandleLobbyDisconnected()
    {
        ShowMainMenu();
    }

    private void HandleConnected()
    {
        if (networkManager.IsHost)
        {
            waitingStatusText.text = "클럭 동기화 중...";
            networkManager.TimeSync.StartSync(OnClockSyncComplete);
        }
        else
        {
            joinStatusText.text = "동기화 중... 잠시 기다려 주세요.";
            // Guest: GAME_START 수신까지 대기
        }
    }

    private void OnClockSyncComplete()
    {
        double hostDspTime = AudioSettings.dspTime;
        double clockOffset = networkManager.TimeSync.ClockOffset;

        // Host = P1, Guest = P2, 첫 공격자 = P1
        networkManager.SetSessionData(localPlayerId: 1, firstAttackerId: 1,
            localGameStartDspTime: hostDspTime);
        networkManager.Send(w => PacketSerializer.WriteGameStart(w,
            myPlayerId: 2, firstAttackerId: 1,
            clockOffset: clockOffset,
            gameStartDspTime: hostDspTime));
        LoadMainLoop();
    }

    private void HandleGameStart(GameStartPacket packet)
    {
        // Guest: localOffset = +clockOffset (host→guest 변환)
        networkManager.TimeSync.SetGuestOffset(packet.clockOffset);

        // RhythmClock 동기화 기준: Host의 게임 시작 시각을 Guest 클럭으로 변환
        double guestGameStartDspTime = packet.gameStartDspTime + packet.clockOffset;
        networkManager.SetSessionData(
            localPlayerId:        packet.myPlayerId,
            firstAttackerId:      packet.firstAttackerId,
            localGameStartDspTime: guestGameStartDspTime);
        LoadMainLoop();
    }

    // ── 씬 전환 ──────────────────────────────────────────────────────────────

    private void LoadMainLoop()
    {
        SceneManager.LoadScene(mainLoopSceneName);
    }

    private void ShowMainMenu()
    {
        mainMenuPanel.SetActive(true);
        hostWaitingPanel.SetActive(false);
        joinPanel.SetActive(false);

        connectButton.interactable = true;
    }

    // ── 유틸 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// 같은 LAN에 있는 다른 클라이언트가 접속할 수 있는 IPv4 주소를 찾는다.
    /// 127.0.0.1, 가상 터널, 자동 사설 IP는 제외한다.
    /// </summary>
    private static string GetLocalIPAddress()
    {
        try
        {
            foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus != OperationalStatus.Up)
                    continue;

                if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;

                if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                    continue;

                IPInterfaceProperties properties = networkInterface.GetIPProperties();

                foreach (UnicastIPAddressInformation addressInfo in properties.UnicastAddresses)
                {
                    IPAddress ip = addressInfo.Address;

                    if (ip.AddressFamily != AddressFamily.InterNetwork)
                        continue;

                    if (IPAddress.IsLoopback(ip))
                        continue;

                    string ipText = ip.ToString();

                    // 인터넷 연결 실패 시 Windows가 자동으로 잡는 주소라 방 코드로 부적절함.
                    if (ipText.StartsWith("169.254."))
                        continue;

                    return ipText;
                }
            }
        }
        catch
        {
            // 아래 fallback으로 이동
        }

        return "IP 주소를 찾을 수 없음";
    }
}
