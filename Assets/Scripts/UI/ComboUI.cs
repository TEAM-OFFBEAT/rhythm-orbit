using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 현재 턴의 연속 비트 성공 횟수를 숫자 스프라이트로 표시한다.
/// 초기화 상태(combo=0)에서는 numberImage를 비활성화한다.
/// </summary>
public class ComboUI : MonoBehaviour
{
    [SerializeField] private Image numberImage;

    /// <summary>1콤보=인덱스0, 7콤보=인덱스6. 총 7개.</summary>
    [SerializeField] private Sprite[] numberSprites;

    private int currentCombo;

    /// <summary>
    /// 콤보를 지정한 값으로 설정한다. 0 이하면 numberImage를 비활성화한다.
    /// </summary>
    public void SetCombo(int count)
    {
        currentCombo = Mathf.Max(0, count);
        if (numberImage == null) return;

        bool valid = currentCombo > 0 && numberSprites != null && currentCombo <= numberSprites.Length;
        numberImage.enabled = valid;
        if (valid)
            numberImage.sprite = numberSprites[currentCombo - 1];
    }

    /// <summary>현재 콤보를 1 증가시킨다.</summary>
    public void Increment() => SetCombo(currentCombo + 1);

    /// <summary>콤보를 초기화한다. numberImage를 비활성화한다.</summary>
    public void Reset() => SetCombo(0);
}
