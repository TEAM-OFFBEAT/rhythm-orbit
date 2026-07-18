using System.Collections.Generic;
using UnityEngine;

public enum AttackSide
{
    P1,
    P2
}

public class AttackTurnRenderer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform p1AttackLine;
    [SerializeField] private Transform p2AttackLine;
    // [SerializeField] private Transform attackGridContainer;
    [SerializeField] private Transform attackNoteContainer;

    [Header("Positions")]
    [SerializeField] private float p1StartX = 5f;
    [SerializeField] private float p1EndX = -5f;
    [SerializeField] private float p2StartX = -5f;
    [SerializeField] private float p2EndX = 5f;
    [SerializeField] private float noteY = 0f;

    [Header("Defense Lines")]
    [SerializeField] private Transform p1DefenseLine;
    [SerializeField] private Transform p2DefenseLine;

    // [Header("Grid Visual")]
    // [SerializeField] private Sprite gridLineSprite;
    // [SerializeField] private Vector2 gridLineSize = new Vector2(0.03f, 1.2f);
    // [SerializeField] private Color beatGridColor = new Color(1f, 0.82f, 0.36f, 0.65f);
    // [SerializeField] private Color halfBeatGridColor = new Color(1f, 1f, 1f, 0.22f);


    private struct NoteEntry { public Transform rect; public int noteId; public float initialX; public double judgeTime; }
    private readonly List<NoteEntry> spawnedNotes = new();
    // private readonly List<Transform> gridLines = new();

    private bool isTransferring;
    private float transferJudgeLineX;
    private float transferSpeed;

    private AttackSide currentSide;
    private double attackStartDspTime;
    private double attackDuration;
    private bool isMoving;

    private void Awake()
    {
        if (attackNoteContainer == null)
            attackNoteContainer = transform;

        // if (attackGridContainer == null)
        //     attackGridContainer = transform;

        if (p1AttackLine != null) p1AttackLine.gameObject.SetActive(false);
        if (p2AttackLine != null) p2AttackLine.gameObject.SetActive(false);
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

        // BuildAttackGrid(side, gridStepCount);

        Transform line = GetAttackLine(side);
        if (line == null)
        {
            Debug.LogWarning($"{side}AttackLine이 연결되지 않았습니다.");
            return;
        }

        line.gameObject.SetActive(true);
        SetLineX(side, GetStartX(side));
    }

    /// <summary>
    /// AttackTurn에서 공격 노트 생성 시 호출. noteRelativeTime 위치에 노트를 표시.
    /// </summary>
    public void SpawnAttackNote(AttackSide side, NoteData note, double duration)
    {
        if (NoteRenderer.Instance == null)
        {
            Debug.LogWarning("NoteRenderer.Instance가 없습니다.");
            return;
        }

        Transform t = NoteRenderer.Instance.AcquireNote(note.noteId, note.noteType);
        if (t == null) return;
        if (attackNoteContainer != null) t.SetParent(attackNoteContainer, false);

        double safeDuration = System.Math.Max(0.01, duration);
        float ratio = Mathf.Clamp01((float)(note.noteRelativeTime / safeDuration));
        float x = Mathf.Lerp(GetStartX(side), GetEndX(side), ratio);

        t.localPosition = new Vector3(x, noteY, 0f);
        spawnedNotes.Add(new NoteEntry { rect = t, noteId = note.noteId, initialX = x });
    }

    /// <summary>
    /// 공격 판정선 이동만 정지. 생성된 노트와 고정선은 유지.
    /// </summary>
    public void StopLine()
    {
        isMoving = false;
        Transform line = GetAttackLine(currentSide);
        if (line != null) line.gameObject.SetActive(false);
    }

    /// <summary>
    /// 스폰된 공격 노트를 모두 제거하고 판정선을 비활성화.
    /// </summary>
    public void ClearAll()
    {
        isMoving = false;
        isTransferring = false;

        if (p1AttackLine != null) p1AttackLine.gameObject.SetActive(false);
        if (p2AttackLine != null) p2AttackLine.gameObject.SetActive(false);

        foreach (NoteEntry entry in spawnedNotes)
            NoteRenderer.Instance?.ReleaseNote(entry.noteId);
        spawnedNotes.Clear();
        // ClearGrid();
    }

    private void Update()
    {
        if (isMoving && GetAttackLine(currentSide) != null)
        {
            double elapsed = AudioSettings.dspTime - attackStartDspTime;
            float t = Mathf.Clamp01((float)(elapsed / attackDuration));
            float x = Mathf.Lerp(GetStartX(currentSide), GetEndX(currentSide), t);
            SetLineX(currentSide, x);
            if (t >= 1f) isMoving = false;
        }

        if (isTransferring) UpdateTransferMovement();
    }

    // private void BuildAttackGrid(AttackSide side, int gridStepCount)
    // {
    //     ClearGrid();
    //     if (attackGridContainer == null)
    //     {
    //         Debug.LogWarning("AttackGridContainer가 연결되지 않았습니다.");
    //         return;
    //     }
    //     int safeCount = Mathf.Max(1, gridStepCount);
    //     for (int i = 0; i <= safeCount; i++)
    //     {
    //         float t = (float)i / safeCount;
    //         float x = Mathf.Lerp(GetStartX(side), GetEndX(side), t);
    //         GameObject go = new GameObject($"GridLine_{i}", typeof(SpriteRenderer));
    //         Transform lineT = go.transform;
    //         lineT.SetParent(attackGridContainer, false);
    //         lineT.localPosition = new Vector3(x, 0f, 0f);
    //         lineT.localScale = new Vector3(gridLineSize.x, gridLineSize.y, 1f);
    //         SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
    //         sr.sprite = gridLineSprite;
    //         sr.color = i % 2 == 0 ? beatGridColor : halfBeatGridColor;
    //         gridLines.Add(lineT);
    //     }
    // }

    // private void ClearGrid()
    // {
    //     foreach (Transform t in gridLines)
    //     {
    //         if (t != null) Destroy(t.gameObject);
    //     }
    //     gridLines.Clear();
    // }

    private Transform GetAttackLine(AttackSide side) => side == AttackSide.P1 ? p1AttackLine : p2AttackLine;

    private void SetLineX(AttackSide side, float x)
    {
        Transform line = GetAttackLine(side);
        if (line == null) return;
        Vector3 pos = line.position;
        pos.x = x;
        line.position = pos;
    }

    /// <summary>
    /// 노트별 도착 시각(judgeTime)을 설정. DefenseTurn.Begin()에서 노트별로 호출 후 StartTransfer 를 호출.
    /// </summary>
    public void SetNoteJudgeTime(int noteId, double judgeTime)
    {
        for (int i = 0; i < spawnedNotes.Count; i++)
        {
            if (spawnedNotes[i].noteId != noteId) continue;
            NoteEntry e = spawnedNotes[i];
            e.judgeTime = judgeTime;
            spawnedNotes[i] = e;
            return;
        }
    }

    /// <summary>
    /// 방어 전환 시 호출. 각 노트는 SetNoteJudgeTime으로 설정된 judgeTime에서 역산한 출발 시각에 따라 이동 시작.
    /// </summary>
    public void StartTransfer(float judgeLineX, float speed)
    {
        transferJudgeLineX = judgeLineX;
        transferSpeed = Mathf.Max(0.001f, Mathf.Abs(speed));
        isTransferring = true;
    }

    /// <summary>
    /// noteId에 해당하는 노트를 제거. DefenseTurn에서 판정 후 호출.
    /// </summary>
    public void RemoveNote(int noteId)
    {
        for (int i = spawnedNotes.Count - 1; i >= 0; i--)
        {
            if (spawnedNotes[i].noteId != noteId) continue;
            NoteRenderer.Instance?.ReleaseNote(noteId);
            spawnedNotes.RemoveAt(i);
            return;
        }
    }

    public float GetStartX(AttackSide side) => side == AttackSide.P1 ? p1StartX : p2StartX;
    public float GetEndX(AttackSide side) => side == AttackSide.P1 ? p1EndX : p2EndX;

    /// <summary>
    /// 공격자 기준 방어 판정선 X 좌표. P1 공격 시 P2DefenseLine, P2 공격 시 P1DefenseLine 위치를 반환.
    /// </summary>
    public float GetJudgeLineX(AttackSide attackerSide)
    {
        Transform line = attackerSide == AttackSide.P1 ? p2DefenseLine : p1DefenseLine;
        if (line != null) return line.position.x;
        Debug.LogWarning($"AttackTurnRenderer: {(attackerSide == AttackSide.P1 ? "P2" : "P1")}DefenseLine이 연결되지 않았습니다.");
        return attackerSide == AttackSide.P1 ? 15f : -15f;
    }

    private void UpdateTransferMovement()
    {
        double now = AudioSettings.dspTime;

        for (int i = spawnedNotes.Count - 1; i >= 0; i--)
        {
            NoteEntry entry = spawnedNotes[i];
            if (entry.rect == null) { spawnedNotes.RemoveAt(i); continue; }

            double travelTime = transferSpeed > 0f
                ? Mathf.Abs(transferJudgeLineX - entry.initialX) / transferSpeed
                : 0.0;
            double departureTime = entry.judgeTime - travelTime;
            float elapsed = (float)System.Math.Max(0.0, now - departureTime);

            float dir = Mathf.Sign(transferJudgeLineX - entry.initialX);
            float newX = entry.initialX + dir * transferSpeed * elapsed;

            if (dir > 0f && newX > transferJudgeLineX) newX = transferJudgeLineX;
            if (dir < 0f && newX < transferJudgeLineX) newX = transferJudgeLineX;

            Vector3 pos = entry.rect.localPosition;
            pos.x = newX;
            entry.rect.localPosition = pos;
        }
    }
}
