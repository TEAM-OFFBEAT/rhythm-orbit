using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 플레이어 한 명의 HUD 슬롯을 표시하는 UI 컴포넌트.
/// 프로필 이미지, 정신력 바, 판정 이미지를 갱신한다.
/// spineProfile이 연결되면 Spine 애니메이션 기반으로 동작하고, 없으면 스프라이트 폴백.
/// </summary>
public class HUDPlayerSlotUI : MonoBehaviour
{
    [Header("Profile - Spine (P1)")]
    [SerializeField] private SpineUIPlayer spineProfile;

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

    private const string SpineIdle    = "default_2";
    private const string SpinePerfect = "perfect";
    private const string SpineGood    = "good";
    private const string SpineMiss    = "miss";
    private const string SpineSuccess = "success";
    private const string SpineFail    = "fail";

    [Header("Sanity")]
    [SerializeField] private Image sanityBarFill;

    [Header("Panel")]
    [SerializeField] private Image panelImage;
    [SerializeField] private Sprite defaultPanelSprite;
    [SerializeField] private Sprite highlightPanelSprite;
    [SerializeField] private Color pulseColorMin = new Color(1f, 1f, 1f, 0.5f);
    [SerializeField] private Color pulseColorMax = Color.white;
    [SerializeField] private float pulseSpeed = 2f;
    private Coroutine pulseCoroutine;

    private void Awake()
    {
        if (spineProfile != null)
            spineProfile.Play(SpineIdle, true);
        else
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
    /// 박자 모드에 따라 활성 스프라이트 세트를 전환한다. P1은 모드 무관(3/4 슬롯 비워두면 4/4 폴백).
    /// </summary>
    public void SetMode(TimeSignatureMode mode)
    {
        currentMode = mode;
        ClearJudgment();
    }

    /// <summary>
    /// 판정 결과에 따라 프로필 이미지를 교체한다.
    /// spineProfile이 연결된 경우 해당 애니메이션을 한 번 재생 후 idle로 복귀한다.
    /// </summary>
    public void ShowJudgment(Judgment judgment)
    {
        if (spineProfile != null)
        {
            PlaySpineOnce(judgment switch
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
        if (spineProfile != null)
        {
            spineProfile.Play(SpineIdle, true);
            return;
        }
        SetPortrait(GetPortrait(defaultPortrait, defaultPortrait34));
    }

    /// <summary>
    /// 교신 성공(true) 또는 실패(false)에 따라 프로필 이미지를 영구 변경한다.
    /// </summary>
    public void SetGameEndPortrait(bool communicationSuccess)
    {
        if (spineProfile != null)
        {
            PlaySpineOnce(communicationSuccess ? SpineSuccess : SpineFail);
            return;
        }
        SetPortrait(communicationSuccess
            ? GetPortrait(successPortrait, successPortrait34)
            : GetPortrait(failPortrait, failPortrait34));
    }

    private void PlaySpineOnce(string animName)
    {
        spineProfile.Play(animName, false);
        spineProfile.AnimationState.AddAnimation(0, SpineIdle, true, 0f);
    }

    /// <summary>
    /// 이 플레이어가 현재 턴(공격 또는 방어)을 소유하면 하이라이트 스프라이트로 교체하고 발광 펄스를 시작한다.
    /// 그렇지 않으면 기본 패널 이미지로 복원한다.
    /// </summary>
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
        {
            pulseCoroutine = StartCoroutine(PulseColor());
        }
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
