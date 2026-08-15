using System.Collections.Generic;
using UnityEngine;

public class NoteRenderer : SceneSingleton<NoteRenderer>
{
    [SerializeField] private Transform noteContainer;
    [SerializeField] private Transform notePrefabHigh;
    [SerializeField] private Transform notePrefabLow;
    [SerializeField] private int initialPoolSize = 8;

    private readonly Dictionary<int, (Transform obj, NoteType type)> activeNotes = new();
    private readonly Stack<Transform> poolHigh = new();
    private readonly Stack<Transform> poolLow = new();

    protected override void Awake()
    {
        base.Awake();
        PrewarmPool(notePrefabHigh, poolHigh);
        PrewarmPool(notePrefabLow, poolLow);
    }

    private void PrewarmPool(Transform prefab, Stack<Transform> pool)
    {
        if (prefab == null) return;
        Transform parent = noteContainer != null ? noteContainer : transform;
        for (int i = 0; i < initialPoolSize; i++)
        {
            Transform t = Instantiate(prefab, parent);
            t.gameObject.SetActive(false);
            pool.Push(t);
        }
    }

    /// <summary>
    /// noteId와 noteType에 해당하는 노트를 타입별 풀에서 꺼내 활성화해 반환. 풀이 비어 있으면 null 반환.
    /// </summary>
    public Transform AcquireNote(int noteId, NoteType noteType)
    {
        Stack<Transform> pool = noteType == NoteType.HIGH ? poolHigh : poolLow;
        if (pool.Count == 0)
        {
            Debug.LogWarning($"NoteRenderer: {noteType} 풀이 비어 있습니다. initialPoolSize를 늘리세요.");
            return null;
        }
        Transform t = pool.Pop();
        t.name = $"Note_{noteId}";
        HideKeyHint(t);
        t.gameObject.SetActive(true);
        activeNotes[noteId] = (t, noteType);
        return t;
    }

    /// <summary>
    /// noteId에 해당하는 노트를 비활성화해 타입별 풀에 반환.
    /// </summary>
    public void ReleaseNote(int noteId)
    {
        if (!activeNotes.TryGetValue(noteId, out var entry)) return;
        activeNotes.Remove(noteId);
        if (entry.obj == null) return;
        HideKeyHint(entry.obj);
        entry.obj.gameObject.SetActive(false);
        Stack<Transform> pool = entry.type == NoteType.HIGH ? poolHigh : poolLow;
        pool.Push(entry.obj);
    }

    /// <summary>
    /// 모든 활성 노트를 비활성화해 타입별 풀에 반환.
    /// </summary>
    public void ReleaseAll()
    {
        foreach (var kvp in activeNotes)
        {
            if (kvp.Value.obj == null) continue;
            HideKeyHint(kvp.Value.obj);
            kvp.Value.obj.gameObject.SetActive(false);
            Stack<Transform> pool = kvp.Value.type == NoteType.HIGH ? poolHigh : poolLow;
            pool.Push(kvp.Value.obj);
        }
        activeNotes.Clear();
    }

    /// <summary>
    /// 튜토리얼용 F/J 키 힌트를 노트 위에 표시한다.
    /// </summary>
    public void ShowKeyHint(int noteId, string text, Color color)
    {
        if (!activeNotes.TryGetValue(noteId, out var entry))
            return;

        Transform noteTransform = entry.obj;

        if (noteTransform == null)
            return;

        NoteKeyHintView hint = noteTransform.GetComponentInChildren<NoteKeyHintView>(true);
        hint?.Show(text, color);
    }

    private void HideKeyHint(Transform noteTransform)
    {
        if (noteTransform == null)
            return;

        NoteKeyHintView hint = noteTransform.GetComponentInChildren<NoteKeyHintView>(true);
        hint?.Hide();
    }

    /// <summary>
    /// 튜토리얼용 F/J 키 힌트를 숨긴다.
    /// 풀링된 노트가 재사용될 때 이전 힌트가 남지 않도록 사용한다.
    /// </summary>
    public void HideKeyHint(int noteId)
    {
        if (!activeNotes.TryGetValue(noteId, out var entry))
            return;

        HideKeyHint(entry.obj);
    }
}
