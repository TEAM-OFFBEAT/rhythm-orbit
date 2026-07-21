using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

/// <summary>
/// TCP P2P 연결 관리, 패킷 수신 스레드, 메인 스레드 디스패치를 담당한다.
///
/// 연결 방식:
///   - Host: StartHost() → 클라이언트 연결 대기 → OnConnected 이벤트
///   - Guest: Connect(ip) → 호스트에 연결 → OnConnected 이벤트
///
/// 수신 루프는 별도 스레드에서 동작하며, 파싱된 이벤트는
/// ConcurrentQueue를 통해 Unity 메인 스레드(Update)에서 발행한다.
/// </summary>
public class NetworkManager : Singleton<NetworkManager>
{
    [SerializeField] private int port = 7777;

    public bool IsHost { get; private set; }
    public bool IsConnected => stream != null;

    /// <summary>
    /// 클럭 동기화 매니저. OnConnected 이후 사용 가능.
    /// </summary>
    public TimeSyncManager TimeSync { get; private set; }

    /// <summary>로컬 플레이어 ID (1 or 2). LobbyController가 씬 전환 전 SetSessionData()로 설정.</summary>
    public int LocalPlayerId { get; private set; } = 1;

    /// <summary>첫 번째 공격자 ID (1 or 2). LobbyController가 씬 전환 전 SetSessionData()로 설정.</summary>
    public int FirstAttackerId { get; private set; } = 1;

    /// <summary>로컬 기준 게임 시작 dspTime. LobbyController가 씬 전환 전 SetSessionData()로 설정.</summary>
    public double LocalGameStartDspTime { get; private set; }

    // ── 수신 이벤트 ──────────────────────────────────────────────────────────
    public event Action OnConnected;
    public event Action<GameStartPacket> OnGameStart;
    public event Action OnGameEnd;
    public event Action<AttackStartPacket> OnAttackStart;
    public event Action<NoteCreatedPacket> OnNoteCreated;
    public event Action<AttackEndPacket> OnAttackEnd;
    public event Action<JudgmentPacket> OnJudgmentReceived;
    public event Action<DefenseEndPacket> OnDefenseEnd;

    private readonly ConcurrentQueue<Action> mainThreadQueue = new();
    private TcpListener listener;
    private TcpClient client;
    private NetworkStream stream;
    private BinaryReader reader;
    private BinaryWriter writer;
    private Thread receiveThread;
    private bool running;

    // ── 연결 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Host 모드 시작. 클라이언트 연결을 비동기로 대기한다.
    /// </summary>
    public void StartHost()
    {
        IsHost = true;
        TimeSync = new TimeSyncManager(5, Send);
        listener = new TcpListener(IPAddress.Any, port);
        listener.Start();
        listener.BeginAcceptTcpClient(OnClientAccepted, null);
        Debug.Log($"NetworkManager: Host 대기 중 (port {port})");
    }

    /// <summary>
    /// Guest 모드. 지정 IP의 Host에 비동기로 연결을 시도한다.
    /// </summary>
    public void Connect(string ip)
    {
        IsHost = false;
        TimeSync = new TimeSyncManager(5, Send);
        client = new TcpClient();
        client.BeginConnect(ip, port, OnConnectResult, null);
        Debug.Log($"NetworkManager: {ip}:{port} 연결 시도");
    }

    /// <summary>
    /// 연결을 종료하고 리소스를 해제한다.
    /// </summary>
    public void Disconnect()
    {
        running = false;
        receiveThread?.Abort();
        stream?.Close();
        client?.Close();
        listener?.Stop();
        stream = null;
        writer = null;
        reader = null;
        Debug.Log("NetworkManager: 연결 종료");
    }

    /// <summary>
    /// LobbyController가 씬 전환 직전에 호출해 세션 데이터를 저장한다.
    /// GameManager는 Awake()에서 NetworkManager.Instance로 이 값을 읽는다.
    /// </summary>
    public void SetSessionData(int localPlayerId, int firstAttackerId)
    {
        LocalPlayerId = localPlayerId;
        FirstAttackerId = firstAttackerId;
    }

    /// <summary>
    /// LobbyController가 씬 전환 직전에 호출해 세션 데이터와 게임 시작 dspTime을 저장한다.
    /// </summary>
    public void SetSessionData(int localPlayerId, int firstAttackerId, double localGameStartDspTime)
    {
        LocalPlayerId = localPlayerId;
        FirstAttackerId = firstAttackerId;
        LocalGameStartDspTime = localGameStartDspTime;
    }

    // ── 송신 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// 패킷 전송. write 람다 안에서 BinaryWriter로 직접 작성한다.
    /// 수신 스레드와 동시 접근을 막기 위해 writer를 lock으로 보호한다.
    /// </summary>
    public void Send(Action<BinaryWriter> write)
    {
        if (writer == null) return;
        lock (writer)
        {
            try
            {
                write(writer);
                writer.Flush();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"NetworkManager: 전송 실패 — {e.Message}");
            }
        }
    }

    // ── Unity 생명주기 ────────────────────────────────────────────────────────

    private void Update()
    {
        // 수신 스레드에서 쌓인 이벤트를 메인 스레드에서 순서대로 발행
        while (mainThreadQueue.TryDequeue(out var action))
            action?.Invoke();
    }

    protected override void OnDestroy()
    {
        Disconnect();
        base.OnDestroy();
    }

    // ── 내부 연결 처리 ────────────────────────────────────────────────────────

    private void OnClientAccepted(IAsyncResult ar)
    {
        client = listener.EndAcceptTcpClient(ar);
        InitStreams();
        mainThreadQueue.Enqueue(() => OnConnected?.Invoke());
    }

    private void OnConnectResult(IAsyncResult ar)
    {
        try
        {
            client.EndConnect(ar);
            InitStreams();
            mainThreadQueue.Enqueue(() => OnConnected?.Invoke());
        }
        catch (Exception e)
        {
            Debug.LogError($"NetworkManager: 연결 실패 — {e.Message}");
        }
    }

    private void InitStreams()
    {
        client.NoDelay = true; // Nagle 알고리즘 비활성화 — 소형 패킷(PING/PONG 등) 즉시 전송
        stream = client.GetStream();
        reader = new BinaryReader(stream);
        writer = new BinaryWriter(stream);
        running = true;
        receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
        receiveThread.Start();
        Debug.Log("NetworkManager: 연결됨");
    }

    // ── 수신 루프 (별도 스레드) ───────────────────────────────────────────────

    private void ReceiveLoop()
    {
        while (running)
        {
            try
            {
                PacketType type = PacketSerializer.PeekType(reader);
                switch (type)
                {
                    case PacketType.PING:
                        mainThreadQueue.Enqueue(() => TimeSync.OnPing());
                        break;

                    case PacketType.PONG:
                        double pongTime = PacketSerializer.ReadPong(reader);
                        mainThreadQueue.Enqueue(() => TimeSync.OnPong(pongTime));
                        break;

                    case PacketType.GAME_START:
                        var gs = PacketSerializer.ReadGameStart(reader);
                        mainThreadQueue.Enqueue(() => OnGameStart?.Invoke(gs));
                        break;

                    case PacketType.GAME_END:
                        mainThreadQueue.Enqueue(() => OnGameEnd?.Invoke());
                        break;

                    case PacketType.ATTACK_START:
                        var ast = PacketSerializer.ReadAttackStart(reader);
                        mainThreadQueue.Enqueue(() => OnAttackStart?.Invoke(ast));
                        break;

                    case PacketType.NOTE_CREATED:
                        var nc = PacketSerializer.ReadNoteCreated(reader);
                        mainThreadQueue.Enqueue(() => OnNoteCreated?.Invoke(nc));
                        break;

                    case PacketType.ATTACK_END:
                        var ae = PacketSerializer.ReadAttackEnd(reader);
                        mainThreadQueue.Enqueue(() => OnAttackEnd?.Invoke(ae));
                        break;

                    case PacketType.JUDGMENT:
                        var jp = PacketSerializer.ReadJudgment(reader);
                        mainThreadQueue.Enqueue(() => OnJudgmentReceived?.Invoke(jp));
                        break;

                    case PacketType.DEFENSE_END:
                        var de = PacketSerializer.ReadDefenseEnd(reader);
                        mainThreadQueue.Enqueue(() => OnDefenseEnd?.Invoke(de));
                        break;

                    default:
                        Debug.LogWarning($"NetworkManager: 알 수 없는 패킷 타입 {type}");
                        break;
                }
            }
            catch (Exception e)
            {
                if (running)
                    Debug.LogWarning($"NetworkManager: 수신 오류 — {e.Message}");
                break;
            }
        }
    }
}
