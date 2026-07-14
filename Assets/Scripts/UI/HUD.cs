using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;

public class HUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameManager gameManager;

    [Header("Game Status UI")]
    [SerializeField] private TMP_Text p1JudgmentLabel;
    [SerializeField] private TMP_Text p2JudgmentLabel;
    [SerializeField] private TMP_Text p1SanityText;
    [SerializeField] private TMP_Text p2SanityText;
    [SerializeField] private Slider p1SanitySlider;
    [SerializeField] private Slider p2SanitySlider;
    //[SerializeField] private TMP_Text sanityDamageLabel;
    [SerializeField] private TMP_Text attackMessageLabel;
    [SerializeField] private TMP_Text bpmText;
    [SerializeField] private AttackProgressUI attackProgressUI;
    
    /*
    private float sanityDamagePopupDuration = 1.0f;
    private readonly Queue<string> sanityDamageQueue = new();
    private Coroutine sanityDamageCoroutine;
    */

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
    public void UpdateSanity(int p1, int p2, int maxSanity)
    {
        if (p1SanityText != null)
        {
            p1SanityText.text = $"P1 SANITY {p1} / {maxSanity}";
        }

        if (p2SanityText != null)
        {
            p2SanityText.text = $"P2 SANITY {p2} / {maxSanity}";
        }

        if (p1SanitySlider != null)
        {
            p1SanitySlider.maxValue = maxSanity;
            p1SanitySlider.value = p1;
        }

        if (p2SanitySlider != null)
        {
            p2SanitySlider.maxValue = maxSanity;
            p2SanitySlider.value = p2;
        }
    }

    /// <summary>
    /// 오버로딩
    /// </summary>
    public void UpdateSanity(int p1, int p2)
    {
        UpdateSanity(p1, p2, 100);
    }

    /// <summary>
    /// 현재 BPM 수치를 HUD에 표시.
    /// </summary>
    public void UpdateBpm(float bpm)
    {
        if (bpmText != null)
        {
            bpmText.text = $"BPM {bpm:0}";
        }
    }

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

    /// <summary>
    /// 정신력 감소 팝업 추가
    /// </summary>
    /*
    public void ShowSanityDamage(int playerId, int amount, string reason)
    {
        sanityDamageQueue.Enqueue($"P{playerId} 정신력 -{amount}\n{reason}");

        if (sanityDamageCoroutine == null)
        {
            sanityDamageCoroutine = StartCoroutine(ShowSanityDamageQueue());
        }
    }

    private IEnumerator ShowSanityDamageQueue()
    {
        while (sanityDamageQueue.Count > 0)
        {
            string message = sanityDamageQueue.Dequeue();

            if (sanityDamageLabel != null)
            {
                sanityDamageLabel.text = message;
                sanityDamageLabel.gameObject.SetActive(true);
            }

            yield return new WaitForSeconds(sanityDamagePopupDuration);
        }

        if (sanityDamageLabel != null)
        {
            sanityDamageLabel.text = string.Empty;
            sanityDamageLabel.gameObject.SetActive(false);
        }

        sanityDamageCoroutine = null;
    }
    */
    /// <summary>
    /// 현재 공격 턴의 목표 메시지를 표시.
    /// </summary>
    public void ShowAttackMessage(string message, AttackSide attackerSide)
    {
        if (attackMessageLabel == null) return;

        attackMessageLabel.text = message;
        attackMessageLabel.gameObject.SetActive(true);
    }

    /// <summary>
    /// 공격 진행도 별 UI를 갱신.
    /// </summary>
    public void UpdateAttackProgress(int currentCount, int targetCount)
    {
        if (attackProgressUI == null) return;

        if (attackProgressUI.TargetCount != targetCount)
        {
            attackProgressUI.Setup(targetCount);
        }

        attackProgressUI.SetProgress(currentCount);
    }

    public void ClearAttackProgress()
    {
        if (attackProgressUI == null) return;

        attackProgressUI.Clear();
    }
}
