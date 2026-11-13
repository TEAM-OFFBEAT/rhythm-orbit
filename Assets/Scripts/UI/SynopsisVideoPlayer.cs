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

    private VideoPlayer player;
    private RawImage videoImage;
    private Action<bool> onFinished;
    private bool finished;

    public static bool IsPlaying => activePlayer != null;

    public static void Play(VideoClip clip, Action<bool> onFinished)
    {
        if (IsPlaying) return;

        var root = new GameObject("Synopsis Video", typeof(RectTransform), typeof(Canvas),
            typeof(GraphicRaycaster));
        activePlayer = root.AddComponent<SynopsisVideoPlayer>();
        activePlayer.onFinished = onFinished;
        EventSystem.current?.SetSelectedGameObject(null);
        activePlayer.StartCoroutine(activePlayer.PlayClip(clip));
    }

    private IEnumerator PlayClip(VideoClip clip)
    {
        Canvas canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue - 1;

        var background = new GameObject("Background", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        Stretch(background.rectTransform);
        background.color = Color.black;

        videoImage = new GameObject("Video", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        Stretch(videoImage.rectTransform);
        videoImage.raycastTarget = false;
        videoImage.enabled = false;
        var aspect = videoImage.gameObject.AddComponent<AspectRatioFitter>();
        aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        aspect.aspectRatio = clip.height > 0 ? (float)clip.width / clip.height : 16f / 9f;

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
            if (!videoImage.enabled && player.texture != null)
            {
                videoImage.texture = player.texture;
                videoImage.enabled = true;
            }
            yield return null;
        }
    }

    private void Stretch(RectTransform rect)
    {
        rect.SetParent(transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
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
