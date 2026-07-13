using System.Collections.Generic;
using UnityEngine;

public class CalibrationNoteSpawner : MonoBehaviour
{
    [SerializeField] private RectTransform noteContainer;
    [SerializeField] private RectTransform notePrefab;
    [SerializeField] private RectTransform judgeLine;
    [SerializeField] private float spawnX = 700f;
    [SerializeField] private double leadTime = 2.0;

    private const int NoteCount = 4;
    private double beatDuration;

    private struct NoteEntry { public RectTransform rect; public int noteId; public double judgeTime; }
    private readonly List<NoteEntry> activeNotes = new();
    private bool isMoving;

    public double LeadTime => leadTime;

    private void Update()
    {
        if (!isMoving || activeNotes.Count == 0) return;

        double now = AudioSettings.dspTime;
        float judgeLineX = judgeLine != null ? judgeLine.anchoredPosition.x : 0f;

        for (int i = activeNotes.Count - 1; i >= 0; i--)
        {
            NoteEntry entry = activeNotes[i];
            if (entry.rect == null) { activeNotes.RemoveAt(i); continue; }

            if (now >= entry.judgeTime)
            {
                Destroy(entry.rect.gameObject);
                activeNotes.RemoveAt(i);
                continue;
            }

            double remainingTime = System.Math.Max(0.0, entry.judgeTime - now);
            float ratio = Mathf.Clamp01((float)(remainingTime / leadTime));
            Vector2 pos = entry.rect.anchoredPosition;
            pos.x = Mathf.Lerp(judgeLineX, spawnX, ratio);
            entry.rect.anchoredPosition = pos;
        }
    }

    /// <summary>
    /// 현재 BPM 기준 1박 간격의 NoteData 4개를 생성하고 이동을 시작.
    /// </summary>
    public List<NoteData> SpawnNotes()
    {
        beatDuration = RhythmClock.Instance.GetNoteDuration() * 2.0;
        ClearAll();
        double firstJudgeTime = AudioSettings.dspTime + leadTime;
        var result = new List<NoteData>();

        for (int i = 0; i < NoteCount; i++)
        {
            var note = new NoteData
            {
                noteId = i,
                noteRelativeTime = i * beatDuration,
                judgeTime = firstJudgeTime + i * beatDuration
            };
            result.Add(note);

            if (notePrefab == null || noteContainer == null) continue;
            RectTransform rect = Instantiate(notePrefab, noteContainer);
            Vector2 judgeLocalPos = (Vector2)noteContainer.InverseTransformPoint(judgeLine.position);
            rect.anchoredPosition = new Vector2(spawnX, judgeLocalPos.y);
            activeNotes.Add(new NoteEntry { rect = rect, noteId = note.noteId, judgeTime = note.judgeTime });
        }

        isMoving = true;
        return result;
    }

    /// <summary>
    /// noteId에 해당하는 노트를 제거.
    /// </summary>
    public void RemoveNote(int noteId)
    {
        for (int i = activeNotes.Count - 1; i >= 0; i--)
        {
            if (activeNotes[i].noteId != noteId) continue;
            if (activeNotes[i].rect != null) Destroy(activeNotes[i].rect.gameObject);
            activeNotes.RemoveAt(i);
            return;
        }
    }

    /// <summary>
    /// 모든 활성 노트를 제거하고 이동을 중단.
    /// </summary>
    public void ClearAll()
    {
        isMoving = false;
        foreach (NoteEntry entry in activeNotes)
        {
            if (entry.rect != null) Destroy(entry.rect.gameObject);
        }
        activeNotes.Clear();
    }
}
