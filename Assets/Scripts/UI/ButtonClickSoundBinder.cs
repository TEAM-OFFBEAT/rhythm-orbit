using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 하위 오브젝트의 모든 Button에 공통 클릭 효과음을 연결한다.
/// 로비, 메인 HUD, 결과 패널 같은 UI Root 또는 Canvas에 붙여 사용한다.
/// </summary>
public class ButtonClickSoundBinder : MonoBehaviour
{
    [Header("Binding")]
    [SerializeField] private bool includeInactiveButtons = true;

    private readonly List<Button> boundButtons = new();

    private void OnEnable()
    {
        BindButtons();
    }

    private void OnDisable()
    {
        UnbindButtons();
    }

    /// <summary>
    /// 인스펙터 Context Menu에서 버튼 바인딩을 수동 갱신할 때 사용한다.
    /// 런타임에 동적으로 버튼이 생성되는 경우에도 호출 가능하다.
    /// </summary>
    [ContextMenu("Refresh Button Bindings")]
    public void Refresh()
    {
        UnbindButtons();
        BindButtons();
    }

    /// <summary>
    /// 현재 오브젝트의 자식 Button들을 찾아 공통 클릭 효과음 리스너를 등록한다.
    /// 이미 바인딩된 버튼은 중복 등록하지 않는다.
    /// </summary>
    private void BindButtons()
    {
        Button[] buttons = GetComponentsInChildren<Button>(includeInactiveButtons);

        foreach (Button button in buttons)
        {
            if (button == null) continue;
            if (boundButtons.Contains(button)) continue;

            button.onClick.AddListener(PlayButtonClickSfx);
            boundButtons.Add(button);
        }
    }

    /// <summary>
    /// 등록했던 버튼 클릭 효과음 리스너를 제거한다.
    /// 씬 전환이나 오브젝트 비활성화 시 중복 재생을 방지한다.
    /// </summary>
    private void UnbindButtons()
    {
        foreach (Button button in boundButtons)
        {
            if (button == null) continue;
            button.onClick.RemoveListener(PlayButtonClickSfx);
        }

        boundButtons.Clear();
    }

    /// <summary>
    /// 버튼 클릭 시 공통 ButtonClick 효과음을 재생한다.
    /// SoundManager가 없는 상황에서는 아무것도 하지 않는다.
    /// </summary>
    private void PlayButtonClickSfx()
    {
        SoundManager.Instance?.PlaySfx(SfxId.ButtonClick);
    }
}