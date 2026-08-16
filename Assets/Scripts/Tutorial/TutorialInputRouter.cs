using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 튜토리얼 씬 입력을 TutorialManager로 전달한다.
/// PlayerInput SendMessages 방식이면 PlayerInput과 같은 GameObject에 붙인다.
/// UI 버튼 OnClick 대상도 이 스크립트로 연결한다.
/// </summary>
public class TutorialInputRouter : MonoBehaviour
{
    [SerializeField] private TutorialManager tutorialManager;
    [SerializeField] private bool logInput = true;

    public void OnTapHigh()
    {
        if (logInput) Debug.Log("TutorialInputRouter: HIGH 입력");
        tutorialManager?.HandleTap(NoteType.HIGH);
    }

    public void OnTapLow()
    {
        if (logInput) Debug.Log("TutorialInputRouter: LOW 입력");
        tutorialManager?.HandleTap(NoteType.LOW);
    }

    public void OnTapHigh(InputValue value)
    {
        if (!IsPressed(value)) return;
        OnTapHigh();
    }

    public void OnTapLow(InputValue value)
    {
        if (!IsPressed(value)) return;
        OnTapLow();
    }

    private bool IsPressed(InputValue value)
    {
        if (value == null) return true;

        try
        {
            return value.isPressed;
        }
        catch
        {
            return true;
        }
    }
}