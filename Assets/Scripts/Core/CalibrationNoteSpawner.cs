using System.Collections.Generic;
using UnityEngine;

public class CalibrationNoteSpawner : MonoBehaviour
{
    private double BeatDuration;
    private const int NoteCount = 4;

    /// <summary>
    /// 현재 BPM 기준 1박 간격의 NoteData 4개를 생성하고 NoteRenderer에 전달해 반환.
    /// </summary>
    public List<NoteData> SpawnNotes()
    {   
        BeatDuration = RhythmClock.Instance.GetNoteDuration() * 2.0;
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
