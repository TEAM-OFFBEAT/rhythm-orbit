using UnityEngine;
using TMPro;

/// <summary>
/// 게임 HUD 전체를 관리하는 UI 중계자.
/// GameManager로부터 받은 P1/P2 데이터를 로컬 플레이어 관점의 My/Opponent 슬롯에 맞게 전달한다.
/// </summary>
public class HUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameManager gameManager;

    [Header("Game Status UI")]
    [SerializeField] private TMP_Text attackMessageLabel;
    [SerializeField] private TMP_Text bpmText;
    [SerializeField] private AttackProgressUI attackProgressUI;

    [Header("Player HUD Slots")]
    [SerializeField] private HUDPlayerSlotUI mySlot;
    [SerializeField] private HUDPlayerSlotUI opponentSlot;

    [Header("Default Name")]
    [SerializeField] private string defaultPlayerName = "Player";

    // 로컬 플레이어의 ID.
    // 네트워크 모드에서는 NetworkManager.LocalPlayerId를 GameManager가 전달한다.
    // 로컬 테스트 모드에서는 기본값 1을 사용한다.
    private int localPlayerId = 1;

    /// <summary>
    /// 로컬 플레이어 기준으로 My/Opponent 슬롯의 이름과 Host/Client 역할 표시를 설정한다.
    /// GameManager.Awake()에서 로컬 플레이어 ID를 읽은 뒤 호출한다.
    /// </summary>
    public void SetupPlayerPerspective(int localPlayerId)
    {
        this.localPlayerId = Mathf.Clamp(localPlayerId, 1, 2);

        int opponentPlayerId = this.localPlayerId == 1 ? 2 : 1;

        mySlot?.SetPlayerName(defaultPlayerName);
        opponentSlot?.SetPlayerName(defaultPlayerName);

        mySlot?.SetHostRole(IsHostPlayer(this.localPlayerId));
        opponentSlot?.SetHostRole(IsHostPlayer(opponentPlayerId));

        ClearJudgments();
    }

    /// <summary>
    /// 현재 규칙상 P1은 Host, P2는 Client로 취급한다.
    /// </summary>
    private bool IsHostPlayer(int playerId)
    {
        return playerId == 1;
    }

    /// <summary>
    /// P1/P2 정신력 값을 로컬 플레이어 관점의 My/Opponent 슬롯에 맞게 표시한다.
    /// SanitySystem.OnSanityChanged 이벤트를 받은 GameManager가 호출한다.
    /// </summary>
    public void UpdateSanity(int p1Sanity, int p2Sanity, int maxSanity)
    {
        int mySanity = localPlayerId == 1 ? p1Sanity : p2Sanity;
        int opponentSanity = localPlayerId == 1 ? p2Sanity : p1Sanity;

        mySlot?.UpdateSanity(mySanity, maxSanity);
        opponentSlot?.UpdateSanity(opponentSanity, maxSanity);
    }

    /// <summary>
    /// 테스트용 정신력 갱신 함수.
    /// maxSanity를 100으로 가정한다.
    /// </summary>
    public void UpdateSanity(int p1Sanity, int p2Sanity)
    {
        UpdateSanity(p1Sanity, p2Sanity, 100);
    }

    /// <summary>
    /// 방어자 판정 결과를 My/Opponent 슬롯 중 올바른 위치에 표시한다.
    /// attackerSide가 P1이면 방어자는 P2, attackerSide가 P2이면 방어자는 P1이다.
    /// </summary>
    public void ShowJudgment(Judgment judgment, AttackSide attackerSide)
    {
        int attackerPlayerId = attackerSide == AttackSide.P1 ? 1 : 2;
        int defenderPlayerId = attackerPlayerId == 1 ? 2 : 1;

        bool isMyJudgment = defenderPlayerId == localPlayerId;

        if (isMyJudgment)
        {
            mySlot?.ShowJudgment(judgment);
        }
        else
        {
            opponentSlot?.ShowJudgment(judgment);
        }
    }

    /// <summary>
    /// My/Opponent 슬롯의 판정 라벨을 초기화한다.
    /// 턴 전환 시 GameManager가 호출한다.
    /// </summary>
    public void ClearJudgments()
    {
        mySlot?.ClearJudgment();
        opponentSlot?.ClearJudgment();
    }

    /// <summary>
    /// 현재 BPM 수치를 HUD에 표시한다.
    /// BPM 단계 변경 시 GameManager가 호출한다.
    /// </summary>
    public void UpdateBpm(float bpm)
    {
        if (bpmText != null)
        {
            bpmText.text = $"BPM {bpm:0}";
        }
    }

    /// <summary>
    /// HIGH 노트 입력 버튼에서 호출된다.
    /// 실제 입력 처리는 GameManager로 전달한다.
    /// </summary>
    public void OnTapHigh()
    {
        if (gameManager == null)
        {
            Debug.LogWarning("GameManager가 HUD에 연결되지 않았습니다.");
            return;
        }

        gameManager.OnTapHigh();
    }

    /// <summary>
    /// LOW 노트 입력 버튼에서 호출된다.
    /// 실제 입력 처리는 GameManager로 전달한다.
    /// </summary>
    public void OnTapLow()
    {
        if (gameManager == null)
        {
            Debug.LogWarning("GameManager가 HUD에 연결되지 않았습니다.");
            return;
        }

        gameManager.OnTapLow();
    }

    /// <summary>
    /// 현재 공격 턴의 목표 메시지를 표시한다.
    /// AttackTurn.OnAttackMessageSelected 이벤트를 받은 GameManager가 호출한다.
    /// </summary>
    public void ShowAttackMessage(string message, AttackSide attackerSide)
    {
        if (attackMessageLabel == null) return;

        attackMessageLabel.text = message;
        attackMessageLabel.gameObject.SetActive(true);
    }

    /// <summary>
    /// 공격 턴의 노트 생성 진행도를 별 UI에 반영한다.
    /// AttackTurn.OnAttackProgressChanged 이벤트를 받은 GameManager가 호출한다.
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

    /// <summary>
    /// 공격 진행도 별 UI를 초기화한다.
    /// 턴 전환 또는 공격 종료 후 필요할 때 GameManager가 호출한다.
    /// </summary>
    public void ClearAttackProgress()
    {
        attackProgressUI?.Clear();
    }
}