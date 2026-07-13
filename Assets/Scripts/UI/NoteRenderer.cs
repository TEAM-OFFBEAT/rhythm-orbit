using System.Collections.Generic;
using UnityEngine;

public class NoteRenderer : SceneSingleton<NoteRenderer>
{
    [SerializeField] private Transform noteContainer;
    [SerializeField] private Transform notePrefab;

    private readonly Dictionary<int, Transform> activeNotes = new();

    /// <summary>
    /// noteId에 해당하는 노트 Transform을 생성해 반환. 오브젝트 풀링 적용 시 이 메서드만 교체.
    /// </summary>
    public Transform AcquireNote(int noteId)
    {
        if (notePrefab == null)
        {
            Debug.LogWarning("NoteRenderer: notePrefab이 연결되지 않았습니다.");
            return null;
        }
        Transform parent = noteContainer != null ? noteContainer : transform;
        Transform t = Instantiate(notePrefab, parent);
        t.name = $"Note_{noteId}";
        activeNotes[noteId] = t;
        return t;
    }

    /// <summary>
    /// noteId에 해당하는 노트를 반환(제거). 오브젝트 풀링 적용 시 이 메서드만 교체.
    /// </summary>
    public void ReleaseNote(int noteId)
    {
        if (!activeNotes.TryGetValue(noteId, out Transform t)) return;
        if (t != null) Destroy(t.gameObject);
        activeNotes.Remove(noteId);
    }

    /// <summary>
    /// 모든 활성 노트를 반환(제거).
    /// </summary>
    public void ReleaseAll()
    {
        foreach (var kvp in activeNotes)
        {
            if (kvp.Value != null) Destroy(kvp.Value.gameObject);
        }
        activeNotes.Clear();
    }
}
