using System.Collections.Generic;
using UnityEngine;

public class CalibrationNoteSpawner : MonoBehaviour
{
    [SerializeField] private RectTransform noteContainer;
    [SerializeField] private RectTransform notePrefab;
    [SerializeField] private RectTransform judgeLine;
    [SerializeField] private float spawnX = 700f;
    [SerializeField] private double leadTime = 2.0;
    [SerializeField] private RectTransform laneEnd;

    private struct NoteEntry
    {
        public RectTransform rect;
        public int noteId;
        public double judgeTime;

        // 입력 판정이나 판정 기한 만료로 제거 요청을 받았는지 기록한다.
        public bool removeRequested;
    }

    private readonly List<NoteEntry> activeNotes = new();

    public double LeadTime => System.Math.Max(0.1, leadTime);

    public bool IsReady => isActiveAndEnabled &&
        noteContainer != null &&
        notePrefab != null &&
        judgeLine != null &&
        laneEnd != null &&
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
            noteContainer == null ||
            judgeLine == null ||
            laneEnd == null)
        {
            return;
        }

        double now = AudioSettings.dspTime;

        Vector3 judgeLocalPos =
            noteContainer.InverseTransformPoint(judgeLine.position);

        Vector3 endLocalPos =
            noteContainer.InverseTransformPoint(laneEnd.position);

        float dir = Mathf.Sign(judgeLocalPos.x - spawnX);

        for (int i = activeNotes.Count - 1; i >= 0; i--)
        {
            NoteEntry entry = activeNotes[i];

            if (entry.rect == null)
            {
                activeNotes.RemoveAt(i);
                continue;
            }

            // 도착 전 이동 속도를 유지하면서 판정선 이후로도 이동한다.
            double remainingTime = entry.judgeTime - now;
            float ratio = Mathf.Min(
                1f,
                (float)(remainingTime / LeadTime));

            float x = Mathf.LerpUnclamped(
                judgeLocalPos.x,
                spawnX,
                ratio);

            bool reachedEnd = dir > 0f
                ? x >= endLocalPos.x
                : x <= endLocalPos.x;

            // 타격하지 않아 남아 있는 노트는 레인 끝에서 제거한다.
            if (reachedEnd)
            {
                Destroy(entry.rect.gameObject);
                activeNotes.RemoveAt(i);
            }

            entry.rect.localPosition = new Vector3(
                x, judgeLocalPos.y, judgeLocalPos.z);

            // 판정 완료와 레인 끝 도착이 모두 충족되면 제거한다.
            if (reachedEnd && entry.removeRequested)
            {
                Destroy(entry.rect.gameObject);
                activeNotes.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// 타격한 미리보기 노트를 즉시 제거한다.
    /// </summary>
    public void RemoveNote(int noteId)
    {
        for (int i = activeNotes.Count - 1; i >= 0; i--)
        {
            if (activeNotes[i].noteId != noteId)
            {
                continue;
            }

            if (activeNotes[i].rect != null)
            {
                Destroy(activeNotes[i].rect.gameObject);
            }

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
