using UnityEngine;

/// <summary>
/// 타격 이펙트를 월드 좌표에 생성한다.
/// AttackTurn(노트 생성 시)과 DefenseTurn(Good/Perfect 판정 시) 에서 호출한다.
/// </summary>
public class HitEffectSpawner : SceneSingleton<HitEffectSpawner>
{
    [SerializeField] private GameObject highEffectPrefab;
    [SerializeField] private GameObject lowEffectPrefab;

    /// <summary>
    /// noteType에 맞는 이펙트를 worldPos에 생성한다.
    /// </summary>
    public void Play(NoteType noteType, Vector3 worldPos)
    {
        GameObject prefab = noteType == NoteType.HIGH ? highEffectPrefab : lowEffectPrefab;
        if (prefab == null)
        {
            Debug.LogWarning($"HitEffectSpawner: {noteType} 이펙트 프리팹이 연결되지 않음.");
            return;
        }

        GameObject effect = Instantiate(prefab, worldPos, Quaternion.identity, transform);
        Destroy(effect, 0.4f);
    }
}
