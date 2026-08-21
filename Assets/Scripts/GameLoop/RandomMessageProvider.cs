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

    /// <summary>
    /// 방어 턴 종료 시 방어자 패널에 표시할 메세지.
    /// MISS 인덱스 위치의 글자를 노이즈 기호로 교체하고, 공격/방어 성공 여부로 종결 기호를 결정한다.
    /// judgments가 attackMessage보다 짧으면(공격 노트 미달) 나머지 위치도 노이즈로 표시한다.
    /// </summary>
    public string GetDefenderPanelMessage(string attackMessage, Judgment[] judgments, bool attackSuccess)
    {
        if (string.IsNullOrEmpty(attackMessage))
            return "";

        char[] chars = attackMessage.ToCharArray();
        bool defenseSuccess = true;

        int judgeLimit = Mathf.Min(chars.Length, judgments?.Length ?? 0);

        for (int i = 0; i < judgeLimit; i++)
        {
            if (judgments[i] == Judgment.MISS)
            {
                chars[i] = RandomNoteChar();
                defenseSuccess = false;
            }
        }

        // 공격 노트가 모자라 판정을 받지 못한 위치는 깨진 글자로 표시
        for (int i = judgeLimit; i < chars.Length; i++)
            chars[i] = RandomNoteChar();

        if (!attackSuccess && !defenseSuccess)
            return "???";

        string suffix = (attackSuccess, defenseSuccess) switch
        {
            (true,  true)  => "!",
            _              => "?",
        };

        return new string(chars) + suffix;
    }

    private static char RandomNoteChar() => (char)Random.Range(0x2669, 0x266D);

    private string CreateFallbackMessage(int characterCount)
    {
        int safeCount = Mathf.Clamp(characterCount, 1, 7);
        return new string('?', safeCount);
    }
}
