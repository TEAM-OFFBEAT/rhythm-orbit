using System.Collections;
using UnityEngine;

/// <summary>
/// 카메라 이동에 반응하여 배경 레이어를 패럴랙스 스크롤링.
/// 각 레이어는 카메라 이동 거리의 일부만큼 반대 방향으로 이동한다.
/// 라운드 전환 시 레이어별 스프라이트를 서서히 교체한다.
/// </summary>
public class ParallaxBackground : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Transform spaceLayer;
    [SerializeField] private Transform sparkleLayer;
    [SerializeField] private Transform planetLayer;

    [Header("Parallax Factors")]
    [SerializeField] private float spaceFactor = 0.8f;
    [SerializeField] private float sparkleFactor = 0.25f;
    [SerializeField] private float planetFactor = 0.333f;

    [Header("Round Sprites")]
    [SerializeField] private Sprite[] spaceSprites;
    [SerializeField] private Sprite[] sparkleSprites;
    [SerializeField] private Sprite[] planetSprites;

    [Header("Transition")]
    [SerializeField] private float transitionDuration = 1f;

    private float cameraStartX;
    private Vector3 spaceStartLocalPos;
    private Vector3 sparkleStartLocalPos;
    private Vector3 planetStartLocalPos;

    private SpriteRenderer spaceRenderer;
    private SpriteRenderer sparkleRenderer;
    private SpriteRenderer planetRenderer;

    private void Awake()
    {
        spaceRenderer   = spaceLayer?.GetComponent<SpriteRenderer>();
        sparkleRenderer = sparkleLayer?.GetComponent<SpriteRenderer>();
        planetRenderer  = planetLayer?.GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        cameraStartX = targetCamera.transform.position.x;
        spaceStartLocalPos   = spaceLayer   != null ? spaceLayer.localPosition   : Vector3.zero;
        sparkleStartLocalPos = sparkleLayer != null ? sparkleLayer.localPosition : Vector3.zero;
        planetStartLocalPos  = planetLayer  != null ? planetLayer.localPosition  : Vector3.zero;
    }

    private void LateUpdate()
    {
        float cameraDelta = targetCamera.transform.position.x - cameraStartX;

        if (spaceLayer != null)
        {
            Vector3 pos = spaceStartLocalPos;
            pos.x += cameraDelta * spaceFactor;
            spaceLayer.localPosition = pos;
        }

        if (sparkleLayer != null)
        {
            Vector3 pos = sparkleStartLocalPos;
            pos.x -= cameraDelta * sparkleFactor;
            sparkleLayer.localPosition = pos;
        }

        if (planetLayer != null)
        {
            Vector3 pos = planetStartLocalPos;
            pos.x -= cameraDelta * planetFactor;
            planetLayer.localPosition = pos;
        }
    }

    /// <summary>
    /// 라운드 인덱스에 해당하는 스프라이트로 배경 레이어를 전환한다.
    /// animate가 true이면 페이드 아웃 → 교체 → 페이드 인 순서로 서서히 전환한다.
    /// </summary>
    public void ApplyRound(int roundIndex, bool animate = true)
    {
        ApplyLayer(spaceRenderer,   spaceSprites,   roundIndex, animate);
        ApplyLayer(sparkleRenderer, sparkleSprites, roundIndex, animate);
        ApplyLayer(planetRenderer,  planetSprites,  roundIndex, animate);
    }

    private void ApplyLayer(SpriteRenderer renderer, Sprite[] sprites, int roundIndex, bool animate)
    {
        if (renderer == null) return;
        var sprite = GetSprite(sprites, roundIndex);
        if (sprite == null) return;

        if (animate && transitionDuration > 0f)
            StartCoroutine(CrossfadeSprite(renderer, sprite));
        else
            renderer.sprite = sprite;
    }

    private static Sprite GetSprite(Sprite[] sprites, int index)
    {
        if (sprites == null || sprites.Length == 0) return null;
        return sprites[Mathf.Clamp(index, 0, sprites.Length - 1)];
    }

    private IEnumerator CrossfadeSprite(SpriteRenderer target, Sprite newSprite)
    {
        if (target.sprite == newSprite) yield break;

        // 구 스프라이트를 임시 자식으로 보존해 두고, 새 스프라이트를 위에 서서히 올린다.
        // 이 방식은 구 스프라이트가 항상 보이므로 투명해지는 순간(플래시)이 없다.
        var tempGo = new GameObject("_fade");
        tempGo.transform.SetParent(target.transform, false);
        var temp = tempGo.AddComponent<SpriteRenderer>();
        temp.sprite = target.sprite;
        temp.material = target.material;
        temp.sortingLayerID = target.sortingLayerID;
        temp.sortingOrder = target.sortingOrder - 1;
        temp.color = Color.white;

        target.sprite = newSprite;
        target.color  = new Color(1f, 1f, 1f, 0f);

        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / transitionDuration);
            target.color = new Color(1f, 1f, 1f, t);
            temp.color   = new Color(1f, 1f, 1f, 1f - t);
            yield return null;
        }

        target.color = Color.white;
        Destroy(tempGo);
    }
}
