using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// PlayerInput의 ToggleSettings 입력을 받아 설정창을 열고 닫는다.
/// </summary>
public class SettingsInputRouter : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private SettingsWindowController settingsWindowController;

    /// <summary>
    /// 설정창 토글 입력. Input Action 이름이 ToggleSettings이면 호출된다.
    /// Tab 키로 설정창을 열고 닫는다.
    /// </summary>
    public void OnToggleSettings(InputValue value)
    {
        if (value != null && !value.isPressed)
        {
            return;
        }

        settingsWindowController?.Toggle();
    }
}