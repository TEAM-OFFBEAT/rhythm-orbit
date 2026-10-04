using System.Collections.Generic;
using UnityEngine;

public class CalibrationNoteSpawner : MonoBehaviour
{
    [SerializeField] private RectTransform noteContainer;
    [SerializeField] private RectTransform notePrefab;
    [SerializeField] private RectTransform judgeLine;
    [SerializeField] private float spawnX = 700f;
    [SerializeField] private double leadTime = 2.0;

    private struct NoteEntry
    {
        public RectTransform rect;
        public int noteId;
        public double judgeTime;
    }

    private readonly List<NoteEntry> activeNotes = new();

    public double LeadTime => System.Math.Max(0.1, leadTime);

    public bool IsReady => isActiveAndEnabled &&
        noteContainer != null && notePrefab != null && judgeLine != null &&
        noteContainer.gameObject.activeInHierarchy;

    public bool SpawnNote(NoteData note)
    {
        if (!IsReady)
        {
            Debug.LogError(
                "CalibrationNoteSpawner의 UI 연결과 활성 상태를 확인하세요.", this);
            return false;
        }

        Vector3 judgeLocalPos =
            noteContainer.InverseTransformPoint(judgeLine.position);

        RectTransform rect = Instantiate(notePrefab, noteContainer);

        rect.localPosition = new Vector3(
            spawnX, judgeLocalPos.y, judgeLocalPos.z);

        rect.gameObject.SetActive(true);

        activeNotes.Add(new NoteEntry
        {
            rect = rect,
            noteId = note.noteId,
            judgeTime = note.judgeTime
        });

        return true;
    }

    private void Update()
    {
        if (activeNotes.Count == 0 ||
            noteContainer == null || judgeLine == null)
            return;

        double now = AudioSettings.dspTime;

        Vector3 judgeLocalPos =
            noteContainer.InverseTransformPoint(judgeLine.position);

        for (int i = activeNotes.Count - 1; i >= 0; i--)
        {
            NoteEntry entry = activeNotes[i];

            if (entry.rect == null)
            {
                activeNotes.RemoveAt(i);
                continue;
            }

            // 도착 후에는 ratio가 0이 되어 판정선에 머문다.
            // 판정 기한에 따른 제거는 CalibrationManager가 담당한다.
            double remainingTime = entry.judgeTime - now;
            float ratio = Mathf.Clamp01((float)(remainingTime / LeadTime));

            float x = Mathf.Lerp(judgeLocalPos.x, spawnX, ratio);

            entry.rect.localPosition = new Vector3(
                x, judgeLocalPos.y, judgeLocalPos.z);
        }
    }

    public void RemoveNote(int noteId)
    {
        for (int i = activeNotes.Count - 1; i >= 0; i--)
        {
            if (activeNotes[i].noteId != noteId) continue;

            if (activeNotes[i].rect != null)
                Destroy(activeNotes[i].rect.gameObject);

            activeNotes.RemoveAt(i);
            return;
        }
    }

    public void ClearAll()
    {
        foreach (NoteEntry entry in activeNotes)
        {
            if (entry.rect != null)
                Destroy(entry.rect.gameObject);
        }

        activeNotes.Clear();
    }

    private void OnDisable()
    {
        ClearAll();
    }
}
