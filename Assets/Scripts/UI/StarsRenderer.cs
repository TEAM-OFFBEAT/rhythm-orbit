using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 공격/방어 턴 별 진행도를 표시하는 렌더러.
/// 별은 Setup() 시 동적으로 생성되고 Clear() 시 제거된다.
/// 레이아웃은 HorizontalLayoutGroup + ContentSizeFitter로 처리한다.
/// </summary>
public class StarsRenderer : MonoBehaviour
{
    [Header("Star Prefab")]
    [SerializeField] private Image starPrefab;

    [Header("State Sprites")]
    [SerializeField] private Sprite stage1Sprite; // 공격 턴 시작: 빈 슬롯
    [SerializeField] private Sprite stage2Sprite; // 탭 입력 시: 채워진 별
    [SerializeField] private Sprite stage3Sprite; // 판정 성공(PERFECT/GOOD): 확정 별
    [SerializeField] private Sprite excessSprite; // 초과 탭

    private readonly List<Image> activeStars = new();
    private Image panelImage;
    private int currentTargetCount;

    public int TargetCount => currentTargetCount;

    private void Awake()
    {
        panelImage = GetComponent<Image>();
        Clear();
    }

    /// <summary>
    /// 목표 노트 수만큼 1단계 별을 생성한다. 기존 별은 모두 제거된다.
    /// </summary>
    public void Setup(int targetCount)
    {
        Clear();
        currentTargetCount = Mathf.Max(1, targetCount);
        for (int i = 0; i < currentTargetCount; i++)
            SpawnStar(stage1Sprite);

        if (panelImage != null) panelImage.enabled = true;
    }

    /// <summary>
    /// 탭 수에 따라 별 상태를 갱신한다.
    /// actualTapCount가 currentTargetCount를 초과하면 초과 별을 추가 생성한다.
    /// </summary>
    public void SetProgress(int filledCount, int actualTapCount = -1)
    {
        int safeFilledCount = Mathf.Clamp(filledCount, 0, currentTargetCount);
        int safeActual = actualTapCount < 0 ? safeFilledCount : Mathf.Max(safeFilledCount, actualTapCount);

        for (int i = 0; i < currentTargetCount && i < activeStars.Count; i++)
            activeStars[i].sprite = i < safeFilledCount ? stage2Sprite : stage1Sprite;

        // 이전 턴에서 남은 초과 별 제거 (target 범위 별은 건드리지 않음)
        int keepCount = Mathf.Max(currentTargetCount, safeActual);
        for (int i = activeStars.Count - 1; i >= keepCount; i--)
        {
            if (activeStars[i] != null) Destroy(activeStars[i].gameObject);
            activeStars.RemoveAt(i);
        }

        while (activeStars.Count < safeActual)
            SpawnStar(excessSprite);
    }

    /// <summary>
    /// 방어 판정 PERFECT/GOOD 시 해당 인덱스의 별을 3단계(확정)로 전환한다.
    /// </summary>
    public void SetStarSuccess(int noteIndex)
    {
        if (noteIndex < 0 || noteIndex >= currentTargetCount || noteIndex >= activeStars.Count) return;
        activeStars[noteIndex].sprite = stage3Sprite;
    }

    /// <summary>
    /// 생성된 별을 모두 제거하고 패널을 숨긴다.
    /// </summary>
    public void Clear()
    {
        foreach (Image star in activeStars)
            if (star != null) Destroy(star.gameObject);
        activeStars.Clear();
        currentTargetCount = 0;

        if (panelImage != null) panelImage.enabled = false;
    }

    private void SpawnStar(Sprite sprite)
    {
        if (starPrefab == null) return;
        Image star = Instantiate(starPrefab, transform);
        star.sprite = sprite;
        star.color = Color.white;
        activeStars.Add(star);
    }
}
