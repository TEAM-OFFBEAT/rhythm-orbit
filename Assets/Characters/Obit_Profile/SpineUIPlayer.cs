using Spine;
using Spine.Unity;
using UnityEngine;

/// <summary>
/// SkeletonGraphic 물리 흔들림이 캔버스/카메라 이동에 반응하지 않도록 고정.
/// </summary>
public class SpineUIPlayer : MonoBehaviour
{
    const string Idle = "default_2";

    SkeletonGraphic sg;
    SkeletonAnimation sa;
    bool resetPhysics;

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
    }

    /// <summary>idle 무한 루프 재생. 이미 재생 중이면 중단 없이 유지.</summary>
    public void PlayIdle() => Play(Idle, true);

    /// <summary>지정 애니메이션을 한 번 재생 후 idle로 복귀.</summary>
    public void PlayOnce(string anim)
    {
        Play(anim, false);
        sa.AnimationState.AddAnimation(0, Idle, true, 0f);
    }

    /// <summary>지정 애니메이션을 크로스페이드 없이 즉시 재생.</summary>
    public void Play(string anim, bool loop)
    {
        var current = sa.AnimationState.GetTrack(0);
        if (loop && current != null && current.Loop && current.Animation.Name == anim)
            return;
        var entry = sa.AnimationState.SetAnimation(0, anim, loop);
        entry.MixDuration = 0f;
        if (!loop) resetPhysics = true;
    }

    void OnUpdateComplete(ISkeletonRenderer _)
    {
        if (!resetPhysics) return;
        sg.Skeleton.UpdateWorldTransform(Spine.Physics.Reset);
        resetPhysics = false;
    }
}
