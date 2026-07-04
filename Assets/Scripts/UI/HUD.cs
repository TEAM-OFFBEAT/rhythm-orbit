using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HUD : SceneSingleton<HUD>
{
    [SerializeField] private Slider keyInputOffsetSlider;
    [SerializeField] private Slider audioOffsetSlider;
    [SerializeField] private TMP_Text keyInputOffsetLabel;
    [SerializeField] private TMP_Text audioOffsetLabel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Button keyInputDecrementButton;
    [SerializeField] private Button keyInputIncrementButton;
    [SerializeField] private Button audioDecrementButton;
    [SerializeField] private Button audioIncrementButton;
    [SerializeField] private Button playTestButton;
    [SerializeField] private TMP_Text feedbackLabel;

    protected override void Awake()
    {
        base.Awake();
        keyInputOffsetSlider.minValue = -200f;
        keyInputOffsetSlider.maxValue = 200f;
        keyInputOffsetSlider.wholeNumbers = true;
        keyInputOffsetSlider.onValueChanged.AddListener(OnKeyInputOffsetSliderChanged);

        audioOffsetSlider.minValue = -200f;
        audioOffsetSlider.maxValue = 200f;
        audioOffsetSlider.wholeNumbers = true;
        audioOffsetSlider.onValueChanged.AddListener(OnAudioOffsetSliderChanged);

        keyInputDecrementButton.onClick.AddListener(OnDecrementKeyInputOffset);
        keyInputIncrementButton.onClick.AddListener(OnIncrementKeyInputOffset);
        audioDecrementButton.onClick.AddListener(OnDecrementAudioOffset);
        audioIncrementButton.onClick.AddListener(OnIncrementAudioOffset);
        playTestButton.onClick.AddListener(OnPlayTest);
        settingsPanel.SetActive(false);
        feedbackLabel.text = string.Empty;
    }

    private void Start()
    {
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
        JudgeSystem.Instance.SetKeyInputOffset(value);
        UpdateKeyInputOffsetLabel(value);
    }

    /// <summary>
    /// 오디오 오프셋 슬라이더 값 변경 시 JudgeSystem의 AudioOffsetMs를 갱신하고 레이블을 업데이트.
    /// </summary>
    public void OnAudioOffsetSliderChanged(float value)
    {
        JudgeSystem.Instance.SetAudioOffset(value);
        UpdateAudioOffsetLabel(value);
    }

    /// <summary>
    /// 설정 패널 표시 여부를 토글.
    /// </summary>
    public void OnToggleSettings()
    {
        settingsPanel.SetActive(!settingsPanel.activeSelf);
    }

    /// <summary>
    /// 키 입력 오프셋을 1ms 감소.
    /// </summary>
    public void OnDecrementKeyInputOffset()
    {
        keyInputOffsetSlider.value -= 1f;
    }

    /// <summary>
    /// 키 입력 오프셋을 1ms 증가.
    /// </summary>
    public void OnIncrementKeyInputOffset()
    {
        keyInputOffsetSlider.value += 1f;
    }

    /// <summary>
    /// 오디오 오프셋을 1ms 감소.
    /// </summary>
    public void OnDecrementAudioOffset()
    {
        audioOffsetSlider.value -= 1f;
    }

    /// <summary>
    /// 오디오 오프셋을 1ms 증가.
    /// </summary>
    public void OnIncrementAudioOffset()
    {
        audioOffsetSlider.value += 1f;
    }

    /// <summary>
    /// Play Test를 시작.
    /// </summary>
    public void OnPlayTest()
    {
        if (CalibrationManager.Instance == null || CalibrationNoteSpawner.Instance == null) return;
        feedbackLabel.text = string.Empty;
        CalibrationManager.Instance.StartPlayTest(CalibrationNoteSpawner.Instance.SpawnNotes());
    }

    /// <summary>
    /// Play Test 결과 피드백 문자열을 화면에 표시.
    /// </summary>
    public void ShowCalibrationFeedback(string feedback)
    {
        feedbackLabel.text = feedback;
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

    private void UpdateKeyInputOffsetLabel(float value)
    {
        keyInputOffsetLabel.text = value >= 0f
            ? $"Offset: +{value:0}ms"
            : $"Offset: {value:0}ms";
    }

    private void UpdateAudioOffsetLabel(float value)
    {
        audioOffsetLabel.text = value >= 0f
            ? $"Audio Offset: +{value:0}ms"
            : $"Audio Offset: {value:0}ms";
    }
    
}
