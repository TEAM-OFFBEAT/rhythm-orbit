using Spine;
using Spine.Unity;
using UnityEngine;

/// <summary>
/// SkeletonGraphic(Canvas UI) 또는 SkeletonAnimation 양쪽을 지원하는 애니메이션 플레이어.
/// SkeletonGraphic 물리 흔들림이 캔버스/카메라 이동에 반응하지 않도록 고정.
/// </summary>
public class SpineUIPlayer : MonoBehaviour
{
    [SerializeField] private string idleAnimationName = "default_2";

    SkeletonGraphic sg;
    SkeletonAnimation sa;
    bool resetPhysics;

    Spine.AnimationState AnimState => sa?.AnimationState;

    void Awake()
    {
        sg = GetComponent<SkeletonGraphic>();
        sa = GetComponent<SkeletonAnimation>();

        if (sg != null)
        {
            sg.PhysicsPositionInheritanceFactor = Vector2.zero;
            sg.PhysicsRotationInheritanceFactor = 0f;
            sg.UpdateComplete += OnUpdateComplete;
        }

        if (sa != null)
            sa.AnimationState.Start += _ => resetPhysics = true;
    }

    void OnEnable() => PlayIdle();

    /// <summary>idle 무한 루프 재생. 이미 재생 중이면 중단 없이 유지.</summary>
    public void PlayIdle() => Play(idleAnimationName, true);

    /// <summary>지정 애니메이션을 한 번 재생 후 idle로 복귀.</summary>
    public void PlayOnce(string anim)
    {
        var state = AnimState;
        if (state == null) return;
        Play(anim, false);
        state.AddAnimation(0, idleAnimationName, true, 0f).MixDuration = 0f;
    }

    /// <summary>지정 애니메이션을 count번 연속 재생 후 idle로 복귀.</summary>
    public void PlayRepeat(string anim, int count)
    {
        var state = AnimState;
        if (state == null || count <= 0) return;
        Play(anim, false);
        for (int i = 1; i < count; i++)
            state.AddAnimation(0, anim, false, 0f).MixDuration = 0f;
        state.AddAnimation(0, idleAnimationName, true, 0f).MixDuration = 0f;
    }

    /// <summary>지정 애니메이션을 크로스페이드 없이 즉시 재생.</summary>
    public void Play(string anim, bool loop)
    {
        var state = AnimState;
        if (state == null) return;
        if (state.Data.SkeletonData.FindAnimation(anim) == null) return;
        var current = state.GetTrack(0);
        if (loop && current != null && current.Loop && current.Animation.Name == anim)
            return;
        var entry = state.SetAnimation(0, anim, loop);
        entry.MixDuration = 0f;
    }

    void OnUpdateComplete(ISkeletonRenderer _)
    {
        if (!resetPhysics) return;
        sg.Skeleton.UpdateWorldTransform(Spine.Physics.Reset);
        resetPhysics = false;
    }
}
