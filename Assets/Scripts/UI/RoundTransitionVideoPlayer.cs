using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// 라운드 전환 시 Space 레이어 위치에 MP4를 재생한다.
/// Space SpriteRenderer와 같은 정렬 순서의 World Space Quad를 사용하므로
/// Sparkle·Planet 레이어가 자연스럽게 위에 유지된다.
/// </summary>
public class RoundTransitionVideoPlayer : MonoBehaviour
{
    [Header("Clips")]
    [SerializeField] private VideoClip[] transitionClips; // [0]=R1→R2, [1]=R2→R3

    [Header("References")]
    [SerializeField] private Transform spaceLayer;
    [SerializeField] private Camera targetCamera;

    [Header("Settings")]
    [SerializeField] private float cameraCenterThreshold = 0.1f;

    private VideoPlayer videoPlayer;
    private GameObject videoQuad;
    private SpriteRenderer spaceRenderer;
    private bool isTransitioning;

    private void Awake()
    {
        spaceRenderer = spaceLayer?.GetComponent<SpriteRenderer>();
        if (targetCamera == null) targetCamera = Camera.main;
        SetupVideoQuad();
    }

    private void SetupVideoQuad()
    {
        float height = targetCamera != null ? targetCamera.orthographicSize * 2f : 10.8f;
        float width  = height * (16f / 9f);

        videoQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        videoQuad.name = "_VideoQuad";
        videoQuad.transform.SetParent(transform, false);
        // spaceLayer와 같은 위치·정렬 순서로 배치
        videoQuad.transform.localScale = new Vector3(width, height, 1f);
        Destroy(videoQuad.GetComponent<MeshCollider>());

        var rend = videoQuad.GetComponent<MeshRenderer>();
        rend.sortingLayerName = spaceRenderer?.sortingLayerName ?? "Default";
        rend.sortingOrder     = spaceRenderer?.sortingOrder     ?? 0;

        var rt  = new RenderTexture(1920, 1080, 0);
        var mat = new Material(Shader.Find("Unlit/Texture"));
        mat.mainTexture = rt;
        rend.sharedMaterial = mat;

        videoPlayer = gameObject.AddComponent<VideoPlayer>();
        videoPlayer.renderMode      = VideoRenderMode.RenderTexture;
        videoPlayer.targetTexture   = rt;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        videoPlayer.playOnAwake     = false;
        videoPlayer.isLooping       = false;
        videoPlayer.skipOnDrop      = true;

        videoQuad.SetActive(false);
    }

    /// <summary>
    /// 지정 인덱스의 클립을 미리 디코딩한다. 게임 시작 시 또는 직전 전환 완료 직후 호출한다.
    /// </summary>
    public void PrepareClip(int transitionIndex)
    {
        if (!TryGetClip(transitionIndex, out var clip)) return;
        videoPlayer.clip = clip;
        videoPlayer.Prepare();
    }

    /// <summary>
    /// 전환 영상을 예약한다. 카메라가 중앙에 도달하면 자동 재생하고 완료 시 onComplete를 호출한다.
    /// onComplete에서 parallaxBackground.ApplyRound()를 호출하면 된다.
    /// </summary>
    public void TriggerTransition(int fromRoundIndex, Action onComplete)
    {
        if (isTransitioning || !TryGetClip(fromRoundIndex, out _))
        {
            onComplete?.Invoke();
            return;
        }
        StartCoroutine(PlaySequence(fromRoundIndex, onComplete));
    }

    private IEnumerator PlaySequence(int clipIndex, Action onComplete)
    {
        isTransitioning = true;
        if (targetCamera == null) targetCamera = Camera.main;

        while (targetCamera != null && Mathf.Abs(targetCamera.transform.position.x) > cameraCenterThreshold)
            yield return null;

        if (!videoPlayer.isPrepared || videoPlayer.clip != GetClipAt(clipIndex))
        {
            videoPlayer.clip = GetClipAt(clipIndex);
            videoPlayer.Prepare();
            while (!videoPlayer.isPrepared) yield return null;
        }

        if (spaceRenderer != null) spaceRenderer.enabled = false;
        videoQuad.SetActive(true);
        videoPlayer.Play();
        yield return null; // isPlaying이 true가 될 때까지 1프레임 대기

        while (videoPlayer.isPlaying)
            yield return null;

        videoQuad.SetActive(false);
        if (spaceRenderer != null) spaceRenderer.enabled = true;

        isTransitioning = false;
        onComplete?.Invoke();
        PrepareClip(clipIndex + 1);
    }

    private bool TryGetClip(int index, out VideoClip clip)
    {
        clip = GetClipAt(index);
        return clip != null;
    }

    private VideoClip GetClipAt(int index)
    {
        if (transitionClips == null || index < 0 || index >= transitionClips.Length) return null;
        return transitionClips[index];
    }
}
