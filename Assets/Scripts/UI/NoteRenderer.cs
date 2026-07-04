using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class NoteRenderer : SceneSingleton<NoteRenderer>
{
    [SerializeField] private RectTransform noteContainer;
    [SerializeField] private RectTransform notePrefab;
    [SerializeField] private RectTransform judgeLine;
    [SerializeField] private float spawnX = 700f;
    [SerializeField] private double leadTime = 2.0;

    private struct NoteEntry { public RectTransform rect; public double judgeTime; public int noteId; }
    private readonly List<NoteEntry> activeNotes = new();
    public double LeadTime => leadTime;
    private bool isMoving;

    /// <summary>
    /// NoteData를 받아 spawnX 위치에 노트 프리팹을 생성.
    /// </summary>
    public void SpawnNote(NoteData note)
    {
        // TODO: DefenseTurn 구현 시 Instantiate/Destroy 대신 오브젝트 풀링으로 교체.
        var rect = Instantiate(notePrefab, noteContainer);
        Vector2 judgeLocalPos = (Vector2)noteContainer.InverseTransformPoint(judgeLine.position);
        rect.anchoredPosition = new Vector2(spawnX, judgeLocalPos.y);
        activeNotes.Add(new NoteEntry { rect = rect, judgeTime = note.judgeTime, noteId = note.noteId });
    }

    /// <summary>
    /// 노트 이동을 활성화.
    /// </summary>
    public void StartMoving()
    {
        isMoving = true;
    }

    /// <summary>
    /// noteId에 해당하는 노트를 제거.
    /// </summary>
    public void RemoveNote(int noteId)
    {
        for (int i = activeNotes.Count - 1; i >= 0; i--)
        {
            if (activeNotes[i].noteId != noteId) continue;
            // TODO: DefenseTurn 구현 시 Instantiate/Destroy 대신 오브젝트 풀링으로 교체.
            if (activeNotes[i].rect != null) Destroy(activeNotes[i].rect.gameObject);
            activeNotes.RemoveAt(i);
            return;
        }
    }

    /// <summary>
    /// 모든 활성 노트를 제거하고 상태를 초기화.
    /// </summary>
    public void ClearAll()
    {
        isMoving = false;
        foreach (var entry in activeNotes)
        {
            // TODO: DefenseTurn 구현 시 Instantiate/Destroy 대신 오브젝트 풀링으로 교체.
            if (entry.rect != null) Destroy(entry.rect.gameObject);
        }
        activeNotes.Clear();
    }

    private void Update()
    {
        if (!isMoving || activeNotes.Count == 0) return;
        double now = AudioSettings.dspTime;
        float judgeLineX = judgeLine.anchoredPosition.x;
        for (int i = activeNotes.Count - 1; i >= 0; i--)
        {
            var entry = activeNotes[i];
            if (entry.rect == null) continue;
            if (now >= entry.judgeTime)
            {
                // TODO: DefenseTurn 구현 시 Instantiate/Destroy 대신 오브젝트 풀링으로 교체.
                Destroy(entry.rect.gameObject);
                activeNotes.RemoveAt(i);
                continue;
            }
            double remainingTime = System.Math.Max(0.0, entry.judgeTime - now);
            float t = Mathf.Clamp01((float)(remainingTime / leadTime));

            var pos = entry.rect.anchoredPosition;
            pos.x = Mathf.Lerp(judgeLineX, spawnX, t);
            entry.rect.anchoredPosition = pos;
        }
    }
}
