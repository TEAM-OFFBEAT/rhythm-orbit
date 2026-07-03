using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class NoteRenderer : SceneSingleton<NoteRenderer>
{
    [SerializeField] private RectTransform noteContainer;
    [SerializeField] private RectTransform judgeLine;
    [SerializeField] private float spawnX = 700f;
    [SerializeField] private float judgeLineX = -600f;
    [SerializeField] private float noteSize = 60f;
    [SerializeField] private Color noteColor = Color.cyan;
    [SerializeField] private double leadTime = 2.0;

    private struct NoteEntry { public RectTransform rect; public double judgeTime; public int noteId; }
    private readonly List<NoteEntry> activeNotes = new();
    public double LeadTime => leadTime;
    private bool isMoving;

    protected override void Awake()
    {
        base.Awake();
        if (noteContainer == null) noteContainer = GetComponent<RectTransform>();
        if (judgeLine != null)
        {
            var pos = judgeLine.anchoredPosition;
            pos.x = judgeLineX;
            judgeLine.anchoredPosition = pos;
            judgeLine.gameObject.SetActive(false);
        }
    }

    // TODO: DefenseTurn 구현 시 Instantiate/Destroy 대신 오브젝트 풀링으로 교체.
    //       노트 타입별 프리팹, 레인 수 등 실제 게임 요소가 확정된 후 설계할 것.
    /// <summary>NoteData를 받아 오른쪽 spawnX 위치에 UI 노트 Image를 생성. CalibrationNoteSpawner에서 호출.</summary>
    public void SpawnNote(NoteData note)
    {
        var go = new GameObject($"Note_{note.noteId}", typeof(Image));
        go.GetComponent<Image>().color = noteColor;
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(noteContainer, false);
        rect.sizeDelta = new Vector2(noteSize, noteSize);
        rect.anchoredPosition = new Vector2(spawnX, 0f);
        activeNotes.Add(new NoteEntry { rect = rect, judgeTime = note.judgeTime, noteId = note.noteId });
    }

    /// <summary>노트 이동을 활성화하고 판정선을 표시. CalibrationNoteSpawner에서 호출.</summary>
    public void StartMoving()
    {
        isMoving = true;
        if (judgeLine != null) judgeLine.gameObject.SetActive(true);
    }

    /// <summary>noteId에 해당하는 노트를 제거. CalibrationManager에서 호출.</summary>
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

    /// <summary>모든 활성 노트를 제거하고 판정선을 숨기며 상태를 초기화. CalibrationManager에서 호출.</summary>
    public void ClearAll()
    {
        isMoving = false;
        if (judgeLine != null) judgeLine.gameObject.SetActive(false);
        foreach (var entry in activeNotes)
            if (entry.rect != null) Destroy(entry.rect.gameObject);
        activeNotes.Clear();
    }

    private void Update()
    {
        if (!isMoving || activeNotes.Count == 0) return;
        double now = AudioSettings.dspTime;
        for (int i = activeNotes.Count - 1; i >= 0; i--)
        {
            var entry = activeNotes[i];
            if (entry.rect == null) continue;
            if (now >= entry.judgeTime)
            {
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
