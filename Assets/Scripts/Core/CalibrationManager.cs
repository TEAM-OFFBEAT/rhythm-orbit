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
    [SerializeField, Min(1f)] private float fallbackBpm = 106f;
    [SerializeField, Min(1)] private int beatsPerNote = 4;

    private readonly List<NoteData> notes = new();
    private Action<string> onFeedback;
    private bool isRunning;
    private double beatDuration;
    private double nextJudgeTime;
    private int nextNoteId;

    // 생성 간격과 별개로, 메인루프처럼 반박 길이를 판정 기준으로 사용한다.
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

        beatDuration = GetBeatDuration();
        nextJudgeTime = AudioSettings.dspTime + spawner.LeadTime;
        nextNoteId = 0;
        this.onFeedback = onFeedback;
        isRunning = true;

        this.onFeedback?.Invoke(string.Empty);
        SpawnNotesIfNeeded(AudioSettings.dspTime);
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
        if (audioSource != null && SoundManager.Instance != null)
        {
            audioSource.volume = SoundManager.Instance.GetScaledSfxVolume(1f);
        }

        if (!isRunning) return;

        if (JudgeSystem.Instance == null || spawner == null || !spawner.IsReady)
        {
            StopPlayTest();
            return;
        }

        if (Math.Abs(beatDuration - GetBeatDuration()) > 0.000001)
        {
            Action<string> callback = onFeedback;
            StartPlayTest(callback);
            return;
        }

        double now = AudioSettings.dspTime;
        RemoveExpiredNotes(now);
        SpawnNotesIfNeeded(now);
    }

    private void SpawnNotesIfNeeded(double now)
    {
        double interval = beatDuration * Mathf.Max(1, beatsPerNote);

        while (nextJudgeTime <= now + spawner.LeadTime)
        {
            double judgeTime = nextJudgeTime;
            int noteId = nextNoteId++;
            nextJudgeTime += interval;

            // 프레임 지연으로 이미 판정 기한이 지난 노트는 생성하지 않는다.
            if (JudgeSystem.Instance.HasPassedHitWindow(
                now, judgeTime, NoteDurationSeconds))
                continue;

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
            StartCoroutine(PlayMetronome(judgeTime));
        }
    }

    private void RemoveExpiredNotes(double now)
    {
        for (int i = notes.Count - 1; i >= 0; i--)
        {
            if (!JudgeSystem.Instance.HasPassedHitWindow(
                now, notes[i].judgeTime, NoteDurationSeconds))
                continue;

            spawner.RemoveNote(notes[i].noteId);
            notes.RemoveAt(i);
        }
    }

    public void OnTap()
    {
        if (!isRunning || JudgeSystem.Instance == null ||
            spawner == null || !spawner.IsReady)
            return;

        double now = AudioSettings.dspTime;
        RemoveExpiredNotes(now);

        NoteData note = FindClosestUnresolved(now);
        if (note == null) return;

        double offsetMs = JudgeSystem.Instance.CalcOffsetMs(now, note.judgeTime);

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
            if (!JudgeSystem.Instance.IsNoteActive(tapTime, note.judgeTime))
                continue;

            double diff = Math.Abs(
                JudgeSystem.Instance.CalcOffsetMs(tapTime, note.judgeTime));

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

    private IEnumerator PlayMetronome(double judgeTime)
    {
        if (audioSource == null || metronomeClip == null)
            yield break;

        while (isRunning && JudgeSystem.Instance != null)
        {
            // 예약 전까지 슬라이더의 현재 오디오 오프셋을 반영한다.
            double scheduledTime = judgeTime +
                JudgeSystem.Instance.AudioOffsetMs / 1000.0;

            if (AudioSettings.dspTime < scheduledTime - 0.1)
            {
                yield return null;
                continue;
            }

            if (audioSource == null)
                yield break;

            audioSource.clip = metronomeClip;

            if (scheduledTime <= AudioSettings.dspTime)
                audioSource.Play();
            else
                audioSource.PlayScheduled(scheduledTime);

            yield break;
        }
    }

    private double GetBeatDuration()
    {
        return RhythmClock.Instance != null
            ? RhythmClock.Instance.GetBeatDuration()
            : 60.0 / Mathf.Max(1f, fallbackBpm);
    }
}
