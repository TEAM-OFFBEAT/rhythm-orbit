using System.Collections;
using Spine.Unity;
using UnityEngine;
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


    [Header("Ending Panel (공통 중앙 스파인)")]
    [SerializeField] private SpineUIPlayer endingPanelPlayer;
    [SerializeField] private float resultDelaySeconds = 3f;

    [Header("P2 Characters (mode-dependent)")]
    [SerializeField] private SpineUIPlayer p1WinP2_44;   // rymo_p1  (4/4)
    [SerializeField] private SpineUIPlayer p1WinP2_34;   // hamo_p1  (3/4)
    [SerializeField] private SpineUIPlayer p2WinP2_44;   // rymo_p2  (4/4)
    [SerializeField] private SpineUIPlayer p2WinP2_34;   // hamo_p2  (3/4)
    [SerializeField] private SpineUIPlayer successP2_44; // rymo_success (4/4)
    [SerializeField] private SpineUIPlayer successP2_34; // hamo_success (3/4)

    [Header("Scene")]
    [SerializeField] private string lobbySceneName = "Lobby";
    [SerializeField] private string mainLoopSceneName = "MainLoop";

    private bool localReplayRequested;
    private bool remoteReplayRequested;
    private bool isOpponentDisconnected;
    private Coroutine resultSequenceCoroutine;

    private void Awake()
    {
        RegisterButtons();
        SubscribeNetworkEvents();
        HideChildPanels();
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

        if (resultSequenceCoroutine != null)
            StopCoroutine(resultSequenceCoroutine);
        resultSequenceCoroutine = StartCoroutine(PlayResultSequence(resultType));
    }

    private IEnumerator PlayResultSequence(GameResultType resultType)
    {
        yield return new WaitForSeconds(resultDelaySeconds);

        TimeSignatureMode mode = NetworkManager.Instance?.SelectedMode
                               ?? GameManager.LocalTestModeOverride
                               ?? TimeSignatureMode.FourFour;
        bool isThreeFour = mode == TimeSignatureMode.ThreeFour;

        // 1. EndingPanel 활성화 + 즉시 change 재생 (idle 노출 없음)
        if (endingPanelPlayer != null)
        {
            endingPanelPlayer.gameObject.SetActive(true);
            endingPanelPlayer.Play("change", false);
        }

        yield return new WaitForSeconds(GetAnimDuration("change"));

        // change 종료 후: 결과 패널 열기 (EndingPanel 뒤에 렌더링)
        switch (resultType)
        {
            case GameResultType.P1Win:
                SetP2Mode(p1WinP2_44, p1WinP2_34);
                SetActivePanel(p1WinPanel);
                break;
            case GameResultType.P2Win:
                SetP2Mode(p2WinP2_44, p2WinP2_34);
                SetActivePanel(p2WinPanel);
                break;
            default:
                SetP2Mode(successP2_44, successP2_34);
                SetActivePanel(successPanel);
                break;
        }

        // 2. init 재생
        string initAnim = GetInitAnimName(resultType, isThreeFour);
        endingPanelPlayer?.Play(initAnim, false);
        yield return new WaitForSeconds(GetAnimDuration(initAnim));

        // 3. loop 재생 (무한 루프) + 버튼 표시
        endingPanelPlayer?.Play(GetLoopAnimName(resultType, isThreeFour), true);
        SetButtonsActive(true);
    }

    private float GetAnimDuration(string animName)
    {
        if (endingPanelPlayer == null) return 0f;
        var sg = endingPanelPlayer.GetComponent<SkeletonGraphic>();
        if (sg?.skeletonDataAsset == null) return 0f;
        var anim = sg.skeletonDataAsset.GetSkeletonData(false)?.FindAnimation(animName);
        return anim != null ? anim.Duration : 0f;
    }

    private string GetInitAnimName(GameResultType resultType, bool isThreeFour)
    {
        switch (resultType)
        {
            case GameResultType.P1Win: return isThreeFour ? "init_p1_hamo" : "init_p1_rymo";
            case GameResultType.P2Win: return "init_p2";
            default:                   return "init_success";
        }
    }

    private string GetLoopAnimName(GameResultType resultType, bool isThreeFour)
    {
        switch (resultType)
        {
            case GameResultType.P1Win: return isThreeFour ? "loop_p1_hamo" : "loop_p1_rymo";
            case GameResultType.P2Win: return "loop_p2";
            default:                   return "loop_success";
        }
    }

    /// <summary>
    /// 결과 패널 전체를 숨긴다.
    /// 게임 시작 시 초기화 용도로 GameManager가 호출한다.
    /// </summary>
    public void HideAll()
    {
        if (resultSequenceCoroutine != null)
        {
            StopCoroutine(resultSequenceCoroutine);
            resultSequenceCoroutine = null;
        }
        HideChildPanels();
        gameObject.SetActive(false);
    }

    private void SetP2Mode(SpineUIPlayer player44, SpineUIPlayer player34)
    {
        TimeSignatureMode mode = NetworkManager.Instance?.SelectedMode
                               ?? GameManager.LocalTestModeOverride
                               ?? TimeSignatureMode.FourFour;
        bool isThreeFour = mode == TimeSignatureMode.ThreeFour;
        player44?.gameObject.SetActive(!isThreeFour);
        player34?.gameObject.SetActive(isThreeFour);
    }

    private void HideChildPanels()
    {
        if (p1WinPanel != null) p1WinPanel.SetActive(false);
        if (p2WinPanel != null) p2WinPanel.SetActive(false);
        if (successPanel != null) successPanel.SetActive(false);
        endingPanelPlayer?.gameObject.SetActive(false);
        SetButtonsActive(false);
    }

    private void SetButtonsActive(bool active)
    {
        if (lobbyButtons != null)
            foreach (var b in lobbyButtons) b?.gameObject.SetActive(active);
        if (replayButtons != null)
            foreach (var b in replayButtons) b?.gameObject.SetActive(active);
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
        net.OnReturnToLobby += HandleReturnToLobby;
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
        net.OnReturnToLobby -= HandleReturnToLobby;
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
        SceneLoadingScreen.Load(lobbySceneName);
    }

    /// <summary>
    /// 다시하기 버튼 처리.
    /// 로컬 모드에서는 즉시 재시작하고,
    /// 네트워크 모드에서는 양쪽 플레이어가 모두 다시하기를 눌렀을 때만 Host가 새 GAME_START를 보낸다.
    /// </summary>
    private void RequestReplay()
    {
        if (isOpponentDisconnected)
            return;

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

        TryStartNetworkReplay();
    }

    private void HandleReplayRequest(ReplayRequestPacket packet)
    {
        if (!gameObject.activeInHierarchy)
        {
            return;
        }

        if (isOpponentDisconnected)
        {
            return;
        }

        remoteReplayRequested = true;
        TryStartNetworkReplay();
    }

    /// <summary>
    /// 양쪽 모두 다시하기를 선택했고, 현재 클라이언트가 Host라면 새 게임 시작 패킷을 전송한다.
    /// Client는 GAME_START 수신을 기다린다.
    /// </summary>
    private void TryStartNetworkReplay()
    {   
        if (isOpponentDisconnected)
            return;

        NetworkManager net = NetworkManager.Instance;

        if (net == null || !net.IsConnected)
            return;

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
        SceneLoadingScreen.Load(mainLoopSceneName);
    }

    /// <summary>
    /// Host가 양측 다시하기 동의를 확인하고 로비 모드 선택 창으로 복귀 신호를 보낸다.
    /// </summary>
    private void StartNetworkReplayAsHost(NetworkManager net)
    {
        net.Send(PacketSerializer.WriteReturnToLobby);
        Time.timeScale = 1f;
        SceneLoadingScreen.Load(lobbySceneName);
    }

    /// <summary>
    /// Guest가 Host의 RETURN_TO_LOBBY 패킷을 받았을 때 로비로 복귀한다.
    /// </summary>
    private void HandleReturnToLobby()
    {
        if (!gameObject.activeInHierarchy) return;
        Time.timeScale = 1f;
        SceneLoadingScreen.Load(lobbySceneName);
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

    /// <summary>
    /// 상대가 결과 화면 또는 게임 중 연결을 종료했을 때 호출된다.
    /// 같은 replay 상태 텍스트에 안내를 표시하고 다시하기 버튼을 비활성화한다.
    /// </summary>
    private void HandleNetworkDisconnected()
    {
        if (!gameObject.activeInHierarchy)
        {
            return;
        }

        isOpponentDisconnected = true;
        remoteReplayRequested = false;
        SetReplayButtonsInteractable(false);

        Debug.Log("ResultPanelUI: 상대 연결 종료 감지. 다시하기 비활성화.");
    }
    
}