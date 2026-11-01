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

    public bool IsOpen => exitPanel != null && exitPanel.activeSelf;

    private bool isQuitting;

    private void Awake()
    {
        exitYesButton?.onClick.AddListener(ConfirmExit);
        exitNoButton?.onClick.AddListener(Close);

        Close();
    }

    private void OnDestroy()
    {
        exitYesButton?.onClick.RemoveListener(ConfirmExit);
        exitNoButton?.onClick.RemoveListener(Close);
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
    /// '아니오' 버튼으로 확인창만 닫는다.
    /// </summary>
    public void Close()
    {
        exitPanel?.SetActive(false);
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
