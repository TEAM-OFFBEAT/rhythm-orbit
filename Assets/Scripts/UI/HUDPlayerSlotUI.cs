using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 플레이어 한 명의 HUD 슬롯을 표시하는 UI 컴포넌트.
/// 프로필 이미지, 정신력 바, 판정 이미지를 갱신한다.
/// spineProfile44/34가 연결되면 Spine 애니메이션 기반으로 동작하고, 없으면 스프라이트 폴백.
/// </summary>
public class HUDPlayerSlotUI : MonoBehaviour
{
    [Header("Profile - Spine")]
    [SerializeField] private SpineUIPlayer spineProfile44;
    [SerializeField] private SpineUIPlayer spineProfile34;

    [Header("Profile (4/4 Mode)")]
    [SerializeField] private Image profileImage;
    [SerializeField] private Sprite defaultPortrait;
    [SerializeField] private Sprite perfectPortrait;
    [SerializeField] private Sprite goodPortrait;
    [SerializeField] private Sprite missPortrait;
    [SerializeField] private Sprite failPortrait;
    [SerializeField] private Sprite successPortrait;

    [Header("Profile (3/4 Mode)")]
    [SerializeField] private Sprite defaultPortrait34;
    [SerializeField] private Sprite perfectPortrait34;
    [SerializeField] private Sprite goodPortrait34;
    [SerializeField] private Sprite missPortrait34;
    [SerializeField] private Sprite failPortrait34;
    [SerializeField] private Sprite successPortrait34;

    private TimeSignatureMode currentMode;

    private const string SpinePerfect = "perfect";
    private const string SpineGood    = "good";
    private const string SpineMiss    = "miss";
    private const string SpineSuccess = "success";
    private const string SpineFail    = "fail";

    [Header("Sanity")]
    [SerializeField] private Image sanityBarFill;
    [SerializeField] private Sprite sanityBarFillSprite34;
    private Sprite sanityBarFillDefault;

    [Header("Panel")]
    [SerializeField] private Image panelImage;
    [SerializeField] private Sprite defaultPanelSprite;
    [SerializeField] private Sprite highlightPanelSprite;
    [SerializeField] private Color pulseColorMin = new Color(1f, 1f, 1f, 0.5f);
    [SerializeField] private Color pulseColorMax = Color.white;
    [SerializeField] private float pulseSpeed = 2f;
    private Coroutine pulseCoroutine;

    private SpineUIPlayer ActiveSpine =>
        currentMode == TimeSignatureMode.ThreeFour && spineProfile34 != null
            ? spineProfile34
            : spineProfile44;

    private void Awake()
    {
        sanityBarFillDefault = sanityBarFill?.sprite;
        if (spineProfile44 != null) spineProfile44.PlayIdle();
        if (spineProfile34 != null) spineProfile34.PlayIdle();
        SyncSpineVisibility();

        if (ActiveSpine == null)
            SetPortrait(defaultPortrait);

        SetActiveState(false);
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
    /// 박자 모드에 따라 활성 Spine 프로필 또는 스프라이트 세트를 전환한다.
    /// </summary>
    public void SetMode(TimeSignatureMode mode)
    {
        currentMode = mode;
        SyncSpineVisibility();
        ClearJudgment();
        if (sanityBarFill != null && sanityBarFillSprite34 != null)
            sanityBarFill.sprite = mode == TimeSignatureMode.ThreeFour ? sanityBarFillSprite34 : sanityBarFillDefault;
    }

    /// <summary>
    /// 판정 결과에 따라 프로필 이미지를 교체한다.
    /// spineProfile이 연결된 경우 해당 애니메이션을 한 번 재생 후 idle로 복귀한다.
    /// </summary>
    public void ShowJudgment(Judgment judgment)
    {
        if (ActiveSpine != null)
        {
            ActiveSpine.PlayOnce(judgment switch
            {
                Judgment.PERFECT => SpinePerfect,
                Judgment.GOOD    => SpineGood,
                _                => SpineMiss,
            });
            return;
        }
        SetPortrait(judgment switch
        {
            Judgment.PERFECT => GetPortrait(perfectPortrait, perfectPortrait34),
            Judgment.GOOD    => GetPortrait(goodPortrait, goodPortrait34),
            _                => GetPortrait(missPortrait, missPortrait34),
        });
    }

    /// <summary>
    /// 프로필을 기본 상태(idle)로 되돌린다.
    /// </summary>
    public void ClearJudgment()
    {
        if (ActiveSpine != null)
        {
            ActiveSpine.PlayIdle();
            return;
        }
        SetPortrait(GetPortrait(defaultPortrait, defaultPortrait34));
    }

    /// <summary>
    /// 교신 성공(true) 또는 실패(false)에 따라 프로필 이미지를 영구 변경한다.
    /// </summary>
    public void SetGameEndPortrait(bool communicationSuccess)
    {
        if (ActiveSpine != null)
        {
            ActiveSpine.PlayOnce(communicationSuccess ? SpineSuccess : SpineFail);
            return;
        }
        SetPortrait(communicationSuccess
            ? GetPortrait(successPortrait, successPortrait34)
            : GetPortrait(failPortrait, failPortrait34));
    }

    /// <summary>
    /// 이 플레이어가 현재 턴을 소유하면 하이라이트하고,
    /// 턴을 소유하지 않는 슬롯은 필요할 때 어둡게 덮는다.
    /// </summary>
    public void SetActiveState(bool isActive)
    {
        if (panelImage == null) return;

        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
            pulseCoroutine = null;
        }

        panelImage.sprite = isActive ? highlightPanelSprite : defaultPanelSprite;
        panelImage.color = Color.white;

        if (isActive)
            pulseCoroutine = StartCoroutine(PulseColor());
    }

    private void SyncSpineVisibility()
    {
        if (spineProfile44 != null)
            spineProfile44.gameObject.SetActive(currentMode != TimeSignatureMode.ThreeFour || spineProfile34 == null);
        if (spineProfile34 != null)
            spineProfile34.gameObject.SetActive(currentMode == TimeSignatureMode.ThreeFour);
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

    private Sprite GetPortrait(Sprite p44, Sprite p34)
        => currentMode == TimeSignatureMode.ThreeFour ? p34 : p44;

    private void SetPortrait(Sprite sprite)
    {
        if (profileImage == null) return;
        profileImage.sprite = sprite;
        profileImage.enabled = sprite != null;
    }
}
