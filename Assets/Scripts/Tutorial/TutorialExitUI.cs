using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 튜토리얼의 나가기 창을 관리한다.
/// 진행 중에는 예/계속하기, 완료 후에는 예/다시하기로 동작한다.
/// </summary>
public class TutorialExitUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TutorialManager tutorialManager;

    [Header("Exit Panel")]
    [SerializeField] private GameObject exitPanel;
    [SerializeField] private Button exitYesButton;
    [SerializeField] private Button exitNoButton;
    [SerializeField] private TMP_Text exitNoButtonLabel;

    private bool isLeaving;

    private void Awake()
    {
        if (exitNoButtonLabel == null && exitNoButton != null)
        {
            exitNoButtonLabel =
                exitNoButton.GetComponentInChildren<TMP_Text>(true);
        }

        exitYesButton?.onClick.AddListener(ConfirmExit);
        exitNoButton?.onClick.AddListener(HandleNoButton);

        RefreshButtons();
        Close();
    }

    private void OnDestroy()
    {
        exitYesButton?.onClick.RemoveListener(ConfirmExit);
        exitNoButton?.onClick.RemoveListener(HandleNoButton);
    }

    public void Toggle()
    {
        if (isLeaving) return;

        if (exitPanel != null && exitPanel.activeSelf)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    /// <summary>
    /// 완료 후 자동 표시와 ESC 입력이 함께 사용하는 열기 함수.
    /// </summary>
    public void Open()
    {
        if (isLeaving) return;

        if (exitPanel == null)
        {
            Debug.LogError(
                "TutorialExitUI: Exit Panel을 연결해주세요.", this);
            return;
        }

        RefreshButtons();

        exitPanel.transform.SetAsLastSibling();
        exitPanel.SetActive(true);
    }

    public void Close()
    {
        if (exitPanel != null)
        {
            exitPanel.SetActive(false);
        }
    }

    /// <summary>
    /// 창의 활성 상태는 유지하면서 두 번째 버튼의 문구만 갱신한다.
    /// </summary>
    public void RefreshButtons()
    {
        if (exitNoButtonLabel == null) return;

        bool completed = tutorialManager != null &&
            tutorialManager.IsTutorialCompleted;

        exitNoButtonLabel.text = completed
            ? "다시하기"
            : "계속하기";
    }

    private void HandleNoButton()
    {
        if (isLeaving) return;

        if (tutorialManager != null &&
            tutorialManager.IsTutorialCompleted)
        {
            ConfirmRestart();
        }
        else
        {
            Close();
        }
    }

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

    private void ConfirmRestart()
    {
        if (isLeaving) return;

        if (tutorialManager == null)
        {
            Debug.LogError(
                "TutorialExitUI: TutorialManager를 연결해주세요.", this);
            return;
        }

        isLeaving = true;
        tutorialManager.RestartTutorial();
    }
}