using TMPro;
using UnityEngine;

/// <summary>
/// 튜토리얼에서 노트 위에 F/J 키 힌트를 표시한다.
/// 일반 게임 노트에서는 숨겨진 상태로 둔다.
/// </summary>
public class NoteKeyHintView : MonoBehaviour
{
    [SerializeField] private TMP_Text keyText;

    private void Awake()
    {
        Hide();
    }

    private void OnDisable()
    {
        Hide();
    }

    public void Show(string text, Color color)
    {
        if (keyText == null)
            return;

        keyText.text = text;
        keyText.color = color;
        keyText.gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (keyText == null)
            return;

        keyText.text = string.Empty;
        keyText.gameObject.SetActive(false);
    }
}