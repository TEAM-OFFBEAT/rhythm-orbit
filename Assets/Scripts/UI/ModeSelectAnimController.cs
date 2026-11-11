using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Spine.Unity;

/// <summary>모드 선택 버튼 호버/클릭 애니메이션 제어기.</summary>
public class ModeSelectAnimController : MonoBehaviour
{
    [SerializeField] private SkeletonAnimation skeleton44;
    [SerializeField] private SkeletonAnimation skeleton34;

    private void Start()
    {
        SetAnim(skeleton44, "default", true);
        SetAnim(skeleton34, "default", true);

        RegisterPointerEvents(skeleton44.gameObject,
            _ => { SetAnim(skeleton44, "focus", false);          SetAnim(skeleton34, "slide_R", false); },
            _ => { SetAnim(skeleton44, "focus_rollback", false); SetAnim(skeleton34, "slide_R_rollback", false); });

        RegisterPointerEvents(skeleton34.gameObject,
            _ => { SetAnim(skeleton34, "focus", false);          SetAnim(skeleton44, "slide", false); },
            _ => { SetAnim(skeleton34, "focus_rollback", false); SetAnim(skeleton44, "slide_rollback", false); });

        skeleton44.GetComponent<Button>().onClick.AddListener(() => SetAnim(skeleton44, "select", false));
        skeleton34.GetComponent<Button>().onClick.AddListener(() => SetAnim(skeleton34, "select", false));
    }

    private static void SetAnim(SkeletonAnimation skeleton, string animName, bool loop) =>
        skeleton.AnimationState.SetAnimation(0, animName, loop);

    private static void RegisterPointerEvents(
        GameObject target,
        UnityEngine.Events.UnityAction<BaseEventData> onEnter,
        UnityEngine.Events.UnityAction<BaseEventData> onExit)
    {
        var trigger = target.AddComponent<EventTrigger>();

        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(onEnter);
        trigger.triggers.Add(enter);

        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(onExit);
        trigger.triggers.Add(exit);
    }
}
