using UnityEngine;
using TMPro;

public class HUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameManager gameManager;

    [Header("Game Status UI")]
    [SerializeField] private TMP_Text p1JudgmentLabel;
    [SerializeField] private TMP_Text p2JudgmentLabel;

    /// <summary>
    /// 두 플레이어의 판정 라벨을 초기화.
    /// </summary>
    public void ClearJudgments()
    {
        if (p1JudgmentLabel != null) p1JudgmentLabel.text = string.Empty;
        if (p2JudgmentLabel != null) p2JudgmentLabel.text = string.Empty;
    }

    /// <summary>
    /// 방어자 판정 결과를 해당 플레이어 라벨에 표시. attackerSide가 P1이면 P2가 방어자.
    /// </summary>
    public void ShowJudgment(Judgment judgment, AttackSide attackerSide)
    {
        TMP_Text label = attackerSide == AttackSide.P1 ? p2JudgmentLabel : p1JudgmentLabel;
        if (label == null) return;
        label.text = judgment.ToString();
    }

    /// <summary>
    /// 두 플레이어의 정신력 수치를 HUD에 반영.
    /// </summary>
    public void UpdateSanity(int p1, int p2) { }

    /// <summary>
    /// 현재 BPM 수치를 HUD에 표시.
    /// </summary>
    public void UpdateBpm(float bpm) { }

    /// <summary>
    /// Tap Action 입력 시 GameManager를 통해 현재 턴에 맞는 컴포넌트로 전달.
    /// </summary>
    public void OnTap()
    {
        if (gameManager == null)
        {
            Debug.LogWarning("GameManager가 HUD에 연결되지 않았습니다.");
            return;
        }
        gameManager.OnTap();
    }
}
