using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AttackProgressUI : MonoBehaviour
{
    [Header("Stars")]
    [SerializeField] private List<Image> starImages = new();

    [Header("Layout Size")]
    [SerializeField] private float starWidth = 42f;
    [SerializeField] private float starHeight = 42f;
    [SerializeField] private float spacing = 4f;
    [SerializeField] private float paddingLeft = 16f;
    [SerializeField] private float paddingRight = 16f;
    [SerializeField] private float paddingTop = 8f;
    [SerializeField] private float paddingBottom = 8f;

    [Header("State Colors")]
    [SerializeField] private Color emptyColor = new Color(1f, 1f, 1f, 0.25f);
    [SerializeField] private Color filledColor = Color.white;

    private RectTransform rectTransform;
    private HorizontalLayoutGroup layoutGroup;
    private Image panelImage;
    private int currentTargetCount;

    public int TargetCount => currentTargetCount;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        layoutGroup = GetComponent<HorizontalLayoutGroup>();
        panelImage = GetComponent<Image>();

        ApplyLayoutSettings();
        Clear();
    }

    /// <summary>
    /// 목표 노트 수만큼 별 UI를 표시.
    /// </summary>
    public void Setup(int targetCount)
    {
        currentTargetCount = Mathf.Clamp(targetCount, 1, starImages.Count);

        ApplyLayoutSettings();
        ResizePanel(currentTargetCount);

        if (panelImage != null)
        {
            panelImage.enabled = true;
        }

        for (int i = 0; i < starImages.Count; i++)
        {
            if (starImages[i] == null) continue;

            bool shouldShow = i < currentTargetCount;
            starImages[i].gameObject.SetActive(shouldShow);
            starImages[i].color = emptyColor;
            starImages[i].raycastTarget = false;
            starImages[i].preserveAspect = true;

            LayoutElement layoutElement = starImages[i].GetComponent<LayoutElement>();
            if (layoutElement != null)
            {
                layoutElement.preferredWidth = starWidth;
                layoutElement.preferredHeight = starHeight;
                layoutElement.flexibleWidth = 0f;
                layoutElement.flexibleHeight = 0f;
            }
        }

        SetProgress(0);
        LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
    }

    /// <summary>
    /// 현재 성공 입력 수만큼 별을 채움.
    /// </summary>
    public void SetProgress(int filledCount)
    {
        int safeFilledCount = Mathf.Clamp(filledCount, 0, currentTargetCount);

        for (int i = 0; i < starImages.Count; i++)
        {
            if (starImages[i] == null) continue;
            if (i >= currentTargetCount) continue;

            starImages[i].color = i < safeFilledCount ? filledColor : emptyColor;
        }
    }

    /// <summary>
    /// 별 진행도 UI를 숨김.
    /// </summary>
    public void Clear()
    {
        currentTargetCount = 0;

        if (panelImage != null)
        {
            panelImage.enabled = false;
        }

        for (int i = 0; i < starImages.Count; i++)
        {
            if (starImages[i] == null) continue;

            starImages[i].gameObject.SetActive(false);
        }

        ResizePanel(1);
    }

    private void ApplyLayoutSettings()
    {
        if (layoutGroup == null) return;

        layoutGroup.padding.left = Mathf.RoundToInt(paddingLeft);
        layoutGroup.padding.right = Mathf.RoundToInt(paddingRight);
        layoutGroup.padding.top = Mathf.RoundToInt(paddingTop);
        layoutGroup.padding.bottom = Mathf.RoundToInt(paddingBottom);

        layoutGroup.spacing = spacing;
        layoutGroup.childAlignment = TextAnchor.MiddleCenter;

        layoutGroup.childControlWidth = true;
        layoutGroup.childControlHeight = true;
        layoutGroup.childForceExpandWidth = false;
        layoutGroup.childForceExpandHeight = false;
    }

    private void ResizePanel(int targetCount)
    {
        if (rectTransform == null) return;

        int safeCount = Mathf.Clamp(targetCount, 1, Mathf.Max(1, starImages.Count));

        float width =
            paddingLeft +
            paddingRight +
            safeCount * starWidth +
            Mathf.Max(0, safeCount - 1) * spacing;

        float height =
            paddingTop +
            paddingBottom +
            starHeight;

        rectTransform.sizeDelta = new Vector2(width, height);
    }
}