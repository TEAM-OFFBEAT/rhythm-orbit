using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 한 씬의 키 입력을 받아 적절한 기능으로 전달한다.
/// 씬마다 활성화된 컴포넌트를 하나만 둔다.
///
/// 입력 우선순위:
/// 설정창 → 현재 씬의 튜토리얼 또는 게임 입력.
///
/// 기존 PlayerInput의 Send Messages 대신
/// 사용할 액션의 performed 이벤트를 명시적으로 구독한다.
/// </summary>
[DisallowMultipleComponent]
public class SceneInputRouter : MonoBehaviour
{
    public enum SceneMode
    {
        Lobby,
        MainLoop,
        Tutorial
    }

    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private SceneMode sceneMode;
    [SerializeField] private bool logInput;

    [Header("Targets")]
    [SerializeField] private SettingsWindowController settingsWindowController;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private TutorialManager tutorialManager;
    [SerializeField] private MainLoopControlUI mainLoopControlUI;

    // 씬마다 독립된 액션 인스턴스를 사용한다.
    // 다른 씬이나 원본 에셋의 활성 상태를 변경하지 않는다.
    private InputActionAsset runtimeActions;

    private readonly List<(
        InputAction action,
        Action<InputAction.CallbackContext> handler
    )> bindings = new();

    private void OnEnable()
    {
        if (inputActions == null)
        {
            Debug.LogError(
                "SceneInputRouter: Input Actions를 연결해주세요.", this);
            return;
        }

        runtimeActions = Instantiate(inputActions);

        // ToggleSettings가 두 맵에 있지만 UI 맵의 액션만 사용한다.
        // 같은 Tab 입력을 중복 처리하지 않도록 경로를 명시한다.
        Bind("UI/ToggleSettings", HandleToggleSettings);
        Bind("UI/ToggleEscape", HandleEscape);
        Bind("GameLoop/TapHigh", HandleHigh);
        Bind("GameLoop/TapLow", HandleLow);
        Bind("GameLoop/AdvanceDialogue", HandleAdvanceDialogue);

        ValidateTargets();
    }

    private void OnDisable()
    {
        // 구독과 활성화를 함께 해제해 씬 재진입 시 중복을 방지한다.
        foreach (var binding in bindings)
        {
            binding.action.performed -= binding.handler;
            binding.action.Disable();
        }

        bindings.Clear();

        if (runtimeActions != null)
        {
            Destroy(runtimeActions);
            runtimeActions = null;
        }
    }

    /// <summary>
    /// 필요한 액션만 찾아 구독하고 활성화한다.
    /// 전체 맵을 켜지 않으므로 사용하지 않는 Tap(anyKey) 등은 실행되지 않는다.
    /// </summary>
    private void Bind(
        string actionPath,
        Action<InputAction.CallbackContext> handler)
    {
        InputAction action = runtimeActions.FindAction(actionPath);

        if (action == null)
        {
            Debug.LogError(
                $"SceneInputRouter: 액션을 찾을 수 없습니다: {actionPath}",
                this);
            return;
        }

        action.performed += handler;
        bindings.Add((action, handler));
        action.Enable();
    }

    private void HandleToggleSettings(InputAction.CallbackContext context)
    {
        if (!context.ReadValueAsButton()) return;

        Trace("Tab → 설정창");

        settingsWindowController?.Toggle();
    }

    private void HandleEscape(InputAction.CallbackContext context)
    {
        if (!context.ReadValueAsButton()) return;

        // ESC 액션이 수신됐는지, 어떤 씬 모드로 처리되는지 확인한다.
        Trace($"ESC 수신 / 모드: {sceneMode}");

        if (settingsWindowController != null &&
            settingsWindowController.IsOpen)
        {
            Trace("ESC → 설정창 닫기");
            settingsWindowController.Close();
            return;
        }

        if (sceneMode != SceneMode.MainLoop)
        {
            return;
        }

        if (mainLoopControlUI == null)
        {
            Debug.LogError(
                "SceneInputRouter: Main Loop Control UI를 연결해주세요.",
                this);
            return;
        }

        Trace("ESC → 메인 루프 나가기 패널 호출");
        mainLoopControlUI.OnToggleEscape();
    }

    private void HandleHigh(InputAction.CallbackContext context)
    {
        if (!context.ReadValueAsButton()) return;

        Trace("HIGH");

        // 설정창이 입력을 소비했다면 게임으로 전달하지 않는다.
        if (settingsWindowController != null &&
            settingsWindowController.TryHandleHighInput())
        {
            return;
        }

        RouteNote(NoteType.HIGH);
    }

    private void HandleLow(InputAction.CallbackContext context)
    {
        if (!context.ReadValueAsButton()) return;

        Trace("LOW");

        if (settingsWindowController != null &&
            settingsWindowController.TryHandleLowInput())
        {
            return;
        }

        RouteNote(NoteType.LOW);
    }

    private void RouteNote(NoteType noteType)
    {
        switch (sceneMode)
        {
            case SceneMode.Tutorial:
                tutorialManager?.HandleTap(noteType);
                break;

            case SceneMode.MainLoop:
                gameManager?.OnTap(noteType);
                break;

            // 로비에서는 설정창이 소비하지 않은 노트 입력을 무시한다.
            case SceneMode.Lobby:
                break;
        }
    }

    private void HandleAdvanceDialogue(
        InputAction.CallbackContext context)
    {
        if (!context.ReadValueAsButton()) return;

        if (settingsWindowController != null &&
            settingsWindowController.IsOpen)
        {
            return;
        }

        if (sceneMode == SceneMode.Tutorial)
        {
            Trace("Space → 튜토리얼 대사");
            tutorialManager?.OnAdvanceDialogue();
        }
    }

    private void ValidateTargets()
    {
        bool missingTarget =
            (sceneMode == SceneMode.Lobby &&
             settingsWindowController == null) ||
            (sceneMode == SceneMode.MainLoop &&
             (gameManager == null || mainLoopControlUI == null)) ||
            (sceneMode == SceneMode.Tutorial &&
             tutorialManager == null);

        if (missingTarget)
        {
            Debug.LogError(
                $"SceneInputRouter: {sceneMode} 씬의 필수 대상을 연결해주세요.",
                this);
        }
    }

    private void Trace(string message)
    {
        if (logInput)
        {
            Debug.Log($"[Input/{sceneMode}] {message}", this);
        }
    }
}