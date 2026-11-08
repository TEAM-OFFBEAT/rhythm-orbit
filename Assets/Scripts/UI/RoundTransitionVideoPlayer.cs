using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// 라운드 전환 시 MP4를 재생한다.
/// CanvasGroup.alpha로 투명도를 제어하므로 셰이더 의존이 없다.
/// 영상 알파 0→1 크로스페이드 → 알파 1 도달 시 onComplete(배경 교체) → 전체 재생 → 알파 1→0 크로스페이드 순으로 진행한다.
/// </summary>
public class RoundTransitionVideoPlayer : MonoBehaviour
{
    [Header("Clips")]
    [SerializeField] private VideoClip[] transitionClips; // [0]=R1→R2, [1]=R2→R3

    [Header("Video Sorting")]
    [SerializeField] private string videoSortingLayer = "Default";
    [SerializeField] private int videoSortingOrder = 2;

    [Header("Settings")]
    [SerializeField] private float fadeDuration = 0.5f;
    [SerializeField] private float cameraCenterThreshold = 0.1f;

    private VideoPlayer videoPlayer;
    private GameObject videoCanvas;
    private CanvasGroup videoCanvasGroup;
    private bool isTransitioning;

    private void Awake()
    {
        videoPlayer   = GetComponent<VideoPlayer>();
        videoCanvas   = transform.Find("_VideoCanvas")?.gameObject;
        if (videoCanvas != null) videoCanvasGroup = videoCanvas.GetComponent<CanvasGroup>();

        if (videoPlayer == null || videoCanvas == null)
            SetupVideoCanvas();
    }

    private void SetupVideoCanvas()
    {
        var cam = Camera.main;
        float height = cam != null ? cam.orthographicSize * 2f : 10.8f;
        float width  = cam != null ? height * cam.aspect : height * (16f / 9f);

        videoCanvas = new GameObject("_VideoCanvas");
        videoCanvas.transform.SetParent(transform, false);

        var canvas = videoCanvas.AddComponent<Canvas>();
        canvas.renderMode       = RenderMode.WorldSpace;
        canvas.worldCamera      = cam;
        canvas.sortingLayerName = videoSortingLayer;
        canvas.sortingOrder     = videoSortingOrder;

        var canvasRect = videoCanvas.GetComponent<RectTransform>();
        canvasRect.localScale = Vector3.one;
        canvasRect.sizeDelta  = new Vector2(width, height);

        videoCanvasGroup = videoCanvas.AddComponent<CanvasGroup>();
        videoCanvasGroup.blocksRaycasts = false;
        videoCanvasGroup.interactable   = false;

        var imageGo = new GameObject("_VideoImage");
        imageGo.transform.SetParent(videoCanvas.transform, false);
        var rect = imageGo.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var rt = new RenderTexture(1920, 1080, 0);
        imageGo.AddComponent<RawImage>().texture = rt;

        videoPlayer = gameObject.AddComponent<VideoPlayer>();
        videoPlayer.renderMode      = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture   = rt;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        videoPlayer.playOnAwake     = false;
        videoPlayer.isLooping       = false;
        videoPlayer.skipOnDrop      = true;

        videoCanvas.SetActive(false);
    }

    /// <summary>
    /// 지정 인덱스의 클립을 미리 디코딩한다. 게임 시작 시 또는 직전 전환 완료 직후 호출한다.
    /// </summary>
    public void PrepareClip(int transitionIndex)
    {
        var clip = GetClipAt(transitionIndex);
        if (clip == null) return;
        videoPlayer.clip = clip;
        videoPlayer.Prepare();
    }

    /// <summary>
    /// 전환 영상을 재생한다. 카메라가 중앙에 도달하면 크로스페이드 인 → onComplete(배경 교체) → 전체 재생 → 크로스페이드 아웃 순으로 진행한다.
    /// </summary>
    public void TriggerTransition(int fromRoundIndex, Action onComplete)
    {
        if (isTransitioning || GetClipAt(fromRoundIndex) == null)
        {
            onComplete?.Invoke();
            return;
        }
        StartCoroutine(PlaySequence(fromRoundIndex, onComplete));
    }

    private IEnumerator PlaySequence(int clipIndex, Action onComplete)
    {
        isTransitioning = true;
        var cam = Camera.main;

        while (cam != null && Mathf.Abs(cam.transform.position.x) > cameraCenterThreshold)
            yield return null;

        if (!videoPlayer.isPrepared || videoPlayer.clip != GetClipAt(clipIndex))
        {
            videoPlayer.clip = GetClipAt(clipIndex);
            videoPlayer.Prepare();
            while (!videoPlayer.isPrepared) yield return null;
        }

        // 페이드 인: 영상 0→1 (배경은 불투명 그대로, over-compositing이 크로스페이드 효과)
        SetVideoAlpha(0f);
        videoCanvas.SetActive(true);
        videoPlayer.Play();
        yield return FadeVideo(0f, 1f);

        // 영상이 배경을 완전히 가린 상태에서 배경 스프라이트 교체
        onComplete?.Invoke();

        while (videoPlayer.isPlaying)
            yield return null;

        // 페이드 아웃: 영상 1→0, 새 배경 드러남
        yield return FadeVideo(1f, 0f);

        videoCanvas.SetActive(false);

        isTransitioning = false;
        PrepareClip(clipIndex + 1);
    }

    private IEnumerator FadeVideo(float from, float to)
    {
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            SetVideoAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / fadeDuration)));
            yield return null;
        }
        SetVideoAlpha(to);
    }

    private void SetVideoAlpha(float alpha)
    {
        if (videoCanvasGroup != null) videoCanvasGroup.alpha = alpha;
    }

    private VideoClip GetClipAt(int index)
    {
        if (transitionClips == null || index < 0 || index >= transitionClips.Length) return null;
        return transitionClips[index];
    }

    /// <summary>
    /// 게임 종료 시 라운드 전환 영상과 후속 콜백을 중단한다.
    /// </summary>
    public void CancelTransition()
    {
        StopAllCoroutines();

        if (videoPlayer != null)
        {
            videoPlayer.Stop();
        }

        if (videoCanvas != null)
        {
            videoCanvas.SetActive(false);
        }

        SetVideoAlpha(0f);
        isTransitioning = false;
    }
}
