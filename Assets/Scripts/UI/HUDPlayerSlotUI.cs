using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 플레이어 한 명의 HUD 슬롯을 표시하는 UI 컴포넌트.
/// 프로필 이미지, 정신력 바, 판정 이미지를 갱신한다.
/// </summary>
public class HUDPlayerSlotUI : MonoBehaviour
{
    [Header("Profile")]
    [SerializeField] private Image profileImage;
    [SerializeField] private Sprite defaultPortrait;
    [SerializeField] private Sprite perfectPortrait;
    [SerializeField] private Sprite goodPortrait;
    [SerializeField] private Sprite missPortrait;
    [SerializeField] private Sprite failPortrait;
    [SerializeField] private Sprite successPortrait;

    [Header("Sanity")]
    [SerializeField] private Image sanityBarFill;

    [Header("Panel")]
    [SerializeField] private Image panelImage;
    [SerializeField] private Sprite defaultPanelSprite;
    [SerializeField] private Sprite highlightPanelSprite;
    [SerializeField] private Color pulseColorMin = new Color(1f, 1f, 1f, 0.5f);
    [SerializeField] private Color pulseColorMax = Color.white;
    [SerializeField] private float pulseSpeed = 2f;
    [Header("Inactive Dim")]
    [SerializeField] private Image inactiveDimOverlay;
    [SerializeField, Range(0f, 1f)] private float inactiveDimAlpha = 0.45f;

    private Coroutine pulseCoroutine;

    private void Awake()
    {
        SetPortrait(defaultPortrait);
        SetActiveState(false, dimWhenInactive: false);
    }

    /// <summary>
    /// 정신력 바를 현재/최대 비율로 갱신한다.
    /// </summary>
    public void UpdateSanity(int currentSanity, int maxSanity)
    {
        if (sanityBarFill == null) return;

        int safeMax = Mathf.Max(1, maxSanity);
        int safeCurrent = Mathf.Clamp(currentSanity, 0, safeMax);
        sanityBarFill.fillAmount = (float)safeCurrent / safeMax;
    }

    /// <summary>
    /// 판정 결과에 따라 프로필 이미지를 교체한다.
    /// </summary>
    public void ShowJudgment(Judgment judgment)
    {
        SetPortrait(judgment switch
        {
            Judgment.PERFECT => perfectPortrait,
            Judgment.GOOD    => goodPortrait,
            _                => missPortrait,
        });
    }

    /// <summary>
    /// 프로필을 기본 상태로 되돌린다.
    /// </summary>
    public void ClearJudgment()
    {
        SetPortrait(defaultPortrait);
    }

    /// <summary>
    /// 교신 성공(true) 또는 실패(false)에 따라 프로필 이미지를 영구 변경한다.
    /// </summary>
    public void SetGameEndPortrait(bool communicationSuccess)
    {
        SetPortrait(communicationSuccess ? successPortrait : failPortrait);
    }

    /// <summary>
    /// 이 플레이어가 현재 턴(공격 또는 방어)을 소유하면 하이라이트 스프라이트로 교체하고 발광 펄스를 시작한다.
    /// 그렇지 않으면 기본 패널 이미지로 복원한다.
    /// </summary>
    /// <summary>
    /// 이 플레이어가 현재 턴을 소유하면 하이라이트하고,
    /// 턴을 소유하지 않는 슬롯은 필요할 때 어둡게 덮는다.
    /// </summary>
    public void SetActiveState(bool isActive, bool dimWhenInactive = false)
    {
        if (panelImage == null) return;

        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
            pulseCoroutine = null;
        }

        panelImage.sprite = isActive ? highlightPanelSprite : defaultPanelSprite;
        panelImage.color = Color.white;

        SetDimOverlayVisible(!isActive && dimWhenInactive);

        if (isActive)
        {
            pulseCoroutine = StartCoroutine(PulseColor());
        }
    }


    private void SetDimOverlayVisible(bool visible)
    {
        if (inactiveDimOverlay == null)
        {
            return;
        }

        inactiveDimOverlay.gameObject.SetActive(visible);

        Color color = inactiveDimOverlay.color;
        color.r = 0f;
        color.g = 0f;
        color.b = 0f;
        color.a = visible ? inactiveDimAlpha : 0f;
        inactiveDimOverlay.color = color;

        inactiveDimOverlay.raycastTarget = false;
    }

    private IEnumerator PulseColor()
    {
        while (true)
        {
            float t = (Mathf.Sin(Time.time * pulseSpeed * Mathf.PI) + 1f) * 0.5f;
            panelImage.color = Color.Lerp(pulseColorMin, pulseColorMax, t);
            yield return null;
        }
    }

    private void SetPortrait(Sprite sprite)
    {
        if (profileImage == null) return;
        profileImage.sprite = sprite;
        profileImage.enabled = sprite != null;
    }
}
