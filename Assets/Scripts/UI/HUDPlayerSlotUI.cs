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

    [Header("Sanity")]
    [SerializeField] private Image sanityBarFill;

    [Header("Judgment")]
    [SerializeField] private Image judgmentImage;
    [SerializeField] private Sprite perfectSprite;
    [SerializeField] private Sprite goodSprite;
    [SerializeField] private Sprite missSprite;

    private void Awake()
    {
        ApplyDefaultPortrait();
        ClearJudgment();
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
    /// 판정 결과를 이 슬롯의 판정 이미지에 표시한다.
    /// </summary>
    public void ShowJudgment(Judgment judgment)
    {
        if (judgmentImage == null) return;

        judgmentImage.sprite = judgment switch
        {
            Judgment.PERFECT => perfectSprite,
            Judgment.GOOD    => goodSprite,
            _                => missSprite,
        };
        judgmentImage.gameObject.SetActive(judgmentImage.sprite != null);
    }

    /// <summary>
    /// 판정 이미지를 숨긴다.
    /// </summary>
    public void ClearJudgment()
    {
        if (judgmentImage == null) return;

        judgmentImage.gameObject.SetActive(false);
    }

    /// <summary>
    /// 프로필 이미지를 설정한다. 
    /// </summary>
    private void ApplyDefaultPortrait()
    {
        if (profileImage == null) return;

        if (defaultPortrait != null)
        {
            profileImage.sprite = defaultPortrait;
        }

        profileImage.enabled = profileImage.sprite != null;
    }
}
