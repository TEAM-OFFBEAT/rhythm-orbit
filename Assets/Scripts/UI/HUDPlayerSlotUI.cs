using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 플레이어 한 명의 HUD 슬롯을 표시하는 UI 컴포넌트.
/// 이름, Host/Client 역할, 프로필 이미지, 정신력, 판정 라벨을 갱신한다.
/// </summary>
public class HUDPlayerSlotUI : MonoBehaviour
{
    [Header("Profile")]
    [SerializeField] private Image profileImage;
    [SerializeField] private Sprite defaultPortrait;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text roleText;

    [Header("Role Display")]
    [SerializeField] private bool showGuestRole = true;

    [Header("Sanity")]
    [SerializeField] private TMP_Text sanityText;
    [SerializeField] private Slider sanitySlider;

    [Header("Judgment")]
    [SerializeField] private TMP_Text judgmentLabel;

    private void Awake()
    {
        ApplyDefaultPortrait();
        ClearJudgment();
    }

    /// <summary>
    /// 이 슬롯에 표시할 플레이어 이름을 설정한다.
    /// </summary>
    public void SetPlayerName(string playerName)
    {
        if (nameText == null) return;

        nameText.text = string.IsNullOrEmpty(playerName) ? "Player" : playerName;
    }

    /// <summary>
    /// 이 슬롯의 네트워크 역할 표시를 설정한다.
    /// isHost가 true면 Host, false면 Guest로 표시한다.
    /// </summary>
    public void SetHostRole(bool isHost)
    {
        if (roleText == null) return;

        if (isHost)
        {
            roleText.text = "Host";
            roleText.gameObject.SetActive(true);
        }
        else
        {
            roleText.text = "Guest";
            roleText.gameObject.SetActive(showGuestRole);
        }
    }

    /// <summary>
    /// 정신력 텍스트와 슬라이더를 갱신한다.
    /// </summary>
    public void UpdateSanity(int currentSanity, int maxSanity)
    {
        int safeMax = Mathf.Max(1, maxSanity);
        int safeCurrent = Mathf.Clamp(currentSanity, 0, safeMax);

        if (sanityText != null)
        {
            sanityText.text = $"{safeCurrent} / {safeMax}";
        }

        if (sanitySlider != null)
        {
            sanitySlider.maxValue = safeMax;
            sanitySlider.value = safeCurrent;
        }
    }

    /// <summary>
    /// 판정 결과를 이 슬롯의 판정 라벨에 표시한다.
    /// </summary>
    public void ShowJudgment(Judgment judgment)
    {
        if (judgmentLabel == null) return;

        judgmentLabel.text = judgment.ToString();
        judgmentLabel.gameObject.SetActive(true);
    }

    /// <summary>
    /// 판정 라벨을 비운다.
    /// </summary>
    public void ClearJudgment()
    {
        if (judgmentLabel == null) return;

        judgmentLabel.text = string.Empty;
        judgmentLabel.gameObject.SetActive(false);
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