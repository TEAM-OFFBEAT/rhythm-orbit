using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private AttackTurn attackTurn;

    [Header("Offset UI")]
    [SerializeField] private Slider keyInputOffsetSlider;
    [SerializeField] private Slider audioOffsetSlider;
    [SerializeField] private TMP_Text keyInputOffsetLabel;
    [SerializeField] private TMP_Text audioOffsetLabel;

    [Header("Settings UI")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Button keyInputDecrementButton;
    [SerializeField] private Button keyInputIncrementButton;
    [SerializeField] private Button audioDecrementButton;
    [SerializeField] private Button audioIncrementButton;
    [SerializeField] private Button playTestButton;
    [SerializeField] private TMP_Text feedbackLabel;

    private void Awake()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }

        if (feedbackLabel != null)
        {
            feedbackLabel.text = string.Empty;
        }
    }

    private void Start()
    {
        if (JudgeSystem.Instance == null)
        {
            Debug.LogWarning("JudgeSystem.Instance가 없습니다.");
            return;
        }

        keyInputOffsetSlider.value = (float)JudgeSystem.Instance.KeyInputOffsetMs;
        UpdateKeyInputOffsetLabel(keyInputOffsetSlider.value);

        audioOffsetSlider.value = (float)JudgeSystem.Instance.AudioOffsetMs;
        UpdateAudioOffsetLabel(audioOffsetSlider.value);
    }

    /// <summary>
    /// 키 입력 오프셋 슬라이더 값 변경 시 JudgeSystem의 KeyInputOffsetMs를 갱신하고 레이블을 업데이트.
    /// </summary>
    public void OnKeyInputOffsetSliderChanged(float value)
    {
        if (JudgeSystem.Instance == null) return;

        JudgeSystem.Instance.SetKeyInputOffset(value);
        UpdateKeyInputOffsetLabel(value);
    }

    /// <summary>
    /// 오디오 오프셋 슬라이더 값 변경 시 JudgeSystem의 AudioOffsetMs를 갱신하고 레이블을 업데이트.
    /// </summary>
    public void OnAudioOffsetSliderChanged(float value)
    {
        if (JudgeSystem.Instance == null) return;

        JudgeSystem.Instance.SetAudioOffset(value);
        UpdateAudioOffsetLabel(value);
    }

    /// <summary>
    /// 설정 패널 표시 여부를 토글.
    /// </summary>
    public void OnToggleSettings()
    {
        if (settingsPanel == null) return;

        settingsPanel.SetActive(!settingsPanel.activeSelf);
    }

    /// <summary>
    /// 키 입력 오프셋을 1ms 감소.
    /// </summary>
    public void OnDecrementKeyInputOffset()
    {
        if (keyInputOffsetSlider == null) return;

        keyInputOffsetSlider.value -= 1f;
    }

    /// <summary>
    /// 키 입력 오프셋을 1ms 증가.
    /// </summary>
    public void OnIncrementKeyInputOffset()
    {
        if (keyInputOffsetSlider == null) return;

        keyInputOffsetSlider.value += 1f;
    }

    /// <summary>
    /// 오디오 오프셋을 1ms 감소.
    /// </summary>
    public void OnDecrementAudioOffset()
    {
        if (audioOffsetSlider == null) return;

        audioOffsetSlider.value -= 1f;
    }

    /// <summary>
    /// 오디오 오프셋을 1ms 증가.
    /// </summary>
    public void OnIncrementAudioOffset()
    {
        if (audioOffsetSlider == null) return;

        audioOffsetSlider.value += 1f;
    }

    /// <summary>
    /// Play Test를 시작.
    /// </summary>
    public void OnPlayTest()
    {
        if (CalibrationManager.Instance == null) return;

        if (feedbackLabel != null)
        {
            feedbackLabel.text = string.Empty;
        }

        CalibrationManager.Instance.StartPlayTest(UpdateCalibrationFeedback);
    }

    /// <summary>
    /// 탭 입력마다 캘리브레이션 피드백 문자열을 갱신.
    /// </summary>
    public void UpdateCalibrationFeedback(string feedback)
    {
        if (feedbackLabel != null)
        {
            feedbackLabel.text = feedback;
        }
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
    /// 판정 결과를 화면에 표시.
    /// </summary>
    public void ShowJudgment(Judgment j) { }

    /// <summary>
    /// 턴 전환 배너 텍스트를 화면에 표시.
    /// </summary>
    public void ShowTurnBanner(string label) { }

    /// <summary>
    /// Tap Action 입력 시 AttackTurn에 입력을 전달. PlayerInput Send Messages에서 호출.
    /// </summary>
    public void OnTap()
    {
        if (attackTurn == null)
        {
            Debug.LogWarning("AttackTurn이 HUD에 연결되지 않았습니다.");
            return;
        }

        attackTurn.OnTap();
    }

    private void UpdateKeyInputOffsetLabel(float value)
    {
        if (keyInputOffsetLabel == null) return;

        keyInputOffsetLabel.text = value >= 0f
            ? $"Offset: +{value:0}ms"
            : $"Offset: {value:0}ms";
    }

    private void UpdateAudioOffsetLabel(float value)
    {
        if (audioOffsetLabel == null) return;

        audioOffsetLabel.text = value >= 0f
            ? $"Audio Offset: +{value:0}ms"
            : $"Audio Offset: {value:0}ms";
    }
}