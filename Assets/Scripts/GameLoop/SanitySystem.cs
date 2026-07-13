using System;
using UnityEngine;

public class SanitySystem : MonoBehaviour
{
    [Header("Sanity Settings")]
    [SerializeField] private int maxSanity = 100;

    [Header("Attack Penalties")]
    [SerializeField] private int badTimingInputPenalty = 1;
    [SerializeField] private int missingNotePenaltyPerNote = 1;
    [SerializeField] private int extraNotePenaltyPerNote = 1;
    [SerializeField] private bool penalizeDuplicateInput = false;
    [SerializeField] private int duplicateInputPenaltyPerInput = 1;

    [Header("Defense Penalties")]
    [SerializeField] private int defenseMissPenaltyPerNote = 1;

    public event Action<int, int, int> OnSanityChanged;
    public event Action<int, int, string> OnSanityDamaged;
    public event Action<int> OnPlayerDefeated;

    public int MaxSanity => Mathf.Max(1, maxSanity);
    public int P1Sanity { get; private set; }
    public int P2Sanity { get; private set; }

    private void Awake()
    {
        ResetSanity();
    }

    /// <summary>
    /// 두 플레이어의 정신력을 최대 정신력으로 리셋.
    /// </summary>
    public void ResetSanity()
    {
        P1Sanity = MaxSanity;
        P2Sanity = MaxSanity;

        OnSanityChanged?.Invoke(P1Sanity, P2Sanity, MaxSanity);
    }

    /// <summary>
    /// 공격 결과에 따라 공격자의 정신력을 감소.
    /// </summary>
    public void ApplyAttackResult(int attackerPlayerId, AttackResult result)
    {
        ApplyDamage(
            attackerPlayerId,
            result.BadTimingInputCount * badTimingInputPenalty,
            "공격 박자 이탈"
        );

        ApplyDamage(
            attackerPlayerId,
            result.MissingNoteCount * missingNotePenaltyPerNote,
            "공격 노트 부족"
        );

        ApplyDamage(
            attackerPlayerId,
            result.ExtraNoteCount * extraNotePenaltyPerNote,
            "공격 초과 입력"
        );

        if (penalizeDuplicateInput)
        {
            ApplyDamage(
                attackerPlayerId,
                result.DuplicateInputCount * duplicateInputPenaltyPerInput,
                "공격 중복 입력"
            );
        }
    }

    /// <summary>
    /// 방어 결과에 따라 방어자의 정신력을 감소.
    /// </summary>
    /*
    public void ApplyDefenseResult(int defenderPlayerId, DefenseResult result)
    {
        ApplyDamage(
            defenderPlayerId,
            result.MissCount * defenseMissPenaltyPerNote,
            "방어 실패"
        );
    }
    */
    /// <summary>
    /// 방어 MISS 1회에 대한 정신력 감소를 적용.
    /// </summary>
    public void ApplyDefenseMiss(int defenderPlayerId)
    {
        ApplyDamage(
            defenderPlayerId,
            defenseMissPenaltyPerNote,
            "방어 실패"
        );
    }

    /// <summary>
    /// 특정 플레이어의 정신력이 0 이하인지 확인.
    /// </summary>
    public bool IsPlayerDefeated(int playerId)
    {
        return GetSanity(playerId) <= 0;
    }

    private void ApplyDamage(int playerId, int amount, string reason)
    {
        if (amount <= 0) return;

        int current = GetSanity(playerId);
        int next = Mathf.Clamp(current - amount, 0, MaxSanity);
        int actualDamage = current - next;

        if (actualDamage <= 0) return;

        SetSanity(playerId, next);

        OnSanityChanged?.Invoke(P1Sanity, P2Sanity, MaxSanity);
        OnSanityDamaged?.Invoke(playerId, actualDamage, reason);

        Debug.Log($"Sanity Damage / P{playerId} -{actualDamage} / reason: {reason}");

        if (next <= 0)
        {
            OnPlayerDefeated?.Invoke(playerId);
        }
    }

    private int GetSanity(int playerId)
    {
        return playerId == 1 ? P1Sanity : P2Sanity;
    }

    private void SetSanity(int playerId, int value)
    {
        if (playerId == 1)
        {
            P1Sanity = value;
        }
        else
        {
            P2Sanity = value;
        }
    }
}