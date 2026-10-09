using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 게임 전체 사운드 재생을 담당하는 매니저.
/// BGM, SFX 목록을 인스펙터에서 관리하고, BPM별 코어루프 BGM 예약 재생을 처리한다.
/// 설정창에서 BGM/SFX 볼륨을 즉시 조절할 수 있도록 public setter를 제공한다.
/// </summary>
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    public const string BgmVolumePrefsKey = "bgmMasterVolume";
    public const string SfxVolumePrefsKey = "sfxMasterVolume";

    [Serializable]
    public class BgmEntry
    {
        public BgmId id;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        public bool loop = true;
    }

    [Serializable]
    public class SfxEntry
    {
        public SfxId id;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        [Min(1)] public int maxSimultaneous = 4;
    }

    [Serializable]
    public class CoreLoopBgmMap
    {
        public float bpm;
        public BgmId bgmId;

        // TODO:
        // BPM 전환 인트로가 확정되면 introBgmId 또는 introClip을 추가해서
        // intro → loop 순서로 DSP 예약 재생하도록 확장.
    }

    [Header("BGM Sources")]
    [FormerlySerializedAs("audioSource")]
    [SerializeField] private AudioSource bgmSourceA;
    [SerializeField] private AudioSource bgmSourceB;

    [Header("SFX Pool")]
    [SerializeField] private int sfxPoolSize = 12;

    [Header("BGM List")]
    [SerializeField] private List<BgmEntry> bgmEntries = new();

    [Header("SFX List")]
    [SerializeField] private List<SfxEntry> sfxEntries = new();

    [Header("Core Loop BGM Mapping")]
    [SerializeField] private List<CoreLoopBgmMap> coreLoopBgmMaps = new();

    [Header("Schedule")]
    [SerializeField] private double bgmScheduleLeadTime = 0.1;

    [Header("Master Volume")]
    [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float bgmMasterVolume = 0.6f;
    [SerializeField, Range(0f, 1f)] private float sfxMasterVolume = 0.8f;

    public float BgmMasterVolume => bgmMasterVolume;
    public float SfxMasterVolume => sfxMasterVolume;

    private readonly Dictionary<BgmId, BgmEntry> bgmMap = new();
    private readonly Dictionary<SfxId, SfxEntry> sfxMap = new();
    private readonly List<AudioSource> sfxSources = new();

    // BGM Source마다 원래 BgmEntry.volume 값을 기억해두고,
    // 설정창 볼륨 변경 시 현재 재생 중인 BGM에도 새 master volume을 적용한다.
    private readonly Dictionary<AudioSource, float> bgmBaseVolumeBySource = new();

    private AudioSource currentBgmSource;
    private AudioSource standbyBgmSource;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadSavedVolumeSettings();

        SetupBgmSources();
        SetupSfxPool();
        BuildLookupTables();
        ApplyBgmMasterVolumeToSources();
    }

    private void LoadSavedVolumeSettings()
    {
        bgmMasterVolume = PlayerPrefs.GetFloat(BgmVolumePrefsKey, bgmMasterVolume);
        sfxMasterVolume = PlayerPrefs.GetFloat(SfxVolumePrefsKey, sfxMasterVolume);

        bgmMasterVolume = Mathf.Clamp01(bgmMasterVolume);
        sfxMasterVolume = Mathf.Clamp01(sfxMasterVolume);
    }

    private void SetupBgmSources()
    {
        if (bgmSourceA == null)
        {
            bgmSourceA = CreateAudioSource("BGM Source A");
        }

        if (bgmSourceB == null)
        {
            bgmSourceB = CreateAudioSource("BGM Source B");
        }

        bgmSourceA.playOnAwake = false;
        bgmSourceB.playOnAwake = false;

        currentBgmSource = bgmSourceA;
        standbyBgmSource = bgmSourceB;

        if (!bgmBaseVolumeBySource.ContainsKey(bgmSourceA))
        {
            bgmBaseVolumeBySource[bgmSourceA] = 1f;
        }

        if (!bgmBaseVolumeBySource.ContainsKey(bgmSourceB))
        {
            bgmBaseVolumeBySource[bgmSourceB] = 1f;
        }
    }

    private void SetupSfxPool()
    {
        sfxSources.Clear();

        for (int i = 0; i < sfxPoolSize; i++)
        {
            AudioSource source = CreateAudioSource($"SFX Source {i + 1}");
            source.playOnAwake = false;
            source.loop = false;
            sfxSources.Add(source);
        }
    }

    private AudioSource CreateAudioSource(string sourceName)
    {
        GameObject child = new GameObject(sourceName);
        child.transform.SetParent(transform);

        AudioSource source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;

        return source;
    }

    private void BuildLookupTables()
    {
        bgmMap.Clear();
        sfxMap.Clear();

        foreach (BgmEntry entry in bgmEntries)
        {
            if (entry == null || entry.clip == null)
            {
                continue;
            }

            bgmMap[entry.id] = entry;
        }

        foreach (SfxEntry entry in sfxEntries)
        {
            if (entry == null || entry.clip == null)
            {
                continue;
            }

            sfxMap[entry.id] = entry;
        }
    }

    /// <summary>
    /// 설정창에서 BGM 전체 볼륨을 변경할 때 호출한다.
    /// 현재 재생 중인 BGM에도 즉시 반영된다.
    /// </summary>
    public void SetBgmMasterVolume(float volume)
    {
        bgmMasterVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(BgmVolumePrefsKey, bgmMasterVolume);
        ApplyBgmMasterVolumeToSources();
    }

    /// <summary>
    /// 설정창에서 SFX 전체 볼륨을 변경할 때 호출한다.
    /// 이후 재생되는 효과음부터 반영된다.
    /// </summary>
    public void SetSfxMasterVolume(float volume)
    {
        sfxMasterVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(SfxVolumePrefsKey, sfxMasterVolume);
    }

    /// <summary>
    /// BGM/SFX 볼륨을 기본값으로 되돌린다.
    /// </summary>
    public void ResetVolumeSettings(float defaultBgmVolume = 0.6f, float defaultSfxVolume = 0.8f)
    {
        SetBgmMasterVolume(defaultBgmVolume);
        SetSfxMasterVolume(defaultSfxVolume);
    }

    public float GetScaledBgmVolume(float baseVolume)
    {
        return Mathf.Clamp01(baseVolume) * bgmMasterVolume * masterVolume;
    }

    public float GetScaledSfxVolume(float baseVolume)
    {
        return Mathf.Clamp01(baseVolume) * sfxMasterVolume * masterVolume;
    }

    private void ApplyBgmMasterVolumeToSources()
    {
        ApplyBgmMasterVolumeToSource(bgmSourceA);
        ApplyBgmMasterVolumeToSource(bgmSourceB);
    }

    private void ApplyBgmMasterVolumeToSource(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        float baseVolume = 1f;

        if (bgmBaseVolumeBySource.TryGetValue(source, out float savedBaseVolume))
        {
            baseVolume = savedBaseVolume;
        }

        source.volume = GetScaledBgmVolume(baseVolume);
    }

    /// <summary>
    /// 지정한 AudioClip을 0.1초 후 DSP 기준으로 예약 재생한다.
    /// </summary>
    public void PlayBGM(AudioClip clip)
    {
        if (clip == null)
        {
            return;
        }

        ScheduleRawBgm(clip, AudioSettings.dspTime + bgmScheduleLeadTime, 1f, true);
    }

    /// <summary>
    /// 현재 재생 중인 BGM을 즉시 정지한다.
    /// </summary>
    public void StopBGM()
    {
        StopBgm();
    }

    /// <summary>
    /// BgmId 기준으로 BGM을 즉시 재생한다.
    /// 메뉴, 튜토리얼 등 정확한 싱크가 덜 중요한 BGM에 사용한다.
    /// </summary>
    public void PlayBgm(BgmId id)
    {
        ScheduleBgm(id, AudioSettings.dspTime + bgmScheduleLeadTime);
    }

    /// <summary>
    /// BgmId 기준으로 지정한 DSP 시각에 BGM을 예약 재생한다.
    /// 코어루프 BGM처럼 양쪽 싱크가 중요한 경우 사용한다.
    /// </summary>
    public void ScheduleBgm(BgmId id, double dspTime, double stopAt = -1)
    {
        if (!bgmMap.TryGetValue(id, out BgmEntry entry))
        {
            Debug.LogWarning($"SoundManager: BGM을 찾을 수 없습니다. id:{id}");
            return;
        }

        ScheduleRawBgm(entry.clip, dspTime, entry.volume, entry.loop, stopAt);
    }

    private void ScheduleRawBgm(AudioClip clip, double dspTime, float volume, bool loop, double stopAt = -1)
    {
        if (clip == null)
        {
            return;
        }

        AudioSource nextSource = standbyBgmSource;
        AudioSource oldSource = currentBgmSource;

        nextSource.Stop();
        nextSource.clip = clip;
        nextSource.loop = loop;

        bgmBaseVolumeBySource[nextSource] = volume;
        nextSource.volume = GetScaledBgmVolume(volume);

        nextSource.PlayScheduled(dspTime);

        if (oldSource != null && oldSource.isPlaying)
        {
            oldSource.SetScheduledEndTime(stopAt >= 0 ? stopAt : dspTime);
        }

        currentBgmSource = nextSource;
        standbyBgmSource = oldSource;

        Debug.Log($"BGM Scheduled / clip:{clip.name}, dspTime:{dspTime:F3}");
    }

    /// <summary>
    /// 현재 BPM에 매핑된 코어루프 BGM을 지정한 DSP 시각에 예약 재생한다.
    /// </summary>
    public void ScheduleCoreLoopBgm(float bpm, double dspTime, double stopAt = -1)
    {
        if (!TryGetCoreLoopBgmId(bpm, out BgmId bgmId))
        {
            Debug.LogWarning($"SoundManager: BPM에 대응되는 BGM이 없습니다. bpm:{bpm}");
            return;
        }

        // TODO:
        // BPM 전환 인트로가 확정되면 여기서
        // intro 예약 → intro 끝나는 시각에 loop 예약 구조로 확장.
        ScheduleBgm(bgmId, dspTime, stopAt);
    }

    /// <summary>
    /// 현재 시간 기준으로 BPM별 코어루프 BGM을 재생한다.
    /// </summary>
    public void PlayCoreLoopBgm(float bpm)
    {
        ScheduleCoreLoopBgm(bpm, AudioSettings.dspTime + bgmScheduleLeadTime);
    }

    private bool TryGetCoreLoopBgmId(float bpm, out BgmId bgmId)
    {
        int roundedBpm = Mathf.RoundToInt(bpm);

        foreach (CoreLoopBgmMap map in coreLoopBgmMaps)
        {
            if (Mathf.RoundToInt(map.bpm) == roundedBpm)
            {
                bgmId = map.bgmId;
                return true;
            }
        }

        bgmId = default;
        return false;
    }

    /// <summary>
    /// 효과음을 즉시 재생한다.
    /// 버튼 클릭, 승패 효과음, 입력 타격음 등에 사용한다.
    /// </summary>
    public void PlaySfx(SfxId id)
    {
        PlaySfxScheduled(id, AudioSettings.dspTime);
    }

    /// <summary>
    /// 효과음을 지정한 DSP 시각에 예약 재생한다.
    /// 나중에 상대 입력 타격음 보정이나 페이크 시뮬레이션에서 사용할 수 있다.
    /// </summary>
    public void PlaySfxScheduled(SfxId id, double dspTime)
    {
        if (!sfxMap.TryGetValue(id, out SfxEntry entry))
        {
            Debug.LogWarning($"SoundManager: SFX를 찾을 수 없습니다. id:{id}");
            return;
        }

        if (entry.clip == null)
        {
            return;
        }

        if (CountPlayingSameClip(entry.clip) >= entry.maxSimultaneous)
        {
            StopOneSameClip(entry.clip);
        }

        AudioSource source = GetAvailableSfxSource();

        source.clip = entry.clip;
        source.volume = GetScaledSfxVolume(entry.volume);
        source.loop = false;
        source.PlayScheduled(dspTime);
    }

    private AudioSource GetAvailableSfxSource()
    {
        foreach (AudioSource source in sfxSources)
        {
            if (!source.isPlaying)
            {
                return source;
            }
        }

        return sfxSources[0];
    }

    private int CountPlayingSameClip(AudioClip clip)
    {
        int count = 0;

        foreach (AudioSource source in sfxSources)
        {
            if (source.isPlaying && source.clip == clip)
            {
                count++;
            }
        }

        return count;
    }

    private void StopOneSameClip(AudioClip clip)
    {
        foreach (AudioSource source in sfxSources)
        {
            if (source.isPlaying && source.clip == clip)
            {
                source.Stop();
                return;
            }
        }
    }

    public void StopBgm()
    {
        bgmSourceA?.Stop();
        bgmSourceB?.Stop();
    }

    public void StopAllSfx()
    {
        foreach (AudioSource source in sfxSources)
        {
            source.Stop();
        }
    }
}