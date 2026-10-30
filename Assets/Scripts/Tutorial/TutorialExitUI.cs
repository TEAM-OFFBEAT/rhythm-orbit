using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 튜토리얼의 로비 나가기 확인창을 관리한다.
/// ESC 입력은 SceneInputRouter에서 전달받는다.
/// </summary>
public class TutorialExitUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TutorialManager tutorialManager;

    [Header("Exit Panel")]
    [SerializeField] private GameObject exitPanel;
    [SerializeField] private Button exitYesButton;
    [SerializeField] private Button exitNoButton;

    private bool isLeaving;

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
    /// 메인 루프와 동일하게 진행을 일시정지하지 않는다.
    /// </summary>
    public void Toggle()
    {
        if (isLeaving) return;

        if (exitPanel == null)
        {
            Debug.LogError(
                "TutorialExitUI: Exit Panel을 연결해주세요.", this);
            return;
        }

        bool shouldOpen = !exitPanel.activeSelf;

        if (shouldOpen)
        {
            // 같은 Canvas의 다른 UI보다 앞에 표시한다.
            exitPanel.transform.SetAsLastSibling();
        }

        exitPanel.SetActive(shouldOpen);
    }

    /// <summary>
    /// '아니오' 버튼으로 확인창만 닫는다.
    /// </summary>
    public void Close()
    {
        if (exitPanel != null)
        {
            exitPanel.SetActive(false);
        }
    }

    /// <summary>
    /// '예' 버튼으로 기존 로비 이동 처리를 실행한다.
    /// </summary>
    private void ConfirmExit()
    {
        if (isLeaving) return;

        if (tutorialManager == null)
        {
            Debug.LogError(
                "TutorialExitUI: TutorialManager를 연결해주세요.", this);
            return;
        }

        isLeaving = true;
        tutorialManager.GoToLobby();
    }
}