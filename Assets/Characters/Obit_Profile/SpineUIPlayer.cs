using Spine;
using Spine.Unity;
using UnityEngine;

/// <summary>
/// SkeletonGraphic 물리 흔들림이 캔버스/카메라 이동에 반응하지 않도록 고정.
/// 이 버전 Spine에서 SkeletonGraphic은 AnimationState를 직접 노출하지 않으므로
/// 같은 GameObject의 SkeletonAnimation에서 가져온다.
/// </summary>
public class SpineUIPlayer : MonoBehaviour
{
    SkeletonGraphic sg;
    SkeletonAnimation sa;
    bool resetPhysics;

    public Spine.AnimationState AnimationState => sa.AnimationState;

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

    /// <summary>
    /// 지정 애니메이션을 크로스페이드 없이 즉시 재생. loop=false면 한 번 재생 후 정지.
    /// </summary>
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
