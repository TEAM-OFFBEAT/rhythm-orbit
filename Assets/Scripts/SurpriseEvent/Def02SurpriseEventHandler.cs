using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// EVT_DEF_02 음향 수신 이벤트 핸들러.
/// 공격턴 동안 비네트 선행 암전이 진행되고,
/// 방어턴에는 일반 노트를 숨긴 채 전체 암전을 유지한다.
/// </summary>
public class Def02SurpriseEventHandler : MonoBehaviour, ISurpriseEventHandler, ISurpriseEventPreludeHandler
{
    [Header("References")]
    [SerializeField] private DefenseTurn defenseTurn;
    [SerializeField] private AttackTurnRenderer attackTurnRenderer;
    [SerializeField] private GameObject blackoutRoot;
    [SerializeField] private Image vignetteImage;
    [SerializeField] private Image solidBlackImage;

    [Header("Prelude Vignette")]
    [Tooltip("공격턴 선행 연출 동안 비네트 이미지가 도달할 최대 알파.")]
    [SerializeField, Range(0f, 1f)] private float vignetteMaxAlpha = 1f;

    [Tooltip("공격턴 진행률 몇 %부터 전체 검은 화면을 서서히 올릴지 정한다.")]
    [SerializeField, Range(0f, 1f)] private float solidFadeStartRatio = 0.75f;

    [Header("Defense Blackout")]
    [Tooltip("방어턴 중 전체 검은 화면 알파. 1이면 완전 암전.")]
    [SerializeField, Range(0f, 1f)] private float defenseBlackAlpha = 1f;

    [Tooltip("이벤트 종료 후 암전이 풀리는 시간.")]
    [SerializeField, Min(0.01f)] private float fadeOutSeconds = 0.25f;

    [Header("Notes")]
    [SerializeField] private bool hideNormalNotesDuringDefense = true;

    [Header("Network")]
    [Tooltip("네트워크 모드에서는 targetPlayerId와 내 localPlayerId가 같을 때만 암전을 적용한다.")]
    [SerializeField] private bool applyOnlyToTargetPlayerInNetwork = true;

    private SurpriseEventContext activeContext;
    private Coroutine preludeCoroutine;
    private Coroutine fadeOutCoroutine;
    private bool isSubscribed;
    private bool noteHidden;
    private bool preludeRunning;

    public SurpriseEventId EventId => SurpriseEventId.EVT_DEF_02_AudioReception;

    private void Awake()
    {
        if (defenseTurn == null)
        {
            defenseTurn = FindAnyObjectByType<DefenseTurn>();
        }

        if (attackTurnRenderer == null)
        {
            attackTurnRenderer = FindAnyObjectByType<AttackTurnRenderer>();
        }

        ResetVisualsInstant();
    }

    private void OnDisable()
    {
        StopPreludeCoroutine();
        StopFadeOutCoroutine();
        Unsubscribe();

        RestoreNotes();
        ResetVisualsInstant();

        activeContext = null;
        preludeRunning = false;
    }

    /// <summary>
    /// 진입 단계 이전 선행 연출.
    /// 현재 공격턴 동안 바깥쪽부터 화면이 어두워지는 비네트 연출을 시작한다.
    /// </summary>
    public void BeginPrelude(SurpriseEventContext context)
    {
        activeContext = context;

        if (!ShouldApplyToThisScreen(context))
        {
            return;
        }

        StopFadeOutCoroutine();
        StopPreludeCoroutine();

        PrepareOverlayImages();

        preludeCoroutine = StartCoroutine(PreludeBlackoutRoutine(context));
        preludeRunning = true;

        Debug.Log("DEF_02 AudioReception 선행 연출 시작");
    }

    /// <summary>
    /// 선행 연출 강제 종료용.
    /// 새 게임 시작/비활성화 같은 예외 상황에서 사용한다.
    /// </summary>
    public void EndPrelude(SurpriseEventContext context)
    {
        StopPreludeCoroutine();
        preludeRunning = false;
    }

    /// <summary>
    /// 이벤트 진입 단계.
    /// 토스트는 SurpriseEventManager가 처리하므로 여기서는 선행 암전을 유지한다.
    /// </summary>
    public void EnterEvent(SurpriseEventContext context)
    {
        activeContext = context;

        if (!ShouldApplyToThisScreen(context))
        {
            return;
        }

        PrepareOverlayImages();

        if (!preludeRunning)
        {
            StartEmergencyFadeToBlack(context);
        }

        Debug.Log("DEF_02 AudioReception 진입");
    }

    /// <summary>
    /// 방어 페이즈가 실제로 시작되면 완전 암전으로 고정하고 일반 노트를 숨긴다.
    /// </summary>
    public void BeginEventPhase(SurpriseEventContext context)
    {
        activeContext = context;

        if (!ShouldApplyToThisScreen(context))
        {
            return;
        }

        Subscribe();
        TryApplyDefensePhaseEffect();

        Debug.Log("DEF_02 AudioReception 적용 준비");
    }

    /// <summary>
    /// 방어 페이즈 종료 시 노트를 복구하고 암전을 해제한다.
    /// </summary>
    public void EndEvent(SurpriseEventContext context)
    {
        if (ShouldApplyToThisScreen(context))
        {
            StopPreludeCoroutine();
            RestoreNotes();
            StartFadeOut();
        }

        Unsubscribe();

        activeContext = null;
        preludeRunning = false;

        Debug.Log("DEF_02 AudioReception 종료");
    }

    private IEnumerator PreludeBlackoutRoutine(SurpriseEventContext context)
    {
        double startDspTime = AudioSettings.dspTime;
        double endDspTime = context.phaseStartDspTime;
        double duration = System.Math.Max(0.01, endDspTime - startDspTime);

        while (AudioSettings.dspTime < endDspTime)
        {
            double now = AudioSettings.dspTime;
            float progress = Mathf.Clamp01((float)((now - startDspTime) / duration));

            // 1단계: 비네트가 천천히 진해짐.
            // 네가 올린 이미지가 바깥이 어둡고 중앙이 밝으니까,
            // 이 알파가 올라갈수록 바깥부터 먹히는 느낌이 난다.
            float vignetteAlpha = Mathf.Lerp(0f, vignetteMaxAlpha, Smooth01(progress));
            SetVignetteAlpha(vignetteAlpha);

            // 2단계: 후반부부터 전체 검은 이미지가 올라와서 중앙까지 닫힘.
            float solidProgress = Mathf.InverseLerp(solidFadeStartRatio, 1f, progress);
            float solidAlpha = Mathf.Lerp(0f, defenseBlackAlpha, Smooth01(solidProgress));
            SetSolidBlackAlpha(solidAlpha);

            yield return null;
        }

        SetVignetteAlpha(vignetteMaxAlpha);
        SetSolidBlackAlpha(defenseBlackAlpha);

        preludeCoroutine = null;
        preludeRunning = false;
    }

    private void StartEmergencyFadeToBlack(SurpriseEventContext context)
    {
        StopPreludeCoroutine();

        preludeCoroutine = StartCoroutine(EmergencyFadeToBlackRoutine(context));
        preludeRunning = true;
    }

    private IEnumerator EmergencyFadeToBlackRoutine(SurpriseEventContext context)
    {
        float fromVignette = GetVignetteAlpha();
        float fromSolid = GetSolidBlackAlpha();

        float duration = Mathf.Max(
            0.01f,
            (float)(context.phaseStartDspTime - AudioSettings.dspTime)
        );

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Smooth01(Mathf.Clamp01(elapsed / duration));

            SetVignetteAlpha(Mathf.Lerp(fromVignette, vignetteMaxAlpha, t));
            SetSolidBlackAlpha(Mathf.Lerp(fromSolid, defenseBlackAlpha, t));

            yield return null;
        }

        SetVignetteAlpha(vignetteMaxAlpha);
        SetSolidBlackAlpha(defenseBlackAlpha);

        preludeCoroutine = null;
        preludeRunning = false;
    }

    private void Subscribe()
    {
        if (isSubscribed || defenseTurn == null)
        {
            return;
        }

        defenseTurn.OnDefenseBegan += HandleDefenseBegan;
        isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (!isSubscribed || defenseTurn == null)
        {
            return;
        }

        defenseTurn.OnDefenseBegan -= HandleDefenseBegan;
        isSubscribed = false;
    }

    private void HandleDefenseBegan()
    {
        TryApplyDefensePhaseEffect();
    }

    private void TryApplyDefensePhaseEffect()
    {
        if (activeContext == null)
        {
            return;
        }

        if (defenseTurn == null || !defenseTurn.IsRunning)
        {
            Debug.Log("DEF_02: DefenseTurn이 아직 시작되지 않아 OnDefenseBegan을 기다림.");
            return;
        }

        StopPreludeCoroutine();

        SetVignetteAlpha(vignetteMaxAlpha);
        SetSolidBlackAlpha(defenseBlackAlpha);

        HideNotes();

        Debug.Log("DEF_02 AudioReception 적용 시작 / 방어 노트 숨김");
    }

    private void HideNotes()
    {
        if (noteHidden)
        {
            return;
        }

        if (!hideNormalNotesDuringDefense)
        {
            return;
        }

        attackTurnRenderer?.SetNormalNotesAlpha(0f);
        noteHidden = true;
    }

    private void RestoreNotes()
    {
        if (!noteHidden)
        {
            return;
        }

        attackTurnRenderer?.SetNormalNotesAlpha(1f);
        noteHidden = false;
    }

    private void StartFadeOut()
    {
        StopFadeOutCoroutine();
        fadeOutCoroutine = StartCoroutine(FadeOutRoutine());
    }

    private IEnumerator FadeOutRoutine()
    {
        float fromVignette = GetImageAlpha(vignetteImage);
        float fromSolid = GetImageAlpha(solidBlackImage);

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, fadeOutSeconds);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float t = Smooth01(Mathf.Clamp01(elapsed / duration));

            SetVignetteAlpha(Mathf.Lerp(fromVignette, 0f, t));
            SetSolidBlackAlpha(Mathf.Lerp(fromSolid, 0f, t));

            yield return null;
        }

        SetVignetteAlpha(0f);
        SetSolidBlackAlpha(0f);

        fadeOutCoroutine = null;
    }

    private void PrepareOverlayImages()
    {
        if (blackoutRoot != null)
        {
            blackoutRoot.SetActive(true);
        }

        if (vignetteImage != null)
        {
            vignetteImage.gameObject.SetActive(true);
            vignetteImage.raycastTarget = false;
        }

        if (solidBlackImage != null)
        {
            solidBlackImage.gameObject.SetActive(true);
            solidBlackImage.raycastTarget = false;
        }
    }

    private void ResetVisualsInstant()
    {
        if (blackoutRoot != null)
        {
            blackoutRoot.SetActive(true);
        }

        SetVignetteAlpha(0f);
        SetSolidBlackAlpha(0f);

        if (vignetteImage != null)
        {
            vignetteImage.raycastTarget = false;
        }

        if (solidBlackImage != null)
        {
            solidBlackImage.raycastTarget = false;
        }
    }

    private void StopPreludeCoroutine()
    {
        if (preludeCoroutine == null)
        {
            return;
        }

        StopCoroutine(preludeCoroutine);
        preludeCoroutine = null;
    }

    private void StopFadeOutCoroutine()
    {
        if (fadeOutCoroutine == null)
        {
            return;
        }

        StopCoroutine(fadeOutCoroutine);
        fadeOutCoroutine = null;
    }

    private bool ShouldApplyToThisScreen(SurpriseEventContext context)
    {
        if (!applyOnlyToTargetPlayerInNetwork)
        {
            return true;
        }

        NetworkManager networkManager = NetworkManager.Instance;

        if (networkManager == null)
        {
            // 로컬 테스트에서는 한 화면이므로 일단 적용한다.
            return true;
        }

        return networkManager.LocalPlayerId == context.targetPlayerId;
    }

    private float Smooth01(float value)
    {
        float t = Mathf.Clamp01(value);
        return t * t * (3f - 2f * t);
    }

    private void SetVignetteAlpha(float alpha)
    {
        if (vignetteImage == null)
        {
            return;
        }

        Color color = Color.white;
        color.a = Mathf.Clamp01(alpha);
        vignetteImage.color = color;
    }

    private void SetSolidBlackAlpha(float alpha)
    {
        if (solidBlackImage == null)
        {
            return;
        }

        Color color = Color.black;
        color.a = Mathf.Clamp01(alpha);
        solidBlackImage.color = color;
    }

    private float GetVignetteAlpha()
    {
        return vignetteImage != null ? vignetteImage.color.a : 0f;
    }

    private float GetSolidBlackAlpha()
    {
        return solidBlackImage != null ? solidBlackImage.color.a : 0f;
    }

    private float GetImageAlpha(Image image)
    {
        return image != null ? image.color.a : 0f;
    }
}