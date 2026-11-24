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

    [Header("Conversion")]
    [SerializeField, Min(0.01f)] private float conversionSeconds = 0.25f;

    private SpriteRenderer normalOverlay;
    private Coroutine conversionCoroutine;
    private float convertedAlpha;
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

    public void BeginConversion(float alpha, double judgeTime)
    {
        Sprite original = spriteRenderer.sprite;
        Color originalColor = spriteRenderer.color;
        BeginMoving();

        if (normalOverlay == null)
        {
            var overlay = new GameObject("NormalNoteFade", typeof(SpriteRenderer));
            overlay.transform.SetParent(transform, false);
            normalOverlay = overlay.GetComponent<SpriteRenderer>();
        }

        normalOverlay.sprite = original;
        normalOverlay.sharedMaterial = spriteRenderer.sharedMaterial;
        normalOverlay.sortingLayerID = spriteRenderer.sortingLayerID;
        normalOverlay.sortingOrder = spriteRenderer.sortingOrder + 1;
        normalOverlay.flipX = spriteRenderer.flipX;
        normalOverlay.flipY = spriteRenderer.flipY;
        normalOverlay.maskInteraction = spriteRenderer.maskInteraction;
        normalOverlay.color = originalColor;
        normalOverlay.enabled = true;
        convertedAlpha = Mathf.Clamp01(alpha);
        Color ghostColor = originalColor;
        ghostColor.a = 0f;
        spriteRenderer.color = ghostColor;

        // 첫 노트도 판정선에 도착하기 전에 유령 모습을 식별할 시간을 남긴다.
        double remaining = judgeTime - AudioSettings.dspTime;
        float duration = remaining > 0.0
            ? Mathf.Min(conversionSeconds, (float)remaining * 0.6f)
            : conversionSeconds;
        conversionCoroutine = StartCoroutine(CrossFade(duration, originalColor.a));
    }

    private IEnumerator CrossFade(float duration, float originalAlpha)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float progress = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            Color ghostColor = spriteRenderer.color;
            ghostColor.a = convertedAlpha * progress;
            spriteRenderer.color = ghostColor;
            Color normalColor = normalOverlay.color;
            normalColor.a = originalAlpha * (1f - progress);
            normalOverlay.color = normalColor;
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        conversionCoroutine = null;
        FinishConversion();
    }

    private void FinishConversion()
    {
        if (normalOverlay != null) normalOverlay.enabled = false;
        if (spriteRenderer == null) return;
        Color color = spriteRenderer.color;
        color.a = convertedAlpha;
        spriteRenderer.color = color;
    }

    private void StopConversion()
    {
        if (conversionCoroutine == null) return;
        StopCoroutine(conversionCoroutine);
        conversionCoroutine = null;
        FinishConversion();
    }
    public void PlayDisappear(Action onFinished)
    {
        if (isDisappearing) return;

        StopConversion();
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
        StopConversion();
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