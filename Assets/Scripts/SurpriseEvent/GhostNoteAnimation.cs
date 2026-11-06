using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 유령노트의 이동 반복 애니메이션과 소멸 애니메이션을 재생한다.
/// 위치와 알파는 기존 이동·페이드인 코드에서 처리한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class GhostNoteAnimation : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private Sprite[] moveFrames;
    [SerializeField, Min(0.001f)] private float moveFrameTime = 0.05f;

    [Header("Disappear")]
    [SerializeField] private Sprite[] disappearFrames;
    [SerializeField, Min(0.001f)] private float disappearFrameTime = 0.05f;

    private SpriteRenderer spriteRenderer;
    private Sprite normalSprite;
    private bool normalRendererEnabled;

    private Coroutine animationCoroutine;
    private bool isDisappearing;
    private Action onDisappearFinished;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        normalSprite = spriteRenderer.sprite;
        normalRendererEnabled = spriteRenderer.enabled;
    }

    public void BeginMoving()
    {
        ResetVisual();

        if (moveFrames == null || moveFrames.Length == 0)
        {
            return;
        }

        animationCoroutine = StartCoroutine(PlayMoveFrames());
    }

    public void PlayDisappear(Action onFinished)
    {
        if (isDisappearing) return;

        StopAnimation();

        isDisappearing = true;
        onDisappearFinished = onFinished;

        if (disappearFrames == null || disappearFrames.Length == 0)
        {
            FinishDisappear();
            return;
        }

        animationCoroutine = StartCoroutine(PlayDisappearFrames());
    }

    private IEnumerator PlayMoveFrames()
    {
        while (true)
        {
            foreach (Sprite frame in moveFrames)
            {
                if (frame != null)
                {
                    spriteRenderer.sprite = frame;
                }

                yield return new WaitForSeconds(
                    Mathf.Max(0.001f, moveFrameTime));
            }
        }
    }

    private IEnumerator PlayDisappearFrames()
    {
        foreach (Sprite frame in disappearFrames)
        {
            if (frame != null)
            {
                spriteRenderer.sprite = frame;
            }

            yield return new WaitForSeconds(
                Mathf.Max(0.001f, disappearFrameTime));
        }

        animationCoroutine = null;
        FinishDisappear();
    }

    private void FinishDisappear()
    {
        spriteRenderer.enabled = false;

        Action callback = onDisappearFinished;
        onDisappearFinished = null;
        callback?.Invoke();
    }

    private void StopAnimation()
    {
        if (animationCoroutine == null) return;

        StopCoroutine(animationCoroutine);
        animationCoroutine = null;
    }

    private void ResetVisual()
    {
        StopAnimation();
        isDisappearing = false;
        onDisappearFinished = null;

        if (spriteRenderer == null) return;

        spriteRenderer.sprite = normalSprite;
        spriteRenderer.enabled = normalRendererEnabled;
    }

    private void OnDisable()
    {
        // 풀에서 일반 노트로 재사용될 수 있도록 복원한다.
        ResetVisual();
    }
}