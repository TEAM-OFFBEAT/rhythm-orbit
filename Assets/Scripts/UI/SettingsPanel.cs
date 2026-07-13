using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsPanel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject settingsPanel;

    [Header("Offset UI")]
    [SerializeField] private Slider keyInputOffsetSlider;
    [SerializeField] private Slider audioOffsetSlider;
    [SerializeField] private TMP_Text keyInputOffsetLabel;
    [SerializeField] private TMP_Text audioOffsetLabel;

    [Header("Calibration UI")]
    [SerializeField] private TMP_Text feedbackLabel;

    private void Start()
    {
        if (JudgeSystem.Instance == null)
        {
            Debug.LogWarning("JudgeSystem.Instance가 없습니다.");
            return;
        }

        if (feedbackLabel != null) feedbackLabel.text = string.Empty;

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
    /// Tab 입력 시 패널을 켜고 끔.
    /// </summary>
    public void OnToggleSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(!settingsPanel.activeSelf);
    }

    /// <summary>
    /// Play Test를 시작.
    /// </summary>
    public void OnPlayTest()
    {
        if (CalibrationManager.Instance == null) return;
        if (feedbackLabel != null) feedbackLabel.text = string.Empty;
        CalibrationManager.Instance.StartPlayTest(UpdateCalibrationFeedback);
    }

    /// <summary>
    /// 탭 입력마다 캘리브레이션 피드백 문자열을 갱신.
    /// </summary>
    public void UpdateCalibrationFeedback(string feedback)
    {
        if (feedbackLabel != null) feedbackLabel.text = feedback;
    }

    private void UpdateKeyInputOffsetLabel(float value)
    {
        if (keyInputOffsetLabel == null) return;
        keyInputOffsetLabel.text = value >= 0f ? $"Offset: +{value:0}ms" : $"Offset: {value:0}ms";
    }

    private void UpdateAudioOffsetLabel(float value)
    {
        if (audioOffsetLabel == null) return;
        audioOffsetLabel.text = value >= 0f ? $"Audio Offset: +{value:0}ms" : $"Audio Offset: {value:0}ms";
    }
}
