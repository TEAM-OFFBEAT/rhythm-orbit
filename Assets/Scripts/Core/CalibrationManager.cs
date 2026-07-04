using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class CalibrationManager : SceneSingleton<CalibrationManager>
{
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip metronomeClip;

    private List<NoteData> notes;
    private HashSet<int> resolvedIds;
    private bool isRunning;

    /// <summary>
    /// 노트 목록을 받아 Play Test를 시작.
    /// </summary>
    public void StartPlayTest(List<NoteData> spawnedNotes)
    {
        if (spawnedNotes == null || spawnedNotes.Count == 0) return;
        StopAllCoroutines();
        notes = spawnedNotes;
        resolvedIds = new HashSet<int>();
        isRunning = true;
        StartCoroutine(PlayMetronome(notes));
    }

    /// <summary>
    /// offsetMs를 기준으로 피드백 문자열을 반환.
    /// </summary>
    public static string GetFeedback(double offsetMs)
    {
        if (offsetMs > 30.0) return "Too Late";
        if (offsetMs > 10.0) return "A bit Late";
        if (offsetMs >= -10.0) return "Perfect!";
        if (offsetMs >= -30.0) return "A bit Early";
        return "Too Early";
    }

    /// <summary>
    /// Tap 입력 시 가장 가까운 노트의 타이밍 오차를 계산해 피드백을 표시.
    /// </summary>
    public void OnTap()
    {
        // TODO: OnTap을 CalibrationManager에서 처리하지 않도록 변경하기
        if (!isRunning) return;
        var note = FindClosestUnresolved(AudioSettings.dspTime);
        if (note == null) return;
        double rawOffset = (AudioSettings.dspTime - note.judgeTime) * 1000.0;
        double offsetMs = JudgeSystem.Instance.CalcOffsetMs(AudioSettings.dspTime, note.judgeTime);
        resolvedIds.Add(note.noteId);
        HUD.Instance.UpdateCalibrationFeedback(GetFeedback(offsetMs));
        NoteRenderer.Instance.RemoveNote(note.noteId);
    }

    private void Update()
    {
        if (!isRunning) return;
        if (notes != null && notes.Count > 0 &&
            AudioSettings.dspTime > notes[notes.Count - 1].judgeTime + 1.0)
            CompleteCalibration();
    }

    private NoteData FindClosestUnresolved(double tapTime)
    {
        NoteData closest = null;
        double minDiff = double.MaxValue;
        foreach (var note in notes)
        {
            if (resolvedIds.Contains(note.noteId)) continue;
            double diff = Math.Abs(tapTime - note.judgeTime);
            if (diff < minDiff) { minDiff = diff; closest = note; }
        }
        return closest;
    }

    private void CompleteCalibration()
    {
        isRunning = false;
        NoteRenderer.Instance.ClearAll();
    }

    private IEnumerator PlayMetronome(List<NoteData> noteList)
    {
        if (audioSource == null || metronomeClip == null) yield break;
        audioSource.clip = metronomeClip;

        foreach (var note in noteList)
        {
            double audioOffsetSec = JudgeSystem.Instance.AudioOffsetMs / 1000.0;
            double scheduledTime = note.judgeTime + audioOffsetSec; // 메트로놈 재생 시각 = 노트 판정 시각 + 오디오 오프셋
            
            // DSP 정확도 보장: scheduledTime 100ms 전까지 매 프레임 대기 후 PlayScheduled로 예약
            while (AudioSettings.dspTime < scheduledTime - 0.1)
                yield return null;

            if (scheduledTime <= AudioSettings.dspTime)
            {
                audioSource.Play();
            }
            else
            {
                audioSource.PlayScheduled(scheduledTime);
            }
        }
    }
    
}
