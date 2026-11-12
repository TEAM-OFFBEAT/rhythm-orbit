using UnityEngine;

/// <summary>
/// EVT_ATK_02 펄스 간섭 이벤트 핸들러.
/// 레인 전체를 덮는 펄스 애니메이션 오버레이를 활성화해 시야를 방해한다.
/// 입력 판정·BPM·정신력 규칙은 변경하지 않는다.
/// </summary>
public class Atk02SurpriseEventHandler : MonoBehaviour, ISurpriseEventHandler
{
    [Tooltip("레인 전체를 덮는 펄스 오버레이 오브젝트. Animator에 EffectsPulse.anim이 설정돼 있어야 한다.")]
    [SerializeField] private GameObject pulseOverlay;

    public SurpriseEventId EventId => SurpriseEventId.EVT_ATK_02_PulseInterference;

    public void EnterEvent(SurpriseEventContext context) { }

    /// <summary>
    /// 공격 턴 시작 시 펄스 오버레이 활성화. 대상 플레이어 화면에만 적용.
    /// </summary>
    public void BeginEventPhase(SurpriseEventContext context)
    {
        if (!ShouldApplyToThisScreen(context)) return;
        if (pulseOverlay != null)
            pulseOverlay.SetActive(true);
    }

    /// <summary>
    /// 페이즈 종료 시 오버레이 비활성화.
    /// </summary>
    public void EndEvent(SurpriseEventContext context)
    {
        if (pulseOverlay != null)
            pulseOverlay.SetActive(false);
    }

    private bool ShouldApplyToThisScreen(SurpriseEventContext context)
    {
        NetworkManager nm = NetworkManager.Instance;
        if (nm == null) return true;
        return nm.LocalPlayerId == context.targetPlayerId;
    }
}
