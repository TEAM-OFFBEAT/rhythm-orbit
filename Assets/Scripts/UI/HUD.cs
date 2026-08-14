using System.Collections;
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
    [SerializeField] private TMP_Text bpmText;
    [SerializeField] private StarsRenderer starsRenderer;

    [Header("Panel Messages")]
    [SerializeField] private GameObject myPanelBubble;
    [SerializeField] private TMP_Text myPanelMessageLabel;
    [SerializeField] private GameObject opponentPanelBubble;
    [SerializeField] private TMP_Text opponentPanelMessageLabel;

    [Header("Player HUD Slots")]
    [SerializeField] private HUDPlayerSlotUI mySlot;
    [SerializeField] private HUDPlayerSlotUI opponentSlot;

    // 로컬 플레이어의 ID.
    // 네트워크 모드에서는 NetworkManager.LocalPlayerId를 GameManager가 전달한다.
    // 로컬 테스트 모드에서는 기본값 1을 사용한다.
    private int localPlayerId = 1;

    private Coroutine myPanelHideCoroutine;
    private Coroutine opponentPanelHideCoroutine;

    /// <summary>
    /// 로컬 플레이어 기준으로 My/Opponent 슬롯의 이름과 Host/Client 역할 표시를 설정한다.
    /// GameManager.Awake()에서 로컬 플레이어 ID를 읽은 뒤 호출한다.
    /// </summary>
    public void SetupPlayerPerspective(int localPlayerId)
    {
        this.localPlayerId = Mathf.Clamp(localPlayerId, 1, 2);

        ClearJudgments();
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
    /// 공격 턴의 노트 생성 진행도를 별 UI에 반영한다.
    /// actualCount는 초과 탭 포함 실제 탭 수, targetCount는 목표 노트 수.
    /// </summary>
    public void UpdateAttackProgress(int actualCount, int targetCount)
    {
        if (starsRenderer == null) return;

        if (starsRenderer.TargetCount != targetCount)
            starsRenderer.Setup(targetCount);

        int filledCount = Mathf.Min(actualCount, targetCount);
        starsRenderer.SetProgress(filledCount, actualCount);
    }

    /// <summary>
    /// 방어 판정 PERFECT/GOOD 시 해당 인덱스의 별을 3단계로 전환한다.
    /// GameManager의 판정 핸들러에서 호출한다.
    /// </summary>
    public void SetStarSuccess(int noteIndex)
    {
        starsRenderer?.SetStarSuccess(noteIndex);
    }

    /// <summary>
    /// 공격자 패널에만 메세지를 표시한다.
    /// 공격자가 로컬 플레이어이면 내 패널, 아니면 상대 패널에 표시한다.
    /// </summary>
    public void ShowInAttackerPanel(string message, AttackSide attackerSide)
    {
        bool isLocalAttacker = (attackerSide == AttackSide.P1 && localPlayerId == 1) ||
                               (attackerSide == AttackSide.P2 && localPlayerId == 2);
        if (isLocalAttacker)
            ShowMyPanelMessage(message);
        else
            ShowOpponentPanelMessage(message);
    }

    /// <summary>
    /// 방어자 패널에만 메세지를 표시한다.
    /// 방어자가 로컬 플레이어이면 내 패널, 아니면 상대 패널에 표시한다.
    /// </summary>
    public void ShowInDefenderPanel(string message, AttackSide attackerSide)
    {
        bool isLocalDefender = (attackerSide == AttackSide.P1 && localPlayerId == 2) ||
                               (attackerSide == AttackSide.P2 && localPlayerId == 1);
        if (isLocalDefender)
            ShowMyPanelMessage(message);
        else
            ShowOpponentPanelMessage(message);
    }

    /// <summary>
    /// 내 패널 메세지 레이블에 텍스트를 표시하고, 0.5박자 후 자동으로 숨긴다.
    /// </summary>
    public void ShowMyPanelMessage(string message)
    {
        if (myPanelMessageLabel != null) myPanelMessageLabel.text = message;
        bool show = !string.IsNullOrEmpty(message);
        if (myPanelBubble != null) myPanelBubble.SetActive(show);
        else if (myPanelMessageLabel != null) myPanelMessageLabel.gameObject.SetActive(show);

        if (myPanelHideCoroutine != null) StopCoroutine(myPanelHideCoroutine);
        myPanelHideCoroutine = show ? StartCoroutine(HideMyPanel()) : null;
    }

    /// <summary>
    /// 상대 패널 메세지 레이블에 텍스트를 표시하고, 0.5박자 후 자동으로 숨긴다.
    /// </summary>
    public void ShowOpponentPanelMessage(string message)
    {
        if (opponentPanelMessageLabel != null) opponentPanelMessageLabel.text = message;
        bool show = !string.IsNullOrEmpty(message);
        if (opponentPanelBubble != null) opponentPanelBubble.SetActive(show);
        else if (opponentPanelMessageLabel != null) opponentPanelMessageLabel.gameObject.SetActive(show);

        if (opponentPanelHideCoroutine != null) StopCoroutine(opponentPanelHideCoroutine);
        opponentPanelHideCoroutine = show ? StartCoroutine(HideOpponentPanel()) : null;
    }

    /// <summary>
    /// 양쪽 패널 메세지를 모두 즉시 숨긴다. 턴 시작 시 GameManager가 호출한다.
    /// </summary>
    public void ClearPanelMessages()
    {
        if (myPanelHideCoroutine != null) { StopCoroutine(myPanelHideCoroutine); myPanelHideCoroutine = null; }
        if (opponentPanelHideCoroutine != null) { StopCoroutine(opponentPanelHideCoroutine); opponentPanelHideCoroutine = null; }

        if (myPanelBubble != null) myPanelBubble.SetActive(false);
        else if (myPanelMessageLabel != null) myPanelMessageLabel.gameObject.SetActive(false);

        if (opponentPanelBubble != null) opponentPanelBubble.SetActive(false);
        else if (opponentPanelMessageLabel != null) opponentPanelMessageLabel.gameObject.SetActive(false);
    }

    private IEnumerator HideMyPanel()
    {
        yield return new WaitForSeconds(GetHalfBeatSeconds());
        if (myPanelBubble != null) myPanelBubble.SetActive(false);
        else if (myPanelMessageLabel != null) myPanelMessageLabel.gameObject.SetActive(false);
        myPanelHideCoroutine = null;
    }

    private IEnumerator HideOpponentPanel()
    {
        yield return new WaitForSeconds(GetHalfBeatSeconds());
        if (opponentPanelBubble != null) opponentPanelBubble.SetActive(false);
        else if (opponentPanelMessageLabel != null) opponentPanelMessageLabel.gameObject.SetActive(false);
        opponentPanelHideCoroutine = null;
    }

    // 0.5박자 = 1반박 = RhythmClock.GetNoteDuration()
    private float GetHalfBeatSeconds()
    {
        if (RhythmClock.Instance == null) return 0.3f;
        return (float)RhythmClock.Instance.GetNoteDuration();
    }

    /// <summary>
    /// 공격 진행도 별 UI를 초기화한다.
    /// 턴 전환 또는 공격 종료 후 필요할 때 GameManager가 호출한다.
    /// </summary>
    public void ClearAttackProgress()
    {
        starsRenderer?.Clear();
    }
}