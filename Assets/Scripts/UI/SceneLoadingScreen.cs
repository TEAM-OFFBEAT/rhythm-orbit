using System.Collections;
using System.Collections.Generic;
using Spine.Unity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Shows a persistent overlay during single-scene transitions.</summary>
public sealed class SceneLoadingScreen : MonoBehaviour
{
    private const float MinimumDisplaySeconds = 1.1f;
    private const float ReadyToActivateProgress = 0.9f;
    private const float EarliestActivationSeconds = 0.45f;
    private const float MaxProgressWhileActivating = 0.85f;
    private const float FillSpeed = 2f;
    private const float FillWidth = 820f;
    private const float FadeOutSeconds = 0.22f;

    private static SceneLoadingScreen activeScreen;
    private static LoadingAssets sharedAssets;
    [SerializeField] private LoadingAssets sceneAssets;
    private readonly List<Object> runtimeAssets = new();
    private readonly List<SkeletonAnimation> loadingAnimations = new();
    private RawImage fill;

    public static bool IsLoading => activeScreen != null;

    private void Awake()
    {
        if (sceneAssets != null && sceneAssets.background != null)
            sharedAssets = sceneAssets;
    }

    public static void Load(string scene)
    {
        if (IsLoading) return;
        if (string.IsNullOrWhiteSpace(scene) || !Application.CanStreamedLevelBeLoaded(scene))
        {
            Debug.LogError($"Cannot load scene '{scene}'. Check the build scene list.");
            return;
        }

        var root = new GameObject("Scene Loading Screen", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        activeScreen = root.AddComponent<SceneLoadingScreen>();
        DontDestroyOnLoad(root);
        EventSystem.current?.SetSelectedGameObject(null);
        activeScreen.StartCoroutine(activeScreen.LoadScene(scene));
    }

    private IEnumerator LoadScene(string scene)
    {
        ThreadPriority previousPriority = Application.backgroundLoadingPriority;
        try
        {
            BuildView();
            SetProgress(0f);
            yield return null;

            // Give the overlay more frame time while Unity integrates scene assets.
            if (previousPriority > ThreadPriority.BelowNormal)
                Application.backgroundLoadingPriority = ThreadPriority.BelowNormal;

            var operation = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            if (operation == null) yield break;
            operation.allowSceneActivation = false;

            float displayElapsed = 0f;
            float displayedProgress = 0f;
            while (displayedProgress < 1f)
            {
                // Keep a long activation frame from skipping the rest of the visible animation.
                float frameTime = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
                displayElapsed += frameTime;
                if (!operation.allowSceneActivation && operation.progress >= ReadyToActivateProgress &&
                    displayElapsed >= EarliestActivationSeconds)
                {
                    operation.allowSceneActivation = true;
                }

                float availableProgress = operation.isDone ? 1f : Mathf.Min(operation.progress, MaxProgressWhileActivating);
                float pacedProgress = Mathf.Clamp01(displayElapsed / MinimumDisplaySeconds);
                float nextProgress = Mathf.MoveTowards(displayedProgress,
                    Mathf.Min(availableProgress, pacedProgress), FillSpeed * frameTime);
                if (nextProgress != displayedProgress)
                {
                    displayedProgress = nextProgress;
                    SetProgress(displayedProgress);
                }
                if (displayedProgress < 1f)
                    yield return null;
            }

            CanvasGroup panel = GetComponent<CanvasGroup>();
            foreach (SkeletonAnimation animation in loadingAnimations)
                animation.timeScale = 0f;

            float fadeElapsed = 0f;
            while (fadeElapsed < FadeOutSeconds)
            {
                fadeElapsed += Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
                float fadeProgress = Mathf.Clamp01(fadeElapsed / FadeOutSeconds);
                panel.alpha = Mathf.SmoothStep(1f, 0f, fadeProgress);
                yield return null;
            }
        }
        finally
        {
            Application.backgroundLoadingPriority = previousPriority;
            Destroy(gameObject);
        }
    }

    private void BuildView()
    {
        Canvas canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        LoadingAssets assets = GetAssets();
        RawImage background = AddImage("Background", assets.background, Vector2.zero, Vector2.zero);
        background.raycastTarget = true;
        RectTransform backgroundRect = background.rectTransform;
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        var aspect = background.gameObject.AddComponent<AspectRatioFitter>();
        aspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        aspect.aspectRatio = 1920f / 1080f;

        AddImage("Track", assets.track, new Vector2(6f, -207f), new Vector2(842f, 51f));
        fill = AddImage("Fill", assets.fill, new Vector2(6f - FillWidth * 0.5f, -207f),
            new Vector2(FillWidth, 40f));
        fill.rectTransform.pivot = new Vector2(0f, 0.5f);

        AddAnimation(assets.obit, "animation", new Vector2(0f, 40f), assets.material);
        AddAnimation(assets.text, "ani2", new Vector2(0f, -312f), assets.material);

        AddLetterboxBars();
    }

    private void AddLetterboxBars()
    {
        float scale = ((float)Screen.width / Screen.height) / (16f / 9f);
        if (Mathf.Approximately(scale, 1f)) return;

        if (scale < 1f)
        {
            float bar = (1f - scale) / 2f;
            AddBar(new Vector2(0f, 1f - bar), Vector2.one);
            AddBar(Vector2.zero, new Vector2(1f, bar));
        }
        else
        {
            float bar = (1f - 1f / scale) / 2f;
            AddBar(Vector2.zero, new Vector2(bar, 1f));
            AddBar(new Vector2(1f - bar, 0f), Vector2.one);
        }
    }

    private void AddBar(Vector2 anchorMin, Vector2 anchorMax)
    {
        var bar = new GameObject("LetterboxBar", typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(transform, false);
        var rect = bar.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var img = bar.GetComponent<Image>();
        img.color = Color.black;
        img.raycastTarget = false;
    }

    private RawImage AddImage(string name, Texture2D texture, Vector2 position, Vector2 size)
    {
        var image = new GameObject(name, typeof(RectTransform)).AddComponent<RawImage>();
        image.rectTransform.SetParent(transform, false);
        image.rectTransform.anchoredPosition = position;
        image.rectTransform.sizeDelta = size;
        image.texture = texture;
        image.raycastTarget = false;
        return image;
    }

    private void AddAnimation(AnimationAssets assets, string animation, Vector2 position, Material material)
    {
        if (assets == null || assets.texture == null || assets.atlas == null ||
            assets.skeleton == null || material == null)
        {
            Debug.LogError($"Missing assets for loading animation '{animation}'.");
            return;
        }

        var atlas = SpineAtlasAsset.CreateRuntimeInstance(assets.atlas, new[] { assets.texture }, material, false);
        runtimeAssets.AddRange(atlas.materials);
        runtimeAssets.Add(atlas);
        // SkeletonGraphic multiplies skeleton units by the canvas reference pixels per unit (100).
        var skeleton = SkeletonDataAsset.CreateRuntimeInstance(assets.skeleton, atlas, false);
        runtimeAssets.Add(skeleton);

        var components = SkeletonGraphic.NewSkeletonGraphicGameObject(skeleton, transform, material);
        components.skeletonRenderer.name = assets.texture.name;
        components.skeletonRenderer.rectTransform.anchoredPosition = position;
        components.skeletonRenderer.raycastTarget = false;
        components.skeletonAnimation.unscaledTime = true;
        components.skeletonAnimation.AnimationState?.SetAnimation(0, animation, true);
        loadingAnimations.Add(components.skeletonAnimation);
    }

    private void SetProgress(float progress)
    {
        float amount = Mathf.Clamp01(progress);
        fill.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, FillWidth * amount);
        fill.uvRect = new Rect(0f, 0f, amount, 1f);
    }

    private void OnDestroy()
    {
        if (activeScreen == this) activeScreen = null;
        foreach (Object asset in runtimeAssets)
            Destroy(asset);
    }

    private static LoadingAssets GetAssets()
    {
#if UNITY_EDITOR
        return sharedAssets ??= LoadAssetsInEditor();
#else
        return sharedAssets ?? new LoadingAssets();
#endif
    }

    [System.Serializable]
    private sealed class LoadingAssets
    {
        public Texture2D background;
        public Texture2D track;
        public Texture2D fill;
        public Material material;
        public AnimationAssets obit;
        public AnimationAssets text;
    }

    [System.Serializable]
    private sealed class AnimationAssets
    {
        public Texture2D texture;
        public TextAsset atlas;
        public TextAsset skeleton;
    }

#if UNITY_EDITOR
    private static LoadingAssets LoadAssetsInEditor()
    {
        return new LoadingAssets
        {
            background = LoadRequired<Texture2D>("Assets/Sprites/loading_background.png"),
            track = LoadRequired<Texture2D>("Assets/Sprites/loading_track.png"),
            fill = LoadRequired<Texture2D>("Assets/Sprites/loading_fill.png"),
            material = LoadRequired<Material>("Assets/Spine/LoadingSpine.mat"),
            obit = LoadAnimationAssets("Assets/Spine/loading_obit"),
            text = LoadAnimationAssets("Assets/Spine/loading_text")
        };
    }

    private static AnimationAssets LoadAnimationAssets(string path)
    {
        return new AnimationAssets
        {
            texture = LoadRequired<Texture2D>(path + ".png"),
            atlas = LoadRequired<TextAsset>(path + "_atlas.bytes"),
            skeleton = LoadRequired<TextAsset>(path + "_skeleton.bytes")
        };
    }

    private static T LoadRequired<T>(string path) where T : Object
    {
        var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
            throw new UnityEditor.Build.BuildFailedException($"Missing loading screen asset: {path}");
        return asset;
    }

    internal static void IncludeAssetsInBuildScene(Scene scene)
    {
        var assets = LoadAssetsInEditor();
        var reference = new GameObject("Loading Screen Assets");
        if (reference.scene != scene)
            SceneManager.MoveGameObjectToScene(reference, scene);
        reference.AddComponent<SceneLoadingScreen>().sceneAssets = assets;
    }
#endif
}

#if UNITY_EDITOR
// Attach references to the processed scene in the build without saving changes to the source scene.
public sealed class SceneLoadingBuildProcessor : UnityEditor.Build.IProcessSceneWithReport
{
    public int callbackOrder => 0;

    public void OnProcessScene(Scene scene, UnityEditor.Build.Reporting.BuildReport report)
    {
        if (report != null)
            SceneLoadingScreen.IncludeAssetsInBuildScene(scene);
    }
}
#endif
