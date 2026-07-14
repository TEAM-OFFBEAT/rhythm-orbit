using System;
using UnityEngine;

/// <summary>
/// P1/P2의 정신력 수치를 관리하는 시스템.
/// 공격/방어 결과에 따른 정신력 감소를 처리하고,
/// 정신력 변경 및 패배 이벤트를 외부에 알린다.
/// </summary>
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

    /// <summary>
    /// 정신력 수치가 변경될 때 발행.
    /// 전달값: P1 현재 정신력, P2 현재 정신력, 최대 정신력.
    /// HUD가 이 이벤트를 받아 정신력 UI를 갱신한다.
    /// </summary>
    public event Action<int, int, int> OnSanityChanged;

    /// <summary>
    /// 특정 플레이어가 정신력 피해를 받았을 때 발행.
    /// 전달값: playerId, 실제 감소량, 감소 사유.
    /// </summary>
    public event Action<int, int, string> OnSanityDamaged;

    /// <summary>
    /// 특정 플레이어의 정신력이 0 이하가 되었을 때 발행.
    /// 전달값: 패배한 playerId.
    /// </summary>
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
    /// 방어 MISS 1회에 대한 정신력 감소를 방어자에게 즉시 적용한다.
    /// 방어 패널티는 방어 턴 종료 시 한 번에 처리하지 않고,
    /// MISS가 발생하는 순간마다 GameManager에서 호출한다.
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

    /// <summary>
    /// 특정 플레이어의 정신력을 감소시키고 관련 이벤트를 발행한다.
    /// 정신력은 0과 MaxSanity 사이로 보정되며,
    /// 실제 감소량이 있을 때만 이벤트를 발행한다.
    /// </summary>
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

    /// <summary>
    /// playerId에 해당하는 현재 정신력을 반환한다.
    /// playerId가 1이면 P1, 그 외에는 P2로 처리한다.
    /// </summary>
    private int GetSanity(int playerId)
    {
        return playerId == 1 ? P1Sanity : P2Sanity;
    }

    /// <summary>
    /// playerId에 해당하는 플레이어의 정신력 값을 설정한다.
    /// playerId가 1이면 P1, 그 외에는 P2로 처리한다.
    /// </summary>
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