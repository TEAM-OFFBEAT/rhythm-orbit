using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로비의 게임 종료 확인창을 관리한다.
/// ESC 입력은 SceneInputRouter에서 전달받는다.
/// </summary>
public class LobbyExitUI : MonoBehaviour, IExclusivePanel
{
    [Header("Exit Panel")]
    [SerializeField] private GameObject exitPanel;
    [SerializeField] private Button exitYesButton;
    [SerializeField] private Button exitNoButton;

    [Header("Mode Exit Panel")]
    [SerializeField] private GameObject modeExitPanel;
    [SerializeField] private Button modeExitYesButton;
    [SerializeField] private Button modeExitNoButton;

    public bool IsOpen => (exitPanel != null && exitPanel.activeSelf)
                       || (modeExitPanel != null && modeExitPanel.activeSelf);

    private bool isQuitting;

    private void Awake()
    {
        exitYesButton?.onClick.AddListener(ConfirmExit);
        exitNoButton?.onClick.AddListener(Close);
        modeExitNoButton?.onClick.AddListener(CloseModeExit);

        Close();
        CloseModeExit();
    }

    private void OnDestroy()
    {
        exitYesButton?.onClick.RemoveListener(ConfirmExit);
        exitNoButton?.onClick.RemoveListener(Close);
        modeExitYesButton?.onClick.RemoveAllListeners();
        modeExitNoButton?.onClick.RemoveListener(CloseModeExit);
    }

    /// <summary>
    /// 모드 선택 종료 확인 패널의 '예' 동작을 외부에서 주입한다.
    /// </summary>
    public void SetModeExitConfirmAction(System.Action action)
    {
        modeExitYesButton?.onClick.RemoveAllListeners();
        if (action != null)
            modeExitYesButton?.onClick.AddListener(() => { CloseModeExit(); action(); });
    }

    /// <summary>
    /// ESC 입력으로 확인창을 열거나 닫는다.
    /// </summary>
    public void Toggle()
    {
        if (isQuitting) return;

        if (exitPanel == null)
        {
            Debug.LogWarning("LobbyExitUI: Exit Panel을 연결해주세요.", this);
            return;
        }

        bool shouldOpen = !exitPanel.activeSelf;

        if (shouldOpen)
        {
            exitPanel.transform.SetAsLastSibling();
        }

        exitPanel.SetActive(shouldOpen);
    }

    /// <summary>
    /// 모드 선택 창에서 ESC/취소 버튼으로 종료 확인창을 열거나 닫는다.
    /// </summary>
    public void ToggleModeExit()
    {
        if (isQuitting) return;

        if (modeExitPanel == null)
        {
            Debug.LogWarning("LobbyExitUI: Mode Exit Panel을 연결해주세요.", this);
            return;
        }

        bool shouldOpen = !modeExitPanel.activeSelf;
        if (shouldOpen) modeExitPanel.transform.SetAsLastSibling();
        modeExitPanel.SetActive(shouldOpen);
    }

    /// <summary>
    /// '아니오' 버튼으로 확인창만 닫는다.
    /// </summary>
    public void Close()
    {
        exitPanel?.SetActive(false);
        modeExitPanel?.SetActive(false);
    }

    private void CloseModeExit()
    {
        modeExitPanel?.SetActive(false);
    }

    /// <summary>
    /// '예' 버튼으로 게임을 종료한다.
    /// </summary>
    private void ConfirmExit()
    {
        if (isQuitting) return;

        isQuitting = true;

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
