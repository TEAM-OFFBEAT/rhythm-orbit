using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class CalibrationManager : SceneSingleton<CalibrationManager>
{
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip metronomeClip;
    [SerializeField] private CalibrationNoteSpawner spawner;

    [Header("Metronome Volume")]
    [SerializeField, Range(0f, 1f)]
    private float normalBeatVolume = 0.35f;

    [SerializeField, Range(0f, 1f)]
    private float lastBeatVolume = 1f;

    // 설정창 테스트 전용 설정. 메인루프의 BPM에는 영향을 주지 않는다.
    private const double TestBpm = 120.0;
    private const int BeatsPerCycle = 4;

    private readonly List<NoteData> notes = new();

    private Action<string> onFeedback;
    private bool isRunning;
    private double beatDuration;
    private double nextJudgeTime;
    private int nextNoteId;
    private float currentBeatVolume;

    // 생성 간격과 별개로, 반박 길이를 판정 기준으로 사용한다.
    private double NoteDurationSeconds => beatDuration / 2.0;

    protected override void Awake()
    {
        base.Awake();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (spawner == null)
            spawner = FindAnyObjectByType<CalibrationNoteSpawner>();
    }

    public void StartPlayTest(Action<string> onFeedback)
    {
        StopPlayTest();

        if (!isActiveAndEnabled || JudgeSystem.Instance == null ||
            spawner == null || !spawner.IsReady)
        {
            Debug.LogError(
                "CalibrationManager의 활성 상태, JudgeSystem과 스포너 UI 연결을 확인하세요.",
                this);
            return;
        }

        beatDuration = 60.0 / TestBpm;

        double now = AudioSettings.dspTime;

        // 첫 노트 도착 전에 1~3박도 재생할 수 있도록 준비 시간을 확보한다.
        nextJudgeTime = now + Math.Max(
            spawner.LeadTime,
            beatDuration * BeatsPerCycle);

        double firstJudgeTime = nextJudgeTime;

        nextNoteId = 0;
        currentBeatVolume = normalBeatVolume;
        this.onFeedback = onFeedback;
        isRunning = true;

        if (audioSource != null)
            audioSource.loop = false;

        this.onFeedback?.Invoke(string.Empty);

        // 노트 생성과 별도로 메트로놈을 한 번만 시작한다.
        StartCoroutine(PlayMetronome(firstJudgeTime));
        SpawnNotesIfNeeded(now);
    }

    public void StopPlayTest()
    {
        isRunning = false;
        StopAllCoroutines();

        if (audioSource != null)
            audioSource.Stop();

        if (spawner != null)
            spawner.ClearAll();

        notes.Clear();
        onFeedback = null;
    }

    private void OnDisable()
    {
        StopPlayTest();
    }

    private void Update()
    {
        ApplyMetronomeVolume();

        if (!isRunning) return;

        if (JudgeSystem.Instance == null ||
            spawner == null || !spawner.IsReady)
        {
            StopPlayTest();
            return;
        }

        double now = AudioSettings.dspTime;

        RemoveExpiredNotes(now);
        SpawnNotesIfNeeded(now);
    }

    private void SpawnNotesIfNeeded(double now)
    {
        double interval = beatDuration * BeatsPerCycle;

        while (nextJudgeTime <= now + spawner.LeadTime)
        {
            double judgeTime = nextJudgeTime;
            int noteId = nextNoteId++;

            nextJudgeTime += interval;

            // 프레임 지연으로 이미 판정 기한이 지난 노트는 생성하지 않는다.
            if (JudgeSystem.Instance.HasPassedHitWindow(
                now, judgeTime, NoteDurationSeconds))
            {
                continue;
            }

            var note = new NoteData
            {
                noteId = noteId,
                noteType = NoteType.HIGH,
                noteRelativeTime = noteId * interval,
                judgeTime = judgeTime
            };

            if (!spawner.SpawnNote(note))
            {
                StopPlayTest();
                return;
            }

            notes.Add(note);
        }
    }

    private void RemoveExpiredNotes(double now)
    {
        for (int i = notes.Count - 1; i >= 0; i--)
        {
            if (!JudgeSystem.Instance.HasPassedHitWindow(
                now, notes[i].judgeTime, NoteDurationSeconds))
            {
                continue;
            }

            // 자동 만료는 판정 데이터만 정리한다.
            // 화면의 미입력 노트는 스포너가 레인 끝에서 제거한다.
            notes.RemoveAt(i);
        }
    }

    public void OnTap()
    {
        if (!isRunning || JudgeSystem.Instance == null ||
            spawner == null || !spawner.IsReady)
        {
            return;
        }

        double now = AudioSettings.dspTime;
        RemoveExpiredNotes(now);

        NoteData note = FindClosestUnresolved(now);

        if (note == null) return;

        double offsetMs = JudgeSystem.Instance.CalcOffsetMs(
            now, note.judgeTime);

        // 타격한 노트는 기존처럼 즉시 제거한다.
        notes.Remove(note);
        spawner.RemoveNote(note.noteId);

        onFeedback?.Invoke(GetFeedback(offsetMs));
    }

    private NoteData FindClosestUnresolved(double tapTime)
    {
        NoteData closest = null;
        double minDiff = double.MaxValue;

        foreach (NoteData note in notes)
        {
            if (!JudgeSystem.Instance.IsNoteActive(
                tapTime, note.judgeTime))
            {
                continue;
            }

            double diff = Math.Abs(
                JudgeSystem.Instance.CalcOffsetMs(
                    tapTime, note.judgeTime));

            if (diff < minDiff)
            {
                minDiff = diff;
                closest = note;
            }
        }

        return closest;
    }

    public static string GetFeedback(double offsetMs)
    {
        if (offsetMs > 30.0) return "Too Late";
        if (offsetMs > 10.0) return "A bit Late";
        if (offsetMs >= -10.0) return "Perfect!";
        if (offsetMs >= -30.0) return "A bit Early";
        return "Too Early";
    }

    private IEnumerator PlayMetronome(double firstJudgeTime)
    {
        if (audioSource == null || metronomeClip == null)
            yield break;

        // 첫 노트의 도착 시각이 4박째가 되도록 1박째를 역산한다.
        double nextBeatTime =
            firstJudgeTime - beatDuration * (BeatsPerCycle - 1);

        int beatIndex = 0;
        const double scheduleAheadSeconds = 0.1;
        const double lateToleranceSeconds = 0.05;

        audioSource.clip = metronomeClip;

        while (isRunning &&
               audioSource != null &&
               JudgeSystem.Instance != null)
        {
            // 예약 전까지 슬라이더의 현재 오디오 오프셋을 반영한다.
            double scheduledTime = nextBeatTime +
                JudgeSystem.Instance.AudioOffsetMs / 1000.0;

            double now = AudioSettings.dspTime;

            if (now < scheduledTime - scheduleAheadSeconds)
            {
                yield return null;
                continue;
            }

            // 지나간 박자를 몰아서 재생하지 않는다.
            if (now > scheduledTime + lateToleranceSeconds)
            {
                nextBeatTime += beatDuration;
                beatIndex = (beatIndex + 1) % BeatsPerCycle;
                continue;
            }

            currentBeatVolume = beatIndex == BeatsPerCycle - 1
                ? lastBeatVolume
                : normalBeatVolume;

            ApplyMetronomeVolume();

            if (scheduledTime <= now)
                audioSource.Play();
            else
                audioSource.PlayScheduled(scheduledTime);

            // 현재 프레임 시각 대신 원래 박자에 더해 누적 오차를 막는다.
            nextBeatTime += beatDuration;
            beatIndex = (beatIndex + 1) % BeatsPerCycle;

            yield return null;
        }
    }

    private void ApplyMetronomeVolume()
    {
        if (audioSource == null) return;

        // 박자별 강약에 설정창의 SFX 볼륨을 함께 반영한다.
        audioSource.volume = SoundManager.Instance != null
            ? SoundManager.Instance.GetScaledSfxVolume(currentBeatVolume)
            : currentBeatVolume;
    }
}