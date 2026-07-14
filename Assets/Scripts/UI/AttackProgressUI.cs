using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AttackProgressUI : MonoBehaviour
{
    [Header("Stars")]
    [SerializeField] private List<Image> starImages = new();

    [Header("State Colors")]
    [SerializeField] private Color emptyColor = new Color(1f, 1f, 1f, 0.25f);
    [SerializeField] private Color filledColor = Color.white;

    private int currentTargetCount;

    public int TargetCount => currentTargetCount;

    private void Awake()
    {
        Clear();
    }

    /// <summary>
    /// 목표 노트 수만큼 미리 배치된 별을 표시.
    /// </summary>
    public void Setup(int targetCount)
    {
        currentTargetCount = Mathf.Clamp(targetCount, 1, starImages.Count);

        for (int i = 0; i < starImages.Count; i++)
        {
            if (starImages[i] == null) continue;

            bool shouldShow = i < currentTargetCount;
            starImages[i].gameObject.SetActive(shouldShow);
            starImages[i].color = emptyColor;
            starImages[i].raycastTarget = false;
        }

        SetProgress(0);
    }

    /// <summary>
    /// 현재 입력 성공 개수만큼 별을 채움.
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

        for (int i = 0; i < starImages.Count; i++)
        {
            if (starImages[i] == null) continue;

            starImages[i].gameObject.SetActive(false);
        }
    }
}