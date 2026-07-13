using System.Collections.Generic;
using UnityEngine;

public class NoteRenderer : SceneSingleton<NoteRenderer>
{
    [SerializeField] private Transform noteContainer;
    [SerializeField] private Transform notePrefab;
    [SerializeField] private int initialPoolSize = 8; // 기본 8개

    private readonly Dictionary<int, Transform> activeNotes = new();
    private readonly Stack<Transform> pool = new();

    protected override void Awake()
    {
        base.Awake();
        Prewarm();
    }

    /// <summary>
    /// notePrefab를 미리 생성해 풀에 넣어두고, 필요 시 AcquireNote로 꺼내 쓰도록 함.
    /// </summary>
    private void Prewarm()
    {
        if (notePrefab == null) return;
        Transform parent = noteContainer != null ? noteContainer : transform;
        for (int i = 0; i < initialPoolSize; i++)
        {
            Transform t = Instantiate(notePrefab, parent);
            t.gameObject.SetActive(false);
            pool.Push(t);
        }
    }

    /// <summary>
    /// noteId에 해당하는 노트를 풀에서 꺼내 활성화해 반환. 풀이 비어 있으면 null 반환.
    /// </summary>
    public Transform AcquireNote(int noteId)
    {
        if (pool.Count == 0)
        {
            Debug.LogWarning("NoteRenderer: 풀이 비어 있습니다. initialPoolSize를 늘리세요.");
            return null;
        }
        Transform t = pool.Pop();
        t.name = $"Note_{noteId}";
        t.gameObject.SetActive(true);
        activeNotes[noteId] = t;
        return t;
    }

    /// <summary>
    /// noteId에 해당하는 노트를 비활성화해 풀에 반환.
    /// </summary>
    public void ReleaseNote(int noteId)
    {
        if (!activeNotes.TryGetValue(noteId, out Transform t)) return;
        activeNotes.Remove(noteId);
        ReturnToPool(t);
    }

    /// <summary>
    /// 모든 활성 노트를 비활성화해 풀에 반환.
    /// </summary>
    public void ReleaseAll()
    {
        foreach (var kvp in activeNotes)
            ReturnToPool(kvp.Value);
        activeNotes.Clear();
    }

    /// <summary>
    /// note를 비활성화해 풀에 반환.
    /// </summary>
    private void ReturnToPool(Transform t)
    {
        if (t == null) return;
        t.gameObject.SetActive(false);
        pool.Push(t);
    }
}
