using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// 게임 HUD 전체를 관리하는 UI 중계자.
/// 두 클라이언트 모두 동일한 절대 화면(P1 좌측 하단, P2 우측 상단)을 표시한다.
/// </summary>
public class HUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameManager gameManager;

    [Header("Game Status UI")]
    [SerializeField] private TMP_Text bpmText;
    [SerializeField] private BpmGaugeUI bpmGauge;
    [SerializeField] private StarsRenderer starsRenderer;
    [SerializeField] private ComboUI comboUI;

    [Header("Panel Messages")]
    [SerializeField] private GameObject p1PanelBubble;
    [SerializeField] private TMP_Text p1PanelMessageLabel;
    [SerializeField] private GameObject p2PanelBubble;
    [SerializeField] private TMP_Text p2PanelMessageLabel;
    [SerializeField] private string defaultBubbleMessage = "...";

    [Header("Player HUD Slots")]
    [SerializeField] private HUDPlayerSlotUI p1Slot;
    [SerializeField] private HUDPlayerSlotUI p2Slot;

    private Coroutine p1PanelHideCoroutine;
    private Coroutine p2PanelHideCoroutine;

    private float currentBpm;
    private Coroutine bpmTextCoroutine;

    private void Awake()
    {
        if (p1PanelBubble != null) p1PanelBubble.SetActive(true);
        if (p1PanelMessageLabel != null) p1PanelMessageLabel.text = defaultBubbleMessage;
        if (p2PanelBubble != null) p2PanelBubble.SetActive(true);
        if (p2PanelMessageLabel != null) p2PanelMessageLabel.text = defaultBubbleMessage;
    }

    /// <summary>
    /// 게임 시작 시 HUD를 초기 상태로 설정한다.
    /// 절대 좌표 렌더링 전환으로 localPlayerId는 더 이상 사용하지 않는다.
    /// </summary>
    public void SetupPlayerPerspective(int localPlayerId)
    {
        ClearJudgments();
    }

    /// <summary>
    /// P1/P2 정신력 값을 절대 좌표 슬롯에 표시한다.
    /// SanitySystem.OnSanityChanged 이벤트를 받은 GameManager가 호출한다.
    /// </summary>
    public void UpdateSanity(int p1Sanity, int p2Sanity, int maxSanity)
    {
        p1Slot?.UpdateSanity(p1Sanity, maxSanity);
        p2Slot?.UpdateSanity(p2Sanity, maxSanity);
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
    /// 방어자의 판정 결과를 해당 플레이어의 절대 좌표 슬롯에 표시한다.
    /// P1이 공격하면 P2(방어자) 슬롯에, P2가 공격하면 P1(방어자) 슬롯에 표시한다.
    /// </summary>
    public void ShowJudgment(Judgment judgment, AttackSide attackerSide)
    {
        if (attackerSide == AttackSide.P1)
            p2Slot?.ShowJudgment(judgment);
        else
            p1Slot?.ShowJudgment(judgment);
    }

    /// <summary>
    /// 현재 활성 플레이어(공격자 또는 방어자)의 슬롯을 하이라이트하고 나머지는 기본 상태로 전환한다.
    /// 공격 phase 진입 시 공격자 ID, 방어 phase 진입 시 방어자 ID를 전달한다.
    /// </summary>
    public void SetTurnOwner(int activePlayerId)
    {
        p1Slot?.SetActiveState(activePlayerId == 1, dimWhenInactive: true);
        p2Slot?.SetActiveState(activePlayerId == 2, dimWhenInactive: true);
    }

    /// <summary>
    /// P1/P2 슬롯의 판정 라벨과 패널 상태(스프라이트, 글로우)를 기본 상태로 초기화한다.
    /// 턴 전환 및 라운드 인트로 진입 시 GameManager가 호출한다.
    /// </summary>
    public void ClearJudgments()
    {
        p1Slot?.ClearJudgment();
        p2Slot?.ClearJudgment();

        p1Slot?.SetActiveState(false, dimWhenInactive: false);
        p2Slot?.SetActiveState(false, dimWhenInactive: false);
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

        if (actualCount > targetCount)
            comboUI?.Reset();
        else
            comboUI?.SetCombo(actualCount);
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
    /// 공격자 패널(P1 공격 시 P1 패널, P2 공격 시 P2 패널)에 메시지를 표시한다.
    /// </summary>
    public void ShowInAttackerPanel(string message, AttackSide attackerSide)
    {
        if (attackerSide == AttackSide.P1)
            ShowP1PanelMessage(message);
        else
            ShowP2PanelMessage(message);
    }

    /// <summary>
    /// 방어자 패널(P1 공격 시 P2 패널, P2 공격 시 P1 패널)에 메시지를 표시한다.
    /// </summary>
    public void ShowInDefenderPanel(string message, AttackSide attackerSide)
    {
        if (attackerSide == AttackSide.P1)
            ShowP2PanelMessage(message);
        else
            ShowP1PanelMessage(message);
    }

    /// <summary>
    /// P1 패널 메세지 레이블에 텍스트를 표시하고, 2박자 후 디폴트 메세지로 되돌린다.
    /// </summary>
    public void ShowP1PanelMessage(string message)
    {
        if (p1PanelHideCoroutine != null) { StopCoroutine(p1PanelHideCoroutine); p1PanelHideCoroutine = null; }

        if (string.IsNullOrEmpty(message))
        {
            if (p1PanelMessageLabel != null) p1PanelMessageLabel.text = defaultBubbleMessage;
            return;
        }

        if (p1PanelMessageLabel != null) p1PanelMessageLabel.text = message;
        p1PanelHideCoroutine = StartCoroutine(RevertP1PanelToDefault());
    }

    /// <summary>
    /// P2 패널 메세지 레이블에 텍스트를 표시하고, 2박자 후 디폴트 메세지로 되돌린다.
    /// </summary>
    public void ShowP2PanelMessage(string message)
    {
        if (p2PanelHideCoroutine != null) { StopCoroutine(p2PanelHideCoroutine); p2PanelHideCoroutine = null; }

        if (string.IsNullOrEmpty(message))
        {
            if (p2PanelMessageLabel != null) p2PanelMessageLabel.text = defaultBubbleMessage;
            return;
        }

        if (p2PanelMessageLabel != null) p2PanelMessageLabel.text = message;
        p2PanelHideCoroutine = StartCoroutine(RevertP2PanelToDefault());
    }

    /// <summary>
    /// 양쪽 패널 메세지를 즉시 디폴트 메세지로 초기화한다.
    /// </summary>
    public void ClearPanelMessages()
    {
        if (p1PanelHideCoroutine != null) { StopCoroutine(p1PanelHideCoroutine); p1PanelHideCoroutine = null; }
        if (p2PanelHideCoroutine != null) { StopCoroutine(p2PanelHideCoroutine); p2PanelHideCoroutine = null; }

        if (p1PanelMessageLabel != null) p1PanelMessageLabel.text = defaultBubbleMessage;
        if (p2PanelMessageLabel != null) p2PanelMessageLabel.text = defaultBubbleMessage;
    }

    private IEnumerator RevertP1PanelToDefault()
    {
        yield return new WaitForSeconds(GetTwoBeatSeconds());
        if (p1PanelMessageLabel != null) p1PanelMessageLabel.text = defaultBubbleMessage;
        p1PanelHideCoroutine = null;
    }

    private IEnumerator RevertP2PanelToDefault()
    {
        yield return new WaitForSeconds(GetTwoBeatSeconds());
        if (p2PanelMessageLabel != null) p2PanelMessageLabel.text = defaultBubbleMessage;
        p2PanelHideCoroutine = null;
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
        comboUI?.Reset();
    }

    /// <summary>현재 콤보를 1 증가시킨다. 방어 턴 성공 판정 시 GameManager가 호출한다.</summary>
    public void IncrementCombo()
    {
        comboUI?.Increment();
    }

    /// <summary>콤보를 초기화한다. 방어 MISS 또는 방어 턴 시작 시 GameManager가 호출한다.</summary>
    public void ResetCombo()
    {
        comboUI?.Reset();
    }
}
