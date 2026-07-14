using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AttackProgressUI : MonoBehaviour
{
    [Header("Star Asset")]
    [SerializeField] private Sprite starSprite;

    [Header("Layout")]
    [SerializeField] private Vector2 starSize = new Vector2(42f, 42f);
    [SerializeField] private float spacing = 4f;

    [Header("State Colors")]
    [SerializeField] private Color emptyColor = new Color(1f, 1f, 1f, 0.25f);
    [SerializeField] private Color filledColor = Color.white;

    private readonly List<Image> stars = new();

    public int TargetCount => stars.Count;

    private void Awake()
    {
        HorizontalLayoutGroup layoutGroup = GetComponent<HorizontalLayoutGroup>();

        if (layoutGroup != null)
        {
            layoutGroup.spacing = spacing;
            layoutGroup.childAlignment = TextAnchor.MiddleCenter;
        }

        gameObject.SetActive(false);
    }

    /// <summary>
    /// 목표 노트 수만큼 별 UI를 생성.
    /// </summary>
    public void Setup(int targetCount)
    {
        Clear();

        int safeCount = Mathf.Clamp(targetCount, 1, 7);

        for (int i = 0; i < safeCount; i++)
        {
            GameObject starObject = new GameObject(
                $"Star_{i + 1}",
                typeof(RectTransform),
                typeof(Image),
                typeof(LayoutElement)
            );

            starObject.transform.SetParent(transform, false);

            Image image = starObject.GetComponent<Image>();
            image.sprite = starSprite;
            image.color = emptyColor;
            image.raycastTarget = false;
            image.preserveAspect = true;

            RectTransform rect = starObject.GetComponent<RectTransform>();
            rect.sizeDelta = starSize;

            LayoutElement layoutElement = starObject.GetComponent<LayoutElement>();
            layoutElement.preferredWidth = starSize.x;
            layoutElement.preferredHeight = starSize.y;

            stars.Add(image);
        }

        gameObject.SetActive(true);
        SetProgress(0);
    }

    /// <summary>
    /// 현재 생성된 노트 수만큼 별을 채움.
    /// </summary>
    public void SetProgress(int filledCount)
    {
        int safeFilledCount = Mathf.Clamp(filledCount, 0, stars.Count);

        for (int i = 0; i < stars.Count; i++)
        {
            stars[i].color = i < safeFilledCount ? filledColor : emptyColor;
        }
    }

    /// <summary>
    /// 별 UI를 모두 제거하고 숨김.
    /// </summary>
    public void Clear()
    {
        foreach (Image star in stars)
        {
            if (star != null)
            {
                Destroy(star.gameObject);
            }
        }

        stars.Clear();
        gameObject.SetActive(false);
    }
}