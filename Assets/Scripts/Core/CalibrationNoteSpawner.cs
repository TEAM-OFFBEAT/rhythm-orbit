using System.Collections.Generic;
using UnityEngine;

public class CalibrationNoteSpawner : SceneSingleton<CalibrationNoteSpawner>
{
    private double BeatDuration; // RhythmeClock 참조
    private const int NoteCount = 4;

    /// <summary>120BPM 기준 0.5초 간격의 NoteData 4개를 생성하고 NoteRenderer에 전달해 반환. HUD에서 호출.</summary>
    public List<NoteData> SpawnNotes()
    {   
        BeatDuration = RhythmClock.Instance.GetNoteDuration()*2.0;
        NoteRenderer.Instance.ClearAll();
        double firstJudgeTime = AudioSettings.dspTime + NoteRenderer.Instance.LeadTime;
        var result = new List<NoteData>();

        for (int i = 0; i < NoteCount; i++)
        {
            var note = new NoteData
            {
                noteId = i,
                noteRelativeTime = i * BeatDuration,
                judgeTime = firstJudgeTime + i * BeatDuration
            };
            result.Add(note);
            NoteRenderer.Instance.SpawnNote(note);
        }

        NoteRenderer.Instance.StartMoving();
        return result;
    }
}
