using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class RandomMessageGroup
{
    [Min(1)]
    public int characterCount = 1;

    public List<string> messages = new();
}

public class RandomMessageProvider : MonoBehaviour
{
    [Header("Random Messages")]
    [SerializeField] private List<RandomMessageGroup> messageGroups = new();

    /// <summary>
    /// 글자수에 맞는 메시지 목록 중 하나를 랜덤으로 반환.
    /// </summary>
    public string GetRandomMessage(int characterCount)
    {
        foreach (RandomMessageGroup group in messageGroups)
        {
            if (group.characterCount != characterCount) continue;
            if (group.messages == null || group.messages.Count == 0) break;

            int randomIndex = Random.Range(0, group.messages.Count);
            return group.messages[randomIndex];
        }

        Debug.LogWarning($"{characterCount}글자 공격 메시지가 없습니다.");
        return CreateFallbackMessage(characterCount);
    }

    /// <summary>
    /// 외부 난수 생성기로 메시지를 선택한다. 양쪽 클라이언트가 같은 시드를 쓸 때 사용.
    /// </summary>
    public string GetRandomMessage(int characterCount, System.Random rng)
    {
        foreach (RandomMessageGroup group in messageGroups)
        {
            if (group.characterCount != characterCount) continue;
            if (group.messages == null || group.messages.Count == 0) break;
            return group.messages[rng.Next(0, group.messages.Count)];
        }
        Debug.LogWarning($"{characterCount}글자 공격 메시지가 없습니다.");
        return CreateFallbackMessage(characterCount);
    }

    private string CreateFallbackMessage(int characterCount)
    {
        int safeCount = Mathf.Clamp(characterCount, 1, 7);
        return new string('?', safeCount);
    }
}