using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>Plays the lobby synopsis over a full-screen, letterboxed panel.</summary>
public sealed class SynopsisVideoPlayer : MonoBehaviour
{
    private const float PrepareTimeoutSeconds = 15f;
    private static SynopsisVideoPlayer activePlayer;

    [SerializeField] private RawImage videoImage;
    [SerializeField] private AspectRatioFitter aspectFitter;
    [SerializeField] private Button skipButton;

    private VideoPlayer player;
    private Action<bool> onFinished;
    private bool finished;

    public static bool IsPlaying => activePlayer != null;

    /// <summary>지정된 프리팹을 인스턴스화해 시놉시스를 재생한다.</summary>
    public static void Play(SynopsisVideoPlayer prefab, VideoClip clip, Action<bool> onFinished)
    {
        if (IsPlaying) return;

        activePlayer = Instantiate(prefab);
        var canvas = activePlayer.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue - 1;
        }
        activePlayer.onFinished = onFinished;
        EventSystem.current?.SetSelectedGameObject(null);
        activePlayer.StartCoroutine(activePlayer.PlayClip(clip));
    }

    private void Awake()
    {
        skipButton?.onClick.AddListener(() => Finish(true));
    }

    private IEnumerator PlayClip(VideoClip clip)
    {
        if (aspectFitter != null)
            aspectFitter.aspectRatio = clip.height > 0 ? (float)clip.width / clip.height : 16f / 9f;

        if (videoImage != null)
            videoImage.enabled = false;

        player = gameObject.AddComponent<VideoPlayer>();
        player.playOnAwake = false;
        player.source = VideoSource.VideoClip;
        player.clip = clip;
        player.renderMode = VideoRenderMode.APIOnly;
        player.audioOutputMode = VideoAudioOutputMode.None;
        player.loopPointReached += HandleEnd;
        player.errorReceived += HandleError;
        player.Prepare();

        float deadline = Time.realtimeSinceStartup + PrepareTimeoutSeconds;
        while (!finished && !player.isPrepared && Time.realtimeSinceStartup < deadline)
            yield return null;

        if (finished) yield break;
        if (!player.isPrepared)
        {
            Debug.LogWarning("Synopsis video did not prepare in time. Opening the tutorial.");
            Finish(false);
            yield break;
        }

        player.Play();
        SoundManager.Instance?.PlayBgm(BgmId.Synopsis);
        while (!finished)
        {
            if (videoImage != null && !videoImage.enabled && player.texture != null)
            {
                videoImage.texture = player.texture;
                videoImage.enabled = true;
            }
            yield return null;
        }
    }

    private void HandleEnd(VideoPlayer source) => Finish(true);

    private void HandleError(VideoPlayer source, string message)
    {
        Debug.LogWarning($"Synopsis video failed: {message}");
        Finish(false);
    }

    private void Finish(bool completed)
    {
        if (finished) return;
        finished = true;
        player.Stop();
        onFinished?.Invoke(completed);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (player != null)
        {
            player.loopPointReached -= HandleEnd;
            player.errorReceived -= HandleError;
        }
        if (activePlayer == this)
            activePlayer = null;
    }
}
