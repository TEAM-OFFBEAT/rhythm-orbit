using System.Collections;
using UnityEngine;

/// <summary>
/// World Space SpriteRenderer로 판정 결과를 표시하는 컴포넌트.
/// HUD Canvas와 무관하게 동작하며, GameManager가 위치와 판정 결과를 설정한다.
/// </summary>
public class JudgmentLabel : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite perfectSprite;
    [SerializeField] private Sprite goodSprite;
    [SerializeField] private Sprite missSprite;
    [SerializeField] private float displayDuration = 0.6f;

    private Coroutine hideCoroutine;

    private void Awake()
    {
        if (spriteRenderer != null)
            spriteRenderer.enabled = false;
    }

    /// <summary>
    /// 판정 결과에 맞는 스프라이트를 표시하고 displayDuration 후 자동으로 숨긴다.
    /// </summary>
    public void ShowJudgment(Judgment judgment)
    {
        if (spriteRenderer == null) return;
        spriteRenderer.sprite = judgment switch
        {
            Judgment.PERFECT => perfectSprite,
            Judgment.GOOD    => goodSprite,
            _                => missSprite,
        };
        spriteRenderer.enabled = spriteRenderer.sprite != null;

        if (hideCoroutine != null) StopCoroutine(hideCoroutine);
        hideCoroutine = StartCoroutine(HideAfterDelay());
    }

    /// <summary>
    /// 즉시 숨긴다. 턴 전환 시 GameManager가 호출한다.
    /// </summary>
    public void ClearJudgment()
    {
        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }
        if (spriteRenderer != null)
            spriteRenderer.enabled = false;
    }

    /// <summary>
    /// World Space X 좌표를 설정한다. Y, Z는 인스펙터에서 설정한 값을 유지한다.
    /// GameManager가 방어 페이즈 시작 시 judgeLineX 값으로 호출한다.
    /// </summary>
    public void SetWorldX(float worldX)
    {
        var pos = transform.position;
        pos.x = worldX;
        transform.position = pos;
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(displayDuration);
        if (spriteRenderer != null)
            spriteRenderer.enabled = false;
        hideCoroutine = null;
    }
}
