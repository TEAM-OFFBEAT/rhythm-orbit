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
    [SerializeField] private BpmGaugeUI bpmGauge;
    [SerializeField] private StarsRenderer starsRenderer;

    [Header("Panel Messages")]
    [SerializeField] private GameObject myPanelBubble;
    [SerializeField] private TMP_Text myPanelMessageLabel;
    [SerializeField] private GameObject opponentPanelBubble;
    [SerializeField] private TMP_Text opponentPanelMessageLabel;
    [SerializeField] private string defaultBubbleMessage = "...";

    [Header("Player HUD Slots")]
    [SerializeField] private HUDPlayerSlotUI mySlot;
    [SerializeField] private HUDPlayerSlotUI opponentSlot;

    // 로컬 플레이어의 ID.
    // 네트워크 모드에서는 NetworkManager.LocalPlayerId를 GameManager가 전달한다.
    // 로컬 테스트 모드에서는 기본값 1을 사용한다.
    private int localPlayerId = 1;

    private Coroutine myPanelHideCoroutine;
    private Coroutine opponentPanelHideCoroutine;

    private float currentBpm;
    private Coroutine bpmTextCoroutine;

    private void Awake()
    {
        if (myPanelBubble != null) myPanelBubble.SetActive(true);
        if (myPanelMessageLabel != null) myPanelMessageLabel.text = defaultBubbleMessage;
        if (opponentPanelBubble != null) opponentPanelBubble.SetActive(true);
        if (opponentPanelMessageLabel != null) opponentPanelMessageLabel.text = defaultBubbleMessage;
    }

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
    /// 현재 턴을 소유한 플레이어의 패널을 하이라이트하고 상대 패널은 기본 상태로 전환한다.
    /// 공격 phase 진입 시 공격자 ID, 방어 phase 진입 시 방어자 ID를 전달한다.
    /// </summary>
    public void SetTurnOwner(int activePlayerId)
    {
        bool mySlotActive = activePlayerId == localPlayerId;
        mySlot?.SetActiveState(mySlotActive);
        opponentSlot?.SetActiveState(!mySlotActive);
    }

    /// <summary>
    /// My/Opponent 슬롯의 판정 라벨과 패널 상태(스프라이트, 글로우)를 기본 상태로 초기화한다.
    /// 턴 전환 및 라운드 인트로 진입 시 GameManager가 호출한다.
    /// </summary>
    public void ClearJudgments()
    {
        mySlot?.ClearJudgment();
        opponentSlot?.ClearJudgment();
        mySlot?.SetActiveState(false);
        opponentSlot?.SetActiveState(false);
    }

    /// <summary>
    /// 현재 BPM 수치를 텍스트와 게이지 바에 동시에 표시한다.
    /// 첫 호출 시 텍스트를 즉시 설정하고, 이후 BPM 상승 시 애니메이션을 적용한다.
    /// BPM 단계 변경 시 GameManager가 호출한다.
    /// </summary>
    public void UpdateBpm(float bpm)
    {
        bpmGauge?.SetBpm(bpm);

        if (bpmText == null) { currentBpm = bpm; return; }

        if (currentBpm <= 0f)
        {
            bpmText.text = $"BPM {bpm:0}";
            currentBpm = bpm;
            return;
        }

        if (bpmTextCoroutine != null) StopCoroutine(bpmTextCoroutine);
        float from = currentBpm;
        currentBpm = bpm;
        bpmTextCoroutine = StartCoroutine(AnimateBpmText(from, bpm));
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
            ShowOpponentPanelMessage(message);
        else
            ShowMyPanelMessage(message);
    }

    /// <summary>
    /// 내 패널 메세지 레이블에 텍스트를 표시하고, 2박자 후 디폴트 메세지로 되돌린다.
    /// </summary>
    public void ShowMyPanelMessage(string message)
    {
        if (myPanelHideCoroutine != null) { StopCoroutine(myPanelHideCoroutine); myPanelHideCoroutine = null; }

        if (string.IsNullOrEmpty(message))
        {
            if (myPanelMessageLabel != null) myPanelMessageLabel.text = defaultBubbleMessage;
            return;
        }

        if (myPanelMessageLabel != null) myPanelMessageLabel.text = message;
        myPanelHideCoroutine = StartCoroutine(RevertMyPanelToDefault());
    }

    /// <summary>
    /// 상대 패널 메세지 레이블에 텍스트를 표시하고, 2박자 후 디폴트 메세지로 되돌린다.
    /// </summary>
    public void ShowOpponentPanelMessage(string message)
    {
        if (opponentPanelHideCoroutine != null) { StopCoroutine(opponentPanelHideCoroutine); opponentPanelHideCoroutine = null; }

        if (string.IsNullOrEmpty(message))
        {
            if (opponentPanelMessageLabel != null) opponentPanelMessageLabel.text = defaultBubbleMessage;
            return;
        }

        if (opponentPanelMessageLabel != null) opponentPanelMessageLabel.text = message;
        opponentPanelHideCoroutine = StartCoroutine(RevertOpponentPanelToDefault());
    }

    /// <summary>
    /// 양쪽 패널 메세지를 즉시 디폴트 메세지로 초기화한다. 턴 시작 시 GameManager가 호출한다.
    /// </summary>
    public void ClearPanelMessages()
    {
        if (myPanelHideCoroutine != null) { StopCoroutine(myPanelHideCoroutine); myPanelHideCoroutine = null; }
        if (opponentPanelHideCoroutine != null) { StopCoroutine(opponentPanelHideCoroutine); opponentPanelHideCoroutine = null; }

        if (myPanelMessageLabel != null) myPanelMessageLabel.text = defaultBubbleMessage;
        if (opponentPanelMessageLabel != null) opponentPanelMessageLabel.text = defaultBubbleMessage;
    }

    private IEnumerator RevertMyPanelToDefault()
    {
        yield return new WaitForSeconds(GetTwoBeatSeconds());
        if (myPanelMessageLabel != null) myPanelMessageLabel.text = defaultBubbleMessage;
        myPanelHideCoroutine = null;
    }

    private IEnumerator RevertOpponentPanelToDefault()
    {
        yield return new WaitForSeconds(GetTwoBeatSeconds());
        if (opponentPanelMessageLabel != null) opponentPanelMessageLabel.text = defaultBubbleMessage;
        opponentPanelHideCoroutine = null;
    }

    private IEnumerator AnimateBpmText(float from, float to)
    {
        float duration = 8f * 60f / to;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float tEased = 1f - Mathf.Pow(1f - t, 3f);
            bpmText.text = $"BPM {Mathf.Round(Mathf.Lerp(from, to, tEased)):0}";
            yield return null;
        }

        bpmText.text = $"BPM {to:0}";
        bpmTextCoroutine = null;
    }

    // 2박자 = 4반박 = RhythmClock.GetNoteDuration() * 4
    private float GetTwoBeatSeconds()
    {
        if (RhythmClock.Instance == null) return 1.0f;
        return (float)(RhythmClock.Instance.GetNoteDuration() * 4.0);
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