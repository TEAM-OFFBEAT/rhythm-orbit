using UnityEngine;

public class NoteCountGenerator : MonoBehaviour
{
    private const int MinAllowedNoteCount = 1;
    private const int MaxAllowedNoteCount = 7;

    [Header("Note Count Range")]
    [SerializeField] private int minNoteCount = 2;
    [SerializeField] private int maxNoteCount = 4;

    /// <summary>
    /// 설정된 범위 안에서 이번 공격에 사용할 노트 수를 랜덤 생성.
    /// </summary>
    public int CreateRandomNoteCount()
    {
        int safeMin = Mathf.Clamp(minNoteCount, MinAllowedNoteCount, MaxAllowedNoteCount);
        int safeMax = Mathf.Clamp(maxNoteCount, safeMin, MaxAllowedNoteCount);

        return Random.Range(safeMin, safeMax + 1);
    }

    /// <summary>
    /// 외부 난수 생성기로 노트 수를 결정한다. 양쪽 클라이언트가 같은 시드를 쓸 때 사용.
    /// </summary>
    public int CreateRandomNoteCount(System.Random rng)
    {
        int safeMin = Mathf.Clamp(minNoteCount, MinAllowedNoteCount, MaxAllowedNoteCount);
        int safeMax = Mathf.Clamp(maxNoteCount, safeMin, MaxAllowedNoteCount);
        return rng.Next(safeMin, safeMax + 1);
    }

    /// <summary>
    /// 라운드별 min/max를 받는 오버로드    
    /// </summary>

    public int CreateRandomNoteCount(System.Random rng, int minNoteCount, int maxNoteCount)
    {
        int safeMin = Mathf.Clamp(minNoteCount, MinAllowedNoteCount, MaxAllowedNoteCount);
        int safeMax = Mathf.Clamp(maxNoteCount, safeMin, MaxAllowedNoteCount);

        return rng.Next(safeMin, safeMax + 1);
    }
}