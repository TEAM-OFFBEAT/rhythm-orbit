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
    [SerializeField] private Button goTutorialButton;

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
        StopGameplayBgmOnLobbyEnter();

        // 씬 재로드 시 Singleton이 씬의 NetworkManager를 즉시 Destroy한다.
        // 실제 지속 인스턴스(DontDestroyOnLoad)를 참조해야 한다.
        if (NetworkManager.Instance != null)
            networkManager = NetworkManager.Instance;

        createRoomButton.onClick.AddListener(OnCreateRoom);
        joinRoomButton.onClick.AddListener(OnJoinRoom);
        goTutorialButton.onClick.AddListener(GoToTutorial);
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

    /// <summary>
    /// 로비 씬에 진입했을 때 게임 플레이 BGM을 정리한다.
    /// 로비 전용 BGM은 추후 이 처리 이후에 따로 재생하면 된다.
    /// 효과음은 별개로 두기 위해 StopAllSfx는 호출하지 않는다.
    /// </summary>
    private void StopGameplayBgmOnLobbyEnter()
    {
        SoundManager.Instance?.StopBgm();
        SoundManager.Instance?.PlayBgm(BgmId.Lobby);

        Debug.Log("LobbyController: 게임 플레이 BGM 정리 완료.");
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
        int sharedSeed = new System.Random().Next();

        // Host = P1, Guest = P2, 첫 공격자 = P1
        networkManager.SetSessionData(localPlayerId: 1, firstAttackerId: 1,
            localGameStartDspTime: hostDspTime, sharedSeed: sharedSeed);
        networkManager.Send(w => PacketSerializer.WriteGameStart(w,
            myPlayerId: 2, firstAttackerId: 1,
            clockOffset: clockOffset,
            gameStartDspTime: hostDspTime,
            sharedSeed: sharedSeed));
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
            localGameStartDspTime: guestGameStartDspTime,
            sharedSeed:           packet.sharedSeed);
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

    public void GoToTutorial()
    {
        NetworkManager.Instance?.Disconnect();

        Time.timeScale = 1f;
        SceneManager.LoadScene("Tutorial");
    }

    // ── 유틸 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// 현재 PC가 외부 네트워크로 나갈 때 실제로 사용하는 IPv4 주소를 우선 찾는다.
    /// 실패하면 활성화된 Wi-Fi/Ethernet 어댑터의 IPv4 주소를 찾는다.
    /// </summary>
    private static string GetLocalIPAddress()
    {
        // 1순위: 현재 기본 라우팅에 사용되는 로컬 IP 찾기
        // 보통 현재 연결 중인 Wi-Fi/Ethernet의 IPv4가 나옴.
        try
        {
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                socket.Connect("8.8.8.8", 65530);

                if (socket.LocalEndPoint is IPEndPoint endPoint)
                {
                    string ip = endPoint.Address.ToString();

                    if (IsUsableIPv4(ip))
                        return ip;
                }
            }
        }
        catch
        {
            // 아래 fallback으로 이동
        }

        // 2순위: 활성화된 Wi-Fi / Ethernet 어댑터에서 IPv4 찾기
        try
        {
            foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus != OperationalStatus.Up)
                    continue;

                if (networkInterface.NetworkInterfaceType != NetworkInterfaceType.Wireless80211 &&
                    networkInterface.NetworkInterfaceType != NetworkInterfaceType.Ethernet)
                    continue;

                string name = networkInterface.Name.ToLower();
                string description = networkInterface.Description.ToLower();

                // 가상 어댑터 제외
                if (name.Contains("virtual") || description.Contains("virtual") ||
                    name.Contains("vmware") || description.Contains("vmware") ||
                    name.Contains("virtualbox") || description.Contains("virtualbox") ||
                    name.Contains("docker") || description.Contains("docker") ||
                    name.Contains("wsl") || description.Contains("wsl") ||
                    name.Contains("bluetooth") || description.Contains("bluetooth"))
                {
                    continue;
                }

                IPInterfaceProperties properties = networkInterface.GetIPProperties();

                foreach (UnicastIPAddressInformation addressInfo in properties.UnicastAddresses)
                {
                    IPAddress address = addressInfo.Address;

                    if (address.AddressFamily != AddressFamily.InterNetwork)
                        continue;

                    string ip = address.ToString();

                    if (IsUsableIPv4(ip))
                        return ip;
                }
            }
        }
        catch
        {
            // 아래 fallback으로 이동
        }

        return "IP 주소를 찾을 수 없음";
    }

    /// <summary>
    /// 방 코드로 표시하기에 적절한 IPv4인지 확인한다.
    /// </summary>
    private static bool IsUsableIPv4(string ip)
    {
        if (string.IsNullOrEmpty(ip))
            return false;

        if (ip == "127.0.0.1")
            return false;

        if (ip.StartsWith("169.254."))
            return false;

        return true;
    }
}
