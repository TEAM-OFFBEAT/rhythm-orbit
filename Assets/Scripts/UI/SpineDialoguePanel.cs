using Spine.Unity;
using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// GameNoticePanel 루트에 붙는 Spine 기반 대화 패널.
/// Open()은 animation_on을 재생하고, Close()는 animation_off 재생 후 비활성화한다.
/// 이미 같은 상태이면 no-op.
/// </summary>
public class SpineDialoguePanel : MonoBehaviour
{
    [SerializeField] private SkeletonAnimation skeletonAnimation;
    [SerializeField] private string onAnimation = "animation_on";
    [SerializeField] private string offAnimation = "animation_off";

    [SerializeField, Min(0f)] private float textRevealDelay = 0.1f;
    [SerializeField, Min(0f)] private float closeAnimationOffset = 0f;

    public bool IsOpen { get; private set; }

    private Coroutine closeCoroutine;

    private void Awake()
    {
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 패널을 연다. 이미 열려 있으면 no-op.
    /// 닫히는 도중이면 닫기를 취소하고 animation_on을 재생한다.
    /// onOpened가 있으면 animation_on 재생이 끝난 뒤 호출한다.
    /// </summary>
    public void Open(Action onOpened = null)
    {
        if (IsOpen) return;

        if (closeCoroutine != null)
        {
            StopCoroutine(closeCoroutine);
            closeCoroutine = null;
        }

        IsOpen = true;
        gameObject.SetActive(true);
        var entry = skeletonAnimation.AnimationState.SetAnimation(0, onAnimation, false);

        if (onOpened != null)
            StartCoroutine(InvokeAfter(textRevealDelay, onOpened));
    }

    private IEnumerator InvokeAfter(float seconds, Action callback)
    {
        yield return new WaitForSecondsRealtime(seconds);
        callback();
    }

    /// <summary>
    /// 패널을 닫는다. animation_off를 재생한 뒤 애니메이션 길이만큼 대기 후 비활성화한다.
    /// 이미 닫혀 있으면 no-op.
    /// </summary>
    public void Close()
    {
        if (!IsOpen) return;

        IsOpen = false;
        var entry = skeletonAnimation.AnimationState.SetAnimation(0, offAnimation, false);
        entry.TrackTime = closeAnimationOffset;
        float remaining = Mathf.Max(0f, entry.Animation.Duration - closeAnimationOffset);
        closeCoroutine = StartCoroutine(DeactivateAfter(remaining));
    }

    private IEnumerator DeactivateAfter(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        gameObject.SetActive(false);
        closeCoroutine = null;
    }
}
