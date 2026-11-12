using System.Collections;
using UnityEngine;

/// <summary>
/// 로컬 플레이어의 정신력이 임계값 이하로 떨어지면 VignetteRenderFeature의 Opacity를 설정해
/// 비네트 오버레이를 화면에 합성한다.
/// </summary>
public class SanityVignetteOverlay : MonoBehaviour
{
    [SerializeField] private SanitySystem sanitySystem;

    [Header("Trigger")]
    [SerializeField] private int triggerThreshold = 30;

    [Header("Pulse")]
    [SerializeField, Range(0f, 1f)] private float minOpacity = 0.3f;
    [SerializeField, Range(0f, 1f)] private float maxOpacity = 0.7f;
    [SerializeField] private float pulseSpeed = 0.5f;

    [Header("Game Over Fade")]
    [SerializeField] private float gameOverFadeDuration = 1.5f;

    [Header("Debug")]
    [SerializeField] private bool debugForceActive = false;

    private int _currentSanity;
    private bool _active;

    private void OnEnable()
    {
        if (sanitySystem != null)
        {
            sanitySystem.OnSanityChanged += OnSanityChanged;
            sanitySystem.OnPlayerDefeated += OnPlayerDefeated;
        }
    }

    private void OnDisable()
    {
        if (sanitySystem != null)
        {
            sanitySystem.OnSanityChanged -= OnSanityChanged;
            sanitySystem.OnPlayerDefeated -= OnPlayerDefeated;
        }
        VignetteRenderFeature.Opacity = 0f;
    }

    private void Update()
    {
        if (!_active && !debugForceActive) return;

        float t = (Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
        VignetteRenderFeature.Opacity = Mathf.Lerp(minOpacity, maxOpacity, t);
    }

    private void OnSanityChanged(int p1Sanity, int p2Sanity, int maxSanity)
    {
        int localId = NetworkManager.Instance?.LocalPlayerId ?? 1;
        _currentSanity = localId == 1 ? p1Sanity : p2Sanity;

        bool shouldBeActive = _currentSanity <= triggerThreshold && _currentSanity > 0;
        if (shouldBeActive == _active) return;

        _active = shouldBeActive;
        if (!_active)
            VignetteRenderFeature.Opacity = 0f;
    }

    private void OnPlayerDefeated(int playerId)
    {
        if (playerId != (NetworkManager.Instance?.LocalPlayerId ?? 1)) return;

        _active = false;
        StartCoroutine(FadeOut());
    }

    private IEnumerator FadeOut()
    {
        float startOpacity = VignetteRenderFeature.Opacity;
        float elapsed = 0f;

        while (elapsed < gameOverFadeDuration)
        {
            elapsed += Time.deltaTime;
            VignetteRenderFeature.Opacity = Mathf.Lerp(startOpacity, 0f, elapsed / gameOverFadeDuration);
            yield return null;
        }

        VignetteRenderFeature.Opacity = 0f;
    }
}
