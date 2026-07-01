using System.Collections.Generic;
using UnityEngine;

public class CalibrationNoteSpawner : SceneSingleton<CalibrationNoteSpawner>
{
    private const double BeatDuration = 0.5; // 120 BPM
    private const int NoteCount = 4;
    private const double LeadTime = 2.0;     // 첫 노트까지의 준비 시간(초)

    /// <summary>120BPM 기준 0.5초 간격의 NoteData 4개를 생성하고 NoteRenderer에 전달해 반환. HUD에서 호출.</summary>
    public List<NoteData> SpawnNotes()
    {
        double firstJudgeTime = AudioSettings.dspTime + LeadTime;
        var result = new List<NoteData>();

        for (int i = 0; i < NoteCount; i++)
        {
            var note = new NoteData
            {
                noteId = i,
                noteRelativeTime = i * BeatDuration,
                snapType = SnapType.BEAT,
                judgeTime = firstJudgeTime + i * BeatDuration
            };
            result.Add(note);
            NoteRenderer.Instance.SpawnNote(note);
        }

        NoteRenderer.Instance.StartMoving();
        return result;
    }
}
