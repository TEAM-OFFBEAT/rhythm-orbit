using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 메인씬 설정창 전체를 제어한다.
/// Tab 키 또는 X 버튼으로 창을 열고 닫고,
/// 설정/오프셋 탭 전환, 볼륨/오프셋 슬라이더, 설정 초기화를 담당한다.
/// 
/// 이 스크립트는 SettingsWindowPrefab 루트에 붙이고,
/// 실제로 보였다 사라지는 창 오브젝트는 windowRoot에 연결한다.
/// </summary>
public class SettingsWindowController : MonoBehaviour
{
    private enum SettingsPageType
    {
        Settings,
        Offset
    }

    [Header("Window")]
    [SerializeField] private GameObject windowRoot;
    [SerializeField] private GameObject blurBackground;
    [SerializeField] private Button closeButton;

    [Header("Tabs")]
    [SerializeField] private Button settingsTabButton;
    [SerializeField] private Button offsetTabButton;
    [SerializeField] private GameObject settingsTabSelectedRoot;
    [SerializeField] private GameObject offsetTabSelectedRoot;

    [Header("Pages")]
    [SerializeField] private GameObject settingsPageRoot;
    [SerializeField] private GameObject offsetPageRoot;

    [Header("Volume Sliders")]
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private Slider bgmVolumeSlider;
    [SerializeField] private TMP_Text sfxVolumeValueLabel;
    [SerializeField] private TMP_Text bgmVolumeValueLabel;

    [Header("Offset Sliders")]
    [SerializeField] private Slider keyInputOffsetSlider;
    [SerializeField] private Slider audioOffsetSlider;
    [SerializeField] private TMP_Text keyInputOffsetValueLabel;
    [SerializeField] private TMP_Text audioOffsetValueLabel;
    
    [Header("Reset Buttons")]
    [SerializeField] private Button settingsResetButton;
    [SerializeField] private Button offsetResetButton;

    [Header("Defaults")]
    [SerializeField, Range(0f, 1f)] private float defaultSfxVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] private float defaultBgmVolume = 0.6f;
    [SerializeField] private float defaultKeyInputOffsetMs = 0f;
    [SerializeField] private float defaultAudioOffsetMs = 0f;

    [Header("Key Input Offset Range")]
    [SerializeField] private float minKeyInputOffsetMs = -100f;
    [SerializeField] private float maxKeyInputOffsetMs = 100f;

    [Header("Audio Offset Range")]
    [SerializeField] private float minOffsetMs = -200f;
    [SerializeField] private float maxOffsetMs = 200f;

    [Header("Calibration")]
    [SerializeField] private CalibrationManager calibrationManager;
    [SerializeField] private TMP_Text calibrationFeedbackLabel;

    public bool IsOpen { get; private set; }

    private SettingsPageType currentPage = SettingsPageType.Settings;

    private void Awake()
    {
        SetupSliderRanges();
        RegisterUiEvents();

        Close();
        ShowSettingsPage();

        RefreshAllUiWithoutNotify();
    }

    private void OnDestroy()
    {
        UnregisterUiEvents();
    }

    /// <summary>
    /// Tab 키 입력 또는 외부 버튼에서 호출한다.
    /// 설정창이 열려 있으면 닫고, 닫혀 있으면 연다.
    /// </summary>
    public void Toggle()
    {
        if (IsOpen)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    /// <summary>
    /// 설정창을 열고 현재 저장된 설정값을 UI에 반영한다.
    /// 오프셋 탭 상태라면 미리보기도 다시 시작한다.
    /// </summary>
    public void Open()
    {
        IsOpen = true;

        if (blurBackground != null)
        {
            blurBackground.SetActive(true);
        }

        if (windowRoot != null)
        {
            windowRoot.SetActive(true);
        }

        RefreshAllUiWithoutNotify();
        ApplyPageVisibility();
    }

    /// <summary>
    /// 설정창을 닫는다.
    /// 오프셋 미리보기도 함께 정지한다.
    /// </summary>
    public void Close()
    {
        IsOpen = false;

        StopCalibration();

        if (blurBackground != null)
        {
            blurBackground.SetActive(false);
        }

        if (windowRoot != null)
        {
            windowRoot.SetActive(false);
        }        
    }

    /// <summary>
    /// 설정 탭을 표시한다.
    /// 입력키 안내, 효과음/배경음 볼륨 조절 화면이다.
    /// </summary>
    public void ShowSettingsPage()
    {
        currentPage = SettingsPageType.Settings;
        ApplyPageVisibility();
    }

    /// <summary>
    /// 오프셋 탭을 표시한다.
    /// 키 입력 오프셋과 오디오 출력 오프셋을 조절하는 화면이다.
    /// </summary>
    public void ShowOffsetPage()
    {
        currentPage = SettingsPageType.Offset;
        ApplyPageVisibility();
    }

    /// <summary>
    /// 모든 설정을 기본값으로 되돌린다.
    /// 두 화면의 초기화 버튼이 공통으로 호출한다.
    /// </summary>
    public void ResetAllSettings()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.SetSfxMasterVolume(defaultSfxVolume);
            SoundManager.Instance.SetBgmMasterVolume(defaultBgmVolume);
        }
        else
        {
            PlayerPrefs.SetFloat(SoundManager.SfxVolumePrefsKey, defaultSfxVolume);
            PlayerPrefs.SetFloat(SoundManager.BgmVolumePrefsKey, defaultBgmVolume);
        }

        if (JudgeSystem.Instance != null)
        {
            JudgeSystem.Instance.SetKeyInputOffset(defaultKeyInputOffsetMs);
            JudgeSystem.Instance.SetAudioOffset(defaultAudioOffsetMs);
        }
        else
        {
            PlayerPrefs.SetFloat("keyInputOffsetMs", defaultKeyInputOffsetMs);
            PlayerPrefs.SetFloat("audioOffsetMs", defaultAudioOffsetMs);
        }

        PlayerPrefs.Save();
        RefreshAllUiWithoutNotify();
    }

    private void SetupSliderRanges()
    {
        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.minValue = 0f;
            sfxVolumeSlider.maxValue = 1f;
            sfxVolumeSlider.wholeNumbers = false;
        }

        if (bgmVolumeSlider != null)
        {
            bgmVolumeSlider.minValue = 0f;
            bgmVolumeSlider.maxValue = 1f;
            bgmVolumeSlider.wholeNumbers = false;
        }

        if (keyInputOffsetSlider != null)
        {
            keyInputOffsetSlider.minValue = minKeyInputOffsetMs;
            keyInputOffsetSlider.maxValue = maxKeyInputOffsetMs;
            keyInputOffsetSlider.wholeNumbers = true;
        }

        if (audioOffsetSlider != null)
        {
            audioOffsetSlider.minValue = minOffsetMs;
            audioOffsetSlider.maxValue = maxOffsetMs;
            audioOffsetSlider.wholeNumbers = true;
        }
    }

    private void RegisterUiEvents()
    {
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Close);
        }

        if (settingsTabButton != null)
        {
            settingsTabButton.onClick.AddListener(ShowSettingsPage);
        }

        if (offsetTabButton != null)
        {
            offsetTabButton.onClick.AddListener(ShowOffsetPage);
        }

        if (settingsResetButton != null)
        {
            settingsResetButton.onClick.AddListener(ResetAllSettings);
        }

        if (offsetResetButton != null)
        {
            offsetResetButton.onClick.AddListener(ResetAllSettings);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
        }

        if (bgmVolumeSlider != null)
        {
            bgmVolumeSlider.onValueChanged.AddListener(OnBgmVolumeChanged);
        }

        if (keyInputOffsetSlider != null)
        {
            keyInputOffsetSlider.onValueChanged.AddListener(OnKeyInputOffsetChanged);
        }

        if (audioOffsetSlider != null)
        {
            audioOffsetSlider.onValueChanged.AddListener(OnAudioOffsetChanged);
        }
    }

    private void UnregisterUiEvents()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
        }

        if (settingsTabButton != null)
        {
            settingsTabButton.onClick.RemoveListener(ShowSettingsPage);
        }

        if (offsetTabButton != null)
        {
            offsetTabButton.onClick.RemoveListener(ShowOffsetPage);
        }

        if (settingsResetButton != null)
        {
            settingsResetButton.onClick.RemoveListener(ResetAllSettings);
        }

        if (offsetResetButton != null)
        {
            offsetResetButton.onClick.RemoveListener(ResetAllSettings);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);
        }

        if (bgmVolumeSlider != null)
        {
            bgmVolumeSlider.onValueChanged.RemoveListener(OnBgmVolumeChanged);
        }

        if (keyInputOffsetSlider != null)
        {
            keyInputOffsetSlider.onValueChanged.RemoveListener(OnKeyInputOffsetChanged);
        }

        if (audioOffsetSlider != null)
        {
            audioOffsetSlider.onValueChanged.RemoveListener(OnAudioOffsetChanged);
        }
    }

    private void ApplyPageVisibility()
    {
        bool showSettings = currentPage == SettingsPageType.Settings;
        bool showOffset = currentPage == SettingsPageType.Offset;

        if (settingsPageRoot != null)
            settingsPageRoot.SetActive(showSettings);

        if (offsetPageRoot != null)
            offsetPageRoot.SetActive(showOffset);

        if (settingsTabSelectedRoot != null)
            settingsTabSelectedRoot.SetActive(showSettings);

        if (offsetTabSelectedRoot != null)
            offsetTabSelectedRoot.SetActive(showOffset);

        if (!IsOpen || !showOffset)
        {
            StopCalibration();
            return;
        }

        if (calibrationManager == null)
        {
            Debug.LogError(
                "[Settings] Calibration Manager 연결이 없거나 파괴되었습니다.",
                this);
            return;
        }

        calibrationManager.StartPlayTest(UpdateCalibrationFeedback);
    }

    private void RefreshAllUiWithoutNotify()
    {
        float sfxVolume = SoundManager.Instance != null
            ? SoundManager.Instance.SfxMasterVolume
            : PlayerPrefs.GetFloat(SoundManager.SfxVolumePrefsKey, defaultSfxVolume);

        float bgmVolume = SoundManager.Instance != null
            ? SoundManager.Instance.BgmMasterVolume
            : PlayerPrefs.GetFloat(SoundManager.BgmVolumePrefsKey, defaultBgmVolume);

        float keyOffset = JudgeSystem.Instance != null
            ? (float)JudgeSystem.Instance.KeyInputOffsetMs
            : PlayerPrefs.GetFloat("keyInputOffsetMs", defaultKeyInputOffsetMs);

        float audioOffset = JudgeSystem.Instance != null
            ? (float)JudgeSystem.Instance.AudioOffsetMs
            : PlayerPrefs.GetFloat("audioOffsetMs", defaultAudioOffsetMs);

        float limitedKeyOffset = Mathf.Clamp(
            keyOffset,
            minKeyInputOffsetMs,
            maxKeyInputOffsetMs
        );

        if (!Mathf.Approximately(keyOffset, limitedKeyOffset))
        {
            if (JudgeSystem.Instance != null)
            {
                JudgeSystem.Instance.SetKeyInputOffset(limitedKeyOffset);
            }
            else
            {
                PlayerPrefs.SetFloat(
                    JudgeSystem.KeyInputOffsetPrefsKey,
                    limitedKeyOffset
                );
            }

            PlayerPrefs.Save();
        }

        keyOffset = limitedKeyOffset;

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.SetValueWithoutNotify(sfxVolume);
        }

        if (bgmVolumeSlider != null)
        {
            bgmVolumeSlider.SetValueWithoutNotify(bgmVolume);
        }

        if (keyInputOffsetSlider != null)
        {
            keyInputOffsetSlider.SetValueWithoutNotify(keyOffset);
        }

        if (audioOffsetSlider != null)
        {
            audioOffsetSlider.SetValueWithoutNotify(audioOffset);
        }

        RefreshVolumeLabels(sfxVolume, bgmVolume);
        RefreshOffsetLabels(keyOffset, audioOffset);
    }

    private void OnSfxVolumeChanged(float value)
    {
        SoundManager.Instance?.SetSfxMasterVolume(value);
        RefreshVolumeLabels(value, GetCurrentBgmVolume());
    }

    private void OnBgmVolumeChanged(float value)
    {
        SoundManager.Instance?.SetBgmMasterVolume(value);
        RefreshVolumeLabels(GetCurrentSfxVolume(), value);
    }

    private void OnKeyInputOffsetChanged(float value)
    {
        float roundedValue = Mathf.Round(value);
        JudgeSystem.Instance?.SetKeyInputOffset(roundedValue);
        PlayerPrefs.Save();

        RefreshOffsetLabels(roundedValue, GetCurrentAudioOffset());
    }

    private void OnAudioOffsetChanged(float value)
    {
        float roundedValue = Mathf.Round(value);
        JudgeSystem.Instance?.SetAudioOffset(roundedValue);
        PlayerPrefs.Save();

        RefreshOffsetLabels(GetCurrentKeyInputOffset(), roundedValue);
    }

    private float GetCurrentSfxVolume()
    {
        return sfxVolumeSlider != null ? sfxVolumeSlider.value : defaultSfxVolume;
    }

    private float GetCurrentBgmVolume()
    {
        return bgmVolumeSlider != null ? bgmVolumeSlider.value : defaultBgmVolume;
    }

    private float GetCurrentKeyInputOffset()
    {
        return keyInputOffsetSlider != null ? keyInputOffsetSlider.value : defaultKeyInputOffsetMs;
    }

    private float GetCurrentAudioOffset()
    {
        return audioOffsetSlider != null ? audioOffsetSlider.value : defaultAudioOffsetMs;
    }

    private void RefreshVolumeLabels(float sfxVolume, float bgmVolume)
    {
        if (sfxVolumeValueLabel != null)
        {
            sfxVolumeValueLabel.text = $"{Mathf.RoundToInt(sfxVolume * 100f)}%";
        }

        if (bgmVolumeValueLabel != null)
        {
            bgmVolumeValueLabel.text = $"{Mathf.RoundToInt(bgmVolume * 100f)}%";
        }
    }

    private void RefreshOffsetLabels(float keyOffset, float audioOffset)
    {
        if (keyInputOffsetValueLabel != null)
        {
            keyInputOffsetValueLabel.text = $"{keyOffset:+0;-0;0} ms";
        }

        if (audioOffsetValueLabel != null)
        {
            audioOffsetValueLabel.text = $"{audioOffset:+0;-0;0} ms";
        }
    }

    /// <summary>
    /// 설정창이 열린 상태에서 HIGH 입력을 처리한다.
    /// 오프셋 탭에서는 F 노트 테스트 입력으로 소비한다.
    /// true를 반환하면 게임 입력으로 전달하지 않는다.
    /// </summary>
    public bool TryHandleHighInput()
    {
        if (!IsOpen)
        {
            return false;
        }

        if (currentPage != SettingsPageType.Offset)
        {
            return true;
        }

        if (calibrationManager != null)
        {
            calibrationManager.OnTap();
        }

        return true;
    }

    /// <summary>
    /// 설정창이 열린 상태에서 LOW 입력을 처리한다.
    /// 현재 오프셋 테스트는 F 노트만 사용하므로 J 입력은 소비만 하고 무시한다.
    /// </summary>
    public bool TryHandleLowInput()
    {
        return IsOpen;
    }

    private void UpdateCalibrationFeedback(string feedback)
    {
        if (calibrationFeedbackLabel == null) return;

        calibrationFeedbackLabel.text = string.IsNullOrEmpty(feedback)
            ? "F 키로 테스트"
            : feedback;
    }

    private void OnDisable()
    {
        IsOpen = false;

        if (blurBackground != null)
        {
            blurBackground.SetActive(false);
        }

        StopCalibration();
    }

    private void StopCalibration()
    {
        if (calibrationManager != null)
        {
            calibrationManager.StopPlayTest();
        }
    }
}