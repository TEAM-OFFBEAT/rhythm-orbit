using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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
        if (prefab == null)
        {
            return;
        }

        Transform parent = noteContainer != null ? noteContainer : transform;

        for (int i = 0; i < initialPoolSize; i++)
        {
            Transform note = Instantiate(prefab, parent);
            note.gameObject.SetActive(false);
            pool.Push(note);
        }
    }

    /// <summary>
    /// noteId와 noteType에 해당하는 노트를 타입별 풀에서 꺼내 활성화해 반환한다.
    /// 풀이 비어 있으면 null을 반환한다.
    /// </summary>
    public Transform AcquireNote(int noteId, NoteType noteType)
    {
        Stack<Transform> pool = noteType == NoteType.HIGH ? poolHigh : poolLow;

        if (pool.Count == 0)
        {
            Debug.LogWarning($"NoteRenderer: {noteType} 풀이 비어 있습니다. initialPoolSize를 늘리세요.");
            return null;
        }

        Transform note = pool.Pop();

        note.name = $"Note_{noteId}";
        HideKeyHint(note);
        SetVisualAlpha(note, 1f);

        note.gameObject.SetActive(true);
        activeNotes[noteId] = (note, noteType);

        return note;
    }

    /// <summary>
    /// EVT_DEF_01 유령 노트용으로 기존 노트를 가져오되, 알파값을 낮춰 흐리게 표시한다.
    /// </summary>
    public Transform AcquireGhostNote(int noteId, NoteType noteType, float alpha)
    {
        Transform note = AcquireNote(noteId, noteType);

        if (note == null)
        {
            return null;
        }

        note.name = $"GhostNote_{noteId}";
        SetVisualAlpha(note, alpha);

        return note;
    }

    /// <summary>
    /// noteId에 해당하는 노트를 비활성화해 타입별 풀에 반환한다.
    /// </summary>
    public void ReleaseNote(int noteId)
    {
        if (!activeNotes.TryGetValue(noteId, out var entry))
        {
            return;
        }

        activeNotes.Remove(noteId);

        if (entry.obj == null)
        {
            return;
        }

        HideKeyHint(entry.obj);
        SetVisualAlpha(entry.obj, 1f);

        entry.obj.gameObject.SetActive(false);

        Stack<Transform> pool = entry.type == NoteType.HIGH ? poolHigh : poolLow;
        pool.Push(entry.obj);
    }

    /// <summary>
    /// 모든 활성 노트를 비활성화해 타입별 풀에 반환한다.
    /// </summary>
    public void ReleaseAll()
    {
        foreach (var kvp in activeNotes)
        {
            Transform note = kvp.Value.obj;

            if (note == null)
            {
                continue;
            }

            HideKeyHint(note);
            SetVisualAlpha(note, 1f);
            note.gameObject.SetActive(false);

            Stack<Transform> pool = kvp.Value.type == NoteType.HIGH ? poolHigh : poolLow;
            pool.Push(note);
        }

        activeNotes.Clear();
    }

    /// <summary>
    /// 특정 노트의 알파값을 변경한다.
    /// DEF_01 유령 노트 fade in에 사용한다.
    /// </summary>
    public void SetNoteAlpha(int noteId, float alpha)
    {
        if (!activeNotes.TryGetValue(noteId, out var entry))
        {
            return;
        }

        SetVisualAlpha(entry.obj, alpha);
    }

    /// <summary>
    /// 튜토리얼용 F/J 키 힌트를 노트 위에 표시한다.
    /// </summary>
    public void ShowKeyHint(int noteId, string text, Color color)
    {
        if (!activeNotes.TryGetValue(noteId, out var entry))
        {
            return;
        }

        Transform noteTransform = entry.obj;

        if (noteTransform == null)
        {
            return;
        }

        NoteKeyHintView hint = noteTransform.GetComponentInChildren<NoteKeyHintView>(true);
        hint?.Show(text, color);
    }

    /// <summary>
    /// 튜토리얼용 F/J 키 힌트를 숨긴다.
    /// 풀링된 노트가 재사용될 때 이전 힌트가 남지 않도록 사용한다.
    /// </summary>
    public void HideKeyHint(int noteId)
    {
        if (!activeNotes.TryGetValue(noteId, out var entry))
        {
            return;
        }

        HideKeyHint(entry.obj);
    }

    private void HideKeyHint(Transform noteTransform)
    {
        if (noteTransform == null)
        {
            return;
        }

        NoteKeyHintView hint = noteTransform.GetComponentInChildren<NoteKeyHintView>(true);
        hint?.Hide();
    }

    /// <summary>
    /// 노트 오브젝트 아래의 SpriteRenderer / UI Graphic 알파를 변경한다.
    /// </summary>
    private void SetVisualAlpha(Transform target, float alpha)
    {
        if (target == null)
        {
            return;
        }

        float safeAlpha = Mathf.Clamp01(alpha);

        SpriteRenderer[] spriteRenderers = target.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer spriteRenderer in spriteRenderers)
        {
            if (spriteRenderer == null)
            {
                continue;
            }

            Color color = spriteRenderer.color;
            color.a = safeAlpha;
            spriteRenderer.color = color;
        }

        Graphic[] graphics = target.GetComponentsInChildren<Graphic>(true);
        foreach (Graphic graphic in graphics)
        {
            if (graphic == null)
            {
                continue;
            }

            Color color = graphic.color;
            color.a = safeAlpha;
            graphic.color = color;
        }
    }
}