using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum AttackSide
{
    P1,
    P2
}

public class AttackTurnRenderer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform attackLine;
    [SerializeField] private RectTransform attackGridContainer;
    [SerializeField] private RectTransform attackNoteContainer;
    [SerializeField] private RectTransform attackNotePrefab;

    [Header("Positions")]
    [SerializeField] private float p1StartX = 500f;
    [SerializeField] private float p1EndX = -500f;
    [SerializeField] private float p2StartX = -500f;
    [SerializeField] private float p2EndX = 500f;
    [SerializeField] private float noteY = 0f;

    [Header("Grid Visual")]
    [SerializeField] private Vector2 gridLineSize = new Vector2(3f, 120f);
    [SerializeField] private Color beatGridColor = new Color(1f, 0.82f, 0.36f, 0.65f);
    [SerializeField] private Color halfBeatGridColor = new Color(1f, 1f, 1f, 0.22f);

    [Header("Attack Visual")]
    [SerializeField] private Vector2 fallbackNoteSize = new Vector2(36f, 36f);
    [SerializeField] private Color p1LineColor = new Color(0.25f, 0.88f, 0.82f);
    [SerializeField] private Color p2LineColor = new Color(1.0f, 0.37f, 0.63f);
    [SerializeField] private Color noteColor = new Color(1.0f, 0.82f, 0.36f);

    private readonly List<RectTransform> spawnedNotes = new();
    private readonly List<RectTransform> gridLines = new();

    private AttackSide currentSide;
    private double attackStartDspTime;
    private double attackDuration;
    private bool isMoving;

    private void Awake()
    {
        if (attackNoteContainer == null)
        {
            attackNoteContainer = transform as RectTransform;
        }

        if (attackGridContainer == null)
        {
            attackGridContainer = transform as RectTransform;
        }

        if (attackLine != null)
        {
            attackLine.gameObject.SetActive(false);

            Image lineImage = attackLine.GetComponent<Image>();
            if (lineImage != null)
            {
                lineImage.raycastTarget = false;
            }
        }
    }

    /// <summary>
    /// AttackTurn에서 공격 시작 시 호출. 고정 박자선을 만들고 공격 판정선 이동을 시작.
    /// </summary>
    public void BeginAttackVisual(AttackSide side, double startDspTime, double duration, int gridStepCount)
    {
        ClearAll();

        currentSide = side;
        attackStartDspTime = startDspTime;
        attackDuration = System.Math.Max(0.01, duration);
        isMoving = true;

        BuildAttackGrid(side, gridStepCount);

        if (attackLine == null)
        {
            Debug.LogWarning("AttackLine이 연결되지 않았습니다.");
            return;
        }

        attackLine.gameObject.SetActive(true);
        SetLineColor(side);
        SetLineX(GetStartX(side));
    }

    /// <summary>
    /// AttackTurn에서 공격 노트 생성 시 호출. noteRelativeTime 위치에 노트를 표시.
    /// </summary>
    public void SpawnAttackNote(AttackSide side, NoteData note, double duration)
    {
        if (attackNoteContainer == null)
        {
            Debug.LogWarning("AttackNoteContainer가 연결되지 않았습니다.");
            return;
        }

        RectTransform rect = CreateNoteRect(note.noteId);
        rect.SetParent(attackNoteContainer, false);

        double safeDuration = System.Math.Max(0.01, duration);
        float t = Mathf.Clamp01((float)(note.noteRelativeTime / safeDuration));
        float x = Mathf.Lerp(GetStartX(side), GetEndX(side), t);

        rect.anchoredPosition = new Vector2(x, noteY);
        spawnedNotes.Add(rect);
    }

    /// <summary>
    /// 공격 판정선 이동만 정지. 생성된 노트와 고정선은 유지.
    /// </summary>
    public void StopLine()
    {
        isMoving = false;

        if (attackLine != null)
        {
            attackLine.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 공격 판정선, 공격 노트, 고정 박자선을 모두 제거.
    /// </summary>
    public void ClearAll()
    {
        isMoving = false;

        if (attackLine != null)
        {
            attackLine.gameObject.SetActive(false);
        }

        foreach (RectTransform note in spawnedNotes)
        {
            if (note != null)
            {
                Destroy(note.gameObject);
            }
        }

        spawnedNotes.Clear();
        ClearGrid();
    }

    private void Update()
    {
        if (!isMoving || attackLine == null) return;

        double elapsed = AudioSettings.dspTime - attackStartDspTime;
        float t = Mathf.Clamp01((float)(elapsed / attackDuration));
        float x = Mathf.Lerp(GetStartX(currentSide), GetEndX(currentSide), t);

        SetLineX(x);

        if (t >= 1f)
        {
            isMoving = false;
        }
    }

    /// <summary>
    /// 공격 구간을 고정된 반박 칸 수만큼 나누어 고정 박자선을 생성.
    /// </summary>
    private void BuildAttackGrid(AttackSide side, int gridStepCount)
    {
        ClearGrid();

        if (attackGridContainer == null)
        {
            Debug.LogWarning("AttackGridContainer가 연결되지 않았습니다.");
            return;
        }

        int safeGridStepCount = Mathf.Max(1, gridStepCount);

        for (int i = 0; i <= safeGridStepCount; i++)
        {
            float t = (float)i / safeGridStepCount;
            float x = Mathf.Lerp(GetStartX(side), GetEndX(side), t);

            GameObject go = new GameObject($"GridLine_{i}", typeof(Image));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(attackGridContainer, false);
            rect.sizeDelta = gridLineSize;
            rect.anchoredPosition = new Vector2(x, 0f);

            Image image = go.GetComponent<Image>();
            image.raycastTarget = false;

            bool isBeat = i % 2 == 0;
            image.color = isBeat ? beatGridColor : halfBeatGridColor;

            gridLines.Add(rect);
        }
    }

    /// <summary>
    /// 생성된 고정 박자선을 모두 제거.
    /// </summary>
    private void ClearGrid()
    {
        foreach (RectTransform line in gridLines)
        {
            if (line != null)
            {
                Destroy(line.gameObject);
            }
        }

        gridLines.Clear();
    }

    private RectTransform CreateNoteRect(int noteId)
    {
        if (attackNotePrefab != null)
        {
            RectTransform note = Instantiate(attackNotePrefab);
            note.name = $"AttackNote_{noteId}";

            Image prefabImage = note.GetComponent<Image>();
            if (prefabImage != null)
            {
                prefabImage.raycastTarget = false;
            }

            return note;
        }

        GameObject go = new GameObject($"AttackNote_{noteId}", typeof(Image));
        Image image = go.GetComponent<Image>();
        image.color = noteColor;
        image.raycastTarget = false;

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = fallbackNoteSize;
        return rect;
    }

    private void SetLineX(float x)
    {
        Vector2 pos = attackLine.anchoredPosition;
        pos.x = x;
        attackLine.anchoredPosition = pos;
    }

    private void SetLineColor(AttackSide side)
    {
        Image image = attackLine.GetComponent<Image>();
        if (image == null) return;

        image.color = side == AttackSide.P1 ? p1LineColor : p2LineColor;
    }

    private float GetStartX(AttackSide side)
    {
        return side == AttackSide.P1 ? p1StartX : p2StartX;
    }

    private float GetEndX(AttackSide side)
    {
        return side == AttackSide.P1 ? p1EndX : p2EndX;
    }
}