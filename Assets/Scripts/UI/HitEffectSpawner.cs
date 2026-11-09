using UnityEngine;

/// <summary>
/// 타격 이펙트를 월드 좌표에 생성한다.
/// AttackTurn(노트 생성 시)과 DefenseTurn(Good/Perfect 판정 시) 에서 호출한다.
/// </summary>
public class HitEffectSpawner : SceneSingleton<HitEffectSpawner>
{
    [SerializeField] private GameObject highEffectPrefab;
    [SerializeField] private GameObject lowEffectPrefab;
    [SerializeField] private GameObject perfectHighEffectPrefab;
    [SerializeField] private GameObject perfectLowEffectPrefab;
    [SerializeField] private GameObject halfNoteHighEffectPrefab;
    [SerializeField] private GameObject halfNoteLowEffectPrefab;

    [SerializeField] private int normalSortingOrder = 3;
    [SerializeField] private int elevatedSortingOrder = 51;

    private bool _elevated;

    /// <summary>
    /// true면 이후 생성되는 이펙트를 BlackOut 위로 렌더링한다. EVT_ATK_02 진입/종료 시 호출.
    /// </summary>
    public void SetTopLayerMode(bool elevated)
    {
        _elevated = elevated;
        int order = elevated ? elevatedSortingOrder : normalSortingOrder;
        foreach (Transform child in transform)
        {
            foreach (var sr in child.GetComponentsInChildren<SpriteRenderer>())
                sr.sortingOrder = order;
        }
    }

    /// <summary>
    /// noteType에 맞는 이펙트를 worldPos에 생성한다. isPerfect가 true이면 Perfect 전용 이펙트를 사용한다.
    /// </summary>
    public void Play(NoteType noteType, Vector3 worldPos, bool isPerfect = false)
    {
        GameObject prefab;
        if (isPerfect)
            prefab = noteType == NoteType.HIGH ? perfectHighEffectPrefab : perfectLowEffectPrefab;
        else
            prefab = noteType == NoteType.HIGH ? highEffectPrefab : lowEffectPrefab;

        if (prefab == null)
        {
            Debug.LogWarning($"HitEffectSpawner: {noteType} 이펙트 프리팹이 연결되지 않음.");
            return;
        }

        GameObject effect = Instantiate(prefab, worldPos, Quaternion.identity, transform);

        int order = _elevated ? elevatedSortingOrder : normalSortingOrder;
        foreach (var sr in effect.GetComponentsInChildren<SpriteRenderer>())
            sr.sortingOrder = order;

        Destroy(effect, 0.4f);
    }

    /// <summary>
    /// EVT_ATK_01 반쪽 노트 생성 시 호출. flipX가 true이면 이펙트를 좌우반전한다.
    /// </summary>
    public void PlayHalfNote(NoteType noteType, Vector3 worldPos, bool flipX)
    {
        GameObject prefab = noteType == NoteType.HIGH ? halfNoteHighEffectPrefab : halfNoteLowEffectPrefab;
        if (prefab == null)
        {
            Debug.LogWarning($"HitEffectSpawner: HalfNote {noteType} 이펙트 프리팹이 연결되지 않음.");
            return;
        }

        GameObject effect = Instantiate(prefab, worldPos, Quaternion.identity, transform);

        var sr = effect.GetComponent<SpriteRenderer>();
        if (sr != null) sr.flipX = flipX;

        int order = _elevated ? elevatedSortingOrder : normalSortingOrder;
        foreach (var s in effect.GetComponentsInChildren<SpriteRenderer>())
            s.sortingOrder = order;

        Destroy(effect, 0.4f);
    }
}
