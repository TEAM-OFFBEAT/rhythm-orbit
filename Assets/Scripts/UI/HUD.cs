using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HUD : SceneSingleton<HUD>
{
    [SerializeField] private JudgeSystem judgeSystem;
    [SerializeField] private Slider offsetSlider;
    [SerializeField] private TMP_Text offsetLabel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Button decrementButton;
    [SerializeField] private Button incrementButton;
    [SerializeField] private Button playTestButton;
    [SerializeField] private TMP_Text feedbackLabel;
    [SerializeField] private CalibrationManager calibrationManager;

    protected override void Awake()
    {
        base.Awake();
        if (judgeSystem == null) judgeSystem = FindAnyObjectByType<JudgeSystem>();
        if (calibrationManager == null) calibrationManager = FindAnyObjectByType<CalibrationManager>();
        offsetSlider.minValue = -200f;
        offsetSlider.maxValue = 200f;
        offsetSlider.wholeNumbers = true;
        offsetSlider.value = (float)judgeSystem.AudioOffsetMs;
        UpdateOffsetLabel(offsetSlider.value);
        offsetSlider.onValueChanged.AddListener(OnOffsetSliderChanged);
        decrementButton.onClick.AddListener(OnDecrementOffset);
        incrementButton.onClick.AddListener(OnIncrementOffset);
        playTestButton.onClick.AddListener(OnPlayTest);
        settingsPanel.SetActive(false);
        feedbackLabel.text = string.Empty;
    }

    /// <summary>슬라이더 값 변경 시 JudgeSystem의 AudioOffsetMs를 갱신하고 레이블을 업데이트. Slider.onValueChanged에서 호출.</summary>
    public void OnOffsetSliderChanged(float value)
    {
        judgeSystem.SetAudioOffset(value);
        UpdateOffsetLabel(value);
    }

    /// <summary>설정 패널 표시 여부를 토글. PlayerInput(SendMessages)에서 호출.</summary>
    public void OnToggleSettings()
    {
        settingsPanel.SetActive(!settingsPanel.activeSelf);
    }

    /// <summary>오프셋을 1ms 감소. decrementButton.onClick에서 호출.</summary>
    public void OnDecrementOffset()
    {
        offsetSlider.value -= 1f;
    }

    /// <summary>오프셋을 1ms 증가. incrementButton.onClick에서 호출.</summary>
    public void OnIncrementOffset()
    {
        offsetSlider.value += 1f;
    }

    /// <summary>Play Test를 시작. playTestButton.onClick에서 호출.</summary>
    public void OnPlayTest()
    {
        if (calibrationManager == null || CalibrationNoteSpawner.Instance == null) return;
        feedbackLabel.text = string.Empty;
        calibrationManager.StartPlayTest(CalibrationNoteSpawner.Instance.SpawnNotes());
    }

    /// <summary>Play Test 결과 피드백 문자열을 화면에 표시. CalibrationManager에서 호출.</summary>
    public void ShowCalibrationFeedback(string feedback)
    {
        feedbackLabel.text = feedback;
    }

    /// <summary>두 플레이어의 정신력 수치를 HUD에 반영. GameManager에서 호출.</summary>
    public void UpdateSanity(int p1, int p2) { }

    /// <summary>현재 BPM 수치를 HUD에 표시. GameManager에서 호출.</summary>
    public void UpdateBpm(float bpm) { }

    /// <summary>판정 결과(Perfect/Good/Miss)를 화면에 표시. DefenseTurn에서 호출.</summary>
    public void ShowJudgment(Judgment j) { }

    /// <summary>턴 전환 배너 텍스트를 화면에 표시. GameManager에서 호출.</summary>
    public void ShowTurnBanner(string label) { }

    private void UpdateOffsetLabel(float value)
    {
        offsetLabel.text = value >= 0f
            ? $"Offset: +{value:0}ms"
            : $"Offset: {value:0}ms";
    }
}
