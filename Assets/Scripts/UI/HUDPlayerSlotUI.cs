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

    [Header("Judgment")]
    [SerializeField] private Image judgmentImage;
    [SerializeField] private Sprite perfectSprite;
    [SerializeField] private Sprite goodSprite;
    [SerializeField] private Sprite missSprite;

    private void Awake()
    {
        SetPortrait(defaultPortrait);
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
    /// 판정 결과를 이 슬롯의 판정 이미지 및 프로필 이미지에 표시한다.
    /// </summary>
    public void ShowJudgment(Judgment judgment)
    {
        if (judgmentImage != null)
        {
            judgmentImage.sprite = judgment switch
            {
                Judgment.PERFECT => perfectSprite,
                Judgment.GOOD    => goodSprite,
                _                => missSprite,
            };
            judgmentImage.gameObject.SetActive(judgmentImage.sprite != null);
        }

        SetPortrait(judgment switch
        {
            Judgment.PERFECT => perfectPortrait,
            Judgment.GOOD    => goodPortrait,
            _                => missPortrait,
        });
    }

    /// <summary>
    /// 판정 이미지를 숨기고 프로필을 기본 상태로 되돌린다.
    /// </summary>
    public void ClearJudgment()
    {
        if (judgmentImage != null)
            judgmentImage.gameObject.SetActive(false);

        SetPortrait(defaultPortrait);
    }

    /// <summary>
    /// 교신 성공(true) 또는 실패(false)에 따라 프로필 이미지를 영구 변경한다.
    /// </summary>
    public void SetGameEndPortrait(bool communicationSuccess)
    {
        SetPortrait(communicationSuccess ? successPortrait : failPortrait);
    }

    private void SetPortrait(Sprite sprite)
    {
        if (profileImage == null) return;
        profileImage.sprite = sprite;
        profileImage.enabled = sprite != null;
    }
}
