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
    [SerializeField] private Transform attackNoteContainer;

    [Header("Positions")]
    [SerializeField] private float p1StartX;
    [SerializeField] private float p1EndX;
    [SerializeField] private float p2StartX;
    [SerializeField] private float p2EndX;

    [Header("Defense Lines")]
    [SerializeField] private Transform p1DefenseLine;
    [SerializeField] private Transform p2DefenseLine;

    [Header("EVT_ATK_01 Half Note Indicator")]
    [Tooltip("반쪽 노트 표시용 SpriteRenderer. 자식 오브젝트에 배치하고 연결한다.")]
    [SerializeField] private SpriteRenderer halfNoteIndicator;
    [SerializeField] private Sprite halfHighSprite;
    [SerializeField] private Sprite halfLowSprite;

    [Header("Normal Note Exit")]
    [Tooltip("P1이 공격한 일반 노트가 사라지는 위치. P2 쪽 레인 끝에 배치한다.")]
    [SerializeField] private Transform p1AttackLaneEnd;

    [Tooltip("P2가 공격한 일반 노트가 사라지는 위치. P1 쪽 레인 끝에 배치한다.")]
    [SerializeField] private Transform p2AttackLaneEnd;


    private struct NoteEntry
    {
        public Transform rect;
        public int noteId;
        public float initialX;
        public double judgeTime;

        // 판정 완료로 화면 제거 요청을 받았는지 기록한다.
        public bool removeRequested;
    }

    private readonly List<NoteEntry> spawnedNotes = new();
    private readonly List<NoteEntry> spawnedGhostNotes = new();

    private bool isTransferring;
    private float transferJudgeLineX;
    private float transferSpeed;

    private AttackSide currentSide;
    private double attackStartDspTime;
    private double attackDuration;
    private bool isMoving;

    // 멀티플레이 상대 공격도 이 렌더러에는 현재 턴 정보가 설정된다.
    public AttackSide CurrentSide => currentSide;
    public double AttackStartDspTime => attackStartDspTime;
    public double AttackDuration => attackDuration;
    private double lastTransferUpdateDspTime;

    private void Awake()
    {
        if (attackNoteContainer == null)
            attackNoteContainer = transform;

        if (p1AttackLine != null) p1AttackLine.gameObject.SetActive(false);
        if (p2AttackLine != null) p2AttackLine.gameObject.SetActive(false);
    }

    /// <summary>
    /// AttackTurn에서 공격 시작 시 호출. 공격 판정선 이동을 시작한다.
    /// </summary>
    public void BeginAttackVisual(AttackSide side, double startDspTime, double duration, int gridStepCount)
    {
        ClearAll();
        currentSide = side;
        attackStartDspTime = startDspTime;
        attackDuration = System.Math.Max(0.01, duration);
        isMoving = true;

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
    /// AttackTurn에서 공격 노트 생성 시 호출. noteRelativeTime 위치에 노트를 표시한다.
    /// </summary>
    public Vector3? SpawnAttackNote(AttackSide side, NoteData note, double duration)
    {
        if (NoteRenderer.Instance == null)
        {
            Debug.LogWarning("NoteRenderer.Instance가 없습니다.");
            return null;
        }

        Transform t = NoteRenderer.Instance.AcquireNote(note.noteId, note.noteType);
        if (t == null) return null;
        if (attackNoteContainer != null) t.SetParent(attackNoteContainer, false);

        double safeDuration = System.Math.Max(0.01, duration);
        float ratio = Mathf.Clamp01((float)(note.noteRelativeTime / safeDuration));
        float x = Mathf.Lerp(GetStartX(side), GetEndX(side), ratio);

        t.localPosition = new Vector3(x, 0f, 0f);
        spawnedNotes.Add(new NoteEntry { rect = t, noteId = note.noteId, initialX = x });
        return t.position;
    }

    /// <summary>
    /// EVT_DEF_01 유령 신호 노트를 표시한다.
    /// 일반 노트와 같은 경로/속도로 이동하지만, 일반 판정 노트 목록과는 분리된다.
    /// </summary>
    public void SpawnGhostNote(
        GhostNoteData ghostNote,
        double duration,
        float attackStartX,
        float attackEndX,
        float alpha
    )
    {
        if (ghostNote == null)
        {
            return;
        }

        if (NoteRenderer.Instance == null)
        {
            Debug.LogWarning("NoteRenderer.Instance가 없습니다.");
            return;
        }

        Transform t = NoteRenderer.Instance.AcquireGhostNote(
            ghostNote.noteId,
            ghostNote.noteType,
            alpha
        );

        if (t == null)
        {
            return;
        }

        if (attackNoteContainer != null)
        {
            t.SetParent(attackNoteContainer, false);
        }

        double safeDuration = System.Math.Max(0.01, duration);
        float ratio = Mathf.Clamp01((float)(ghostNote.noteRelativeTime / safeDuration));
        float x = Mathf.Lerp(attackStartX, attackEndX, ratio);

        t.localPosition = new Vector3(x, 0f, 0f);

        spawnedGhostNotes.Add(new NoteEntry
        {
            rect = t,
            noteId = ghostNote.noteId,
            initialX = x,
            judgeTime = ghostNote.judgeTime
        });
    }

    /// <summary>
    /// noteId에 해당하는 유령 노트를 제거한다.
    /// </summary>
    public void RemoveGhostNote(
        int noteId,
        bool playDisappear = false)
    {
        for (int i = spawnedGhostNotes.Count - 1; i >= 0; i--)
        {
            NoteEntry entry = spawnedGhostNotes[i];

            if (entry.noteId != noteId)
            {
                continue;
            }

            spawnedGhostNotes.RemoveAt(i);

            NoteRenderer pool = NoteRenderer.Instance;

            if (pool == null)
            {
                return;
            }

            if (!playDisappear || entry.rect == null)
            {
                pool.ReleaseNote(noteId);
                return;
            }

            // 이동 갱신보다 판정 처리가 먼저 실행된 경우에도 위치를 맞춘다.
            if (isTransferring &&
                AudioSettings.dspTime >= entry.judgeTime)
            {
                Vector3 pos = entry.rect.localPosition;
                pos.x = transferJudgeLineX;
                entry.rect.localPosition = pos;
            }

            pool.ReleaseGhostNoteAfterAnimation(noteId);
            return;
        }
    }

    /// <summary>
    /// 모든 유령 노트를 제거한다.
    /// </summary>
    public void ClearGhostNotes()
    {
        for (int i = spawnedGhostNotes.Count - 1; i >= 0; i--)
        {
            NoteRenderer.Instance?.ReleaseNote(spawnedGhostNotes[i].noteId);
        }

        spawnedGhostNotes.Clear();
    }

    /// <summary>
    /// EVT_ATK_01 첫 탭 시 호출. 그리드 위치에 반쪽 노트 인디케이터를 표시한다.
    /// P1(오른쪽→왼쪽 이동)은 flipX=false, P2(왼쪽→오른쪽)는 flipX=true.
    /// </summary>
    public void SpawnHalfAttackNote(AttackSide side, NoteType type, double noteRelativeTime, double attackDuration)
    {
        if (halfNoteIndicator == null) return;

        double safeDuration = System.Math.Max(0.01, attackDuration);
        float ratio = Mathf.Clamp01((float)(noteRelativeTime / safeDuration));
        float x = Mathf.Lerp(GetStartX(side), GetEndX(side), ratio);

        halfNoteIndicator.transform.localPosition = new Vector3(x, 0f, 0f);
        halfNoteIndicator.sprite = type == NoteType.HIGH ? halfHighSprite : halfLowSprite;
        halfNoteIndicator.flipX = (side == AttackSide.P1);
        halfNoteIndicator.transform.localScale = UnityEngine.Vector3.one;
        halfNoteIndicator.gameObject.SetActive(true);
    }

    /// <summary>
    /// 반쪽 노트 인디케이터를 숨긴다.
    /// 완성 또는 파괴 시 호출된다.
    /// </summary>
    public void ClearHalfNote()
    {
        if (halfNoteIndicator != null)
            halfNoteIndicator.gameObject.SetActive(false);
    }

    /// <summary>
    /// 공격 판정선 이동만 정지. 생성된 노트는 유지한다.
    /// </summary>
    public void StopLine()
    {
        isMoving = false;
        Transform line = GetAttackLine(currentSide);
        if (line != null) line.gameObject.SetActive(false);
    }

    /// <summary>
    /// 스폰된 공격 노트를 모두 제거하고 판정선을 비활성화한다.
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

        ClearGhostNotes();
        ClearHalfNote();
    }

    private bool isPaused;

    /// <summary>
    /// 렌더러 Update 루프를 일시정지하거나 재개한다.
    /// </summary>
    public void SetPaused(bool value) => isPaused = value;

    private void Update()
    {
        if (isPaused) return;

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
    /// 노트별 도착 시각(judgeTime)을 설정한다. DefenseTurn.Begin()에서 노트별로 호출 후 StartTransfer를 호출한다.
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
    /// 방어 전환 시 호출. 각 노트는 SetNoteJudgeTime으로 설정된 judgeTime에서
    /// 역산한 출발 시각에 따라 이동 시작한다.
    /// </summary>
    public void StartTransfer(float judgeLineX, float speed)
    {
        transferJudgeLineX = judgeLineX;
        transferSpeed = Mathf.Max(0.001f, Mathf.Abs(speed));
        isTransferring = true;
        lastTransferUpdateDspTime = AudioSettings.dspTime;

        double now = AudioSettings.dspTime;

        Debug.Log(
            $"[Transfer] side:{currentSide}, speed:{transferSpeed:F2}, " +
            $"normal:{spawnedNotes.Count}, ghost:{spawnedGhostNotes.Count}",
            this);

        foreach (NoteEntry entry in spawnedGhostNotes)
        {
            double travelTime =
                Mathf.Abs(transferJudgeLineX - entry.initialX) / transferSpeed;

            double departureTime = entry.judgeTime - travelTime;

            Debug.Log(
                $"[GhostTransfer] id:{entry.noteId}, " +
                $"from:{entry.initialX:F2}, to:{transferJudgeLineX:F2}, " +
                $"remain:{entry.judgeTime - now:F3}s, " +
                $"travel:{travelTime:F3}s, " +
                $"lateBy:{System.Math.Max(0.0, now - departureTime):F3}s",
                this);
        }
    }

    /// <summary>
    /// 유령 노트가 방어 시작 이후에 출발해 판정 시각까지 도착하는 데
    /// 필요한 최소 공통 속도를 구한다.
    /// </summary>
    public float GetGhostTransferSpeed(
        float judgeLineX,
        double defenseStartDspTime,
        float fallbackSpeed)
    {
        float requiredSpeed = 0f;

        foreach (NoteEntry entry in spawnedGhostNotes)
        {
            if (entry.rect == null)
            {
                continue;
            }

            double remainingTime = entry.judgeTime - defenseStartDspTime;

            if (remainingTime <= 0.0)
            {
                continue;
            }

            float distance = Mathf.Abs(judgeLineX - entry.initialX);
            float speed = (float)(distance / remainingTime);

            requiredSpeed = Mathf.Max(requiredSpeed, speed);
        }

        return requiredSpeed > 0f
            ? requiredSpeed
            : fallbackSpeed;
    }

    /// <summary>
    /// 타격한 일반 노트의 시각 오브젝트를 즉시 반환한다.
    /// 미입력 노트는 이동 갱신에서 레인 끝 도착 시 반환한다.
    /// </summary>
    public void RemoveNote(int noteId)
    {
        for (int i = spawnedNotes.Count - 1; i >= 0; i--)
        {
            if (spawnedNotes[i].noteId != noteId)
            {
                continue;
            }

            NoteRenderer.Instance?.ReleaseNote(noteId);
            spawnedNotes.RemoveAt(i);
            return;
        }
    }

    /// <summary>
    /// 일반 공격/방어 노트의 알파값을 변경한다.
    /// DEF_02 음향 수신 이벤트에서 방어 노트를 숨기거나 복구할 때 사용한다.
    /// 유령 노트는 spawnedGhostNotes로 분리되어 있으므로 여기서는 건드리지 않는다.
    /// </summary>
    public void SetNormalNotesAlpha(float alpha)
    {
        foreach (NoteEntry entry in spawnedNotes)
        {
            NoteRenderer.Instance?.SetNoteAlpha(entry.noteId, alpha);
        }
    }

    /// <summary>
    /// 공격 구간 시작 X 좌표를 절대 좌표로 반환한다.
    /// </summary>
    public float GetStartX(AttackSide side) => side == AttackSide.P1 ? p1StartX : p2StartX;

    /// <summary>
    /// 공격 구간 끝 X 좌표를 절대 좌표로 반환한다.
    /// </summary>
    public float GetEndX(AttackSide side) => side == AttackSide.P1 ? p1EndX : p2EndX;

    /// <summary>
    /// 공격자 기준 방어 판정선 X 좌표를 절대 좌표로 반환한다.
    /// P1 공격 시 P2 방어선, P2 공격 시 P1 방어선 위치를 반환한다.
    /// </summary>
    public float GetJudgeLineX(AttackSide attackerSide)
    {
        Transform line = attackerSide == AttackSide.P1 ? p2DefenseLine : p1DefenseLine;
        if (line != null) return line.position.x;
        Debug.LogWarning($"AttackTurnRenderer: DefenseLine이 연결되지 않았습니다.");
        return attackerSide == AttackSide.P1 ? 15f : -15f;
    }

    /// <summary>
    /// 현재 전송 중인 판정선의 월드 좌표를 반환한다.
    /// X는 transferJudgeLineX, Y/Z는 실제 DefenseLine 트랜스폼 기준.
    /// </summary>
    public Vector3 GetTransferJudgeLineWorldPos()
    {
        Transform line = p1DefenseLine != null ? p1DefenseLine : p2DefenseLine;
        float y = line != null ? line.position.y : 0f;
        float z = line != null ? line.position.z : 0f;
        return new Vector3(transferJudgeLineX, y, z);
    }

    private void UpdateTransferMovement()
    {
        double now = AudioSettings.dspTime;
        double gap = now - lastTransferUpdateDspTime;

        // 이동 갱신 사이에 50ms 이상 경과한 경우만 기록한다.
        if (lastTransferUpdateDspTime > 0.0 && gap > 0.05)
        {
            Debug.LogWarning(
                $"[MoveGap] frame:{Time.frameCount}, " +
                $"gap:{gap * 1000.0:F1}ms, speed:{transferSpeed:F2}",
                this);
        }

        lastTransferUpdateDspTime = now;

        UpdateTransferMovementFor(spawnedNotes);
        UpdateTransferMovementFor(spawnedGhostNotes, isGhost: true);
    }

    private void UpdateTransferMovementFor(
    List<NoteEntry> noteEntries,
    bool isGhost = false)
    {
        double now = AudioSettings.dspTime;

        Transform laneEnd = currentSide == AttackSide.P1
            ? p1AttackLaneEnd
            : p2AttackLaneEnd;

        for (int i = noteEntries.Count - 1; i >= 0; i--)
        {
            NoteEntry entry = noteEntries[i];

            if (entry.rect == null)
            {
                noteEntries.RemoveAt(i);
                continue;
            }

            double travelTime = transferSpeed > 0f
                ? Mathf.Abs(transferJudgeLineX - entry.initialX) / transferSpeed
                : 0.0;

            double departureTime = entry.judgeTime - travelTime;
            float elapsed =
                (float)System.Math.Max(0.0, now - departureTime);

            float dir = Mathf.Sign(transferJudgeLineX - entry.initialX);
            float newX = entry.initialX + dir * transferSpeed * elapsed;

            // 유령은 판정선, 일반 노트는 레인 끝을 종점으로 사용한다.
            float endX = transferJudgeLineX;

            if (!isGhost && laneEnd != null)
            {
                endX = entry.rect.parent != null
                    ? entry.rect.parent.InverseTransformPoint(laneEnd.position).x
                    : laneEnd.position.x;
            }

            bool reachedEnd = dir > 0f
                ? newX >= endX
                : newX <= endX;

            if (reachedEnd)
            {
                newX = endX;
            }

            Vector3 pos = entry.rect.localPosition;
            pos.x = newX;
            entry.rect.localPosition = pos;

            if (isGhost)
            {
                if (now >= entry.judgeTime)
                {
                    pos.x = transferJudgeLineX;
                    entry.rect.localPosition = pos;

                    // 이동 목록에서는 제외하되,
                    // 풀 반환은 소멸 애니메이션이 끝난 뒤 수행한다.
                    RemoveGhostNote(entry.noteId, playDisappear: true);
                }

                // 유령은 아래 일반 노트 반환 코드를 실행하지 않는다.
                continue;
            }

            // 타격하지 않아 남아 있는 일반 노트만 레인 끝에서 반환한다.
            if (reachedEnd)
            {
                NoteRenderer.Instance?.ReleaseNote(entry.noteId);
                noteEntries.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// 현재 배치 위치에서 지정한 속도로 판정선까지 이동하는 시간을 구한다.
    /// </summary>
    public bool TryGetGhostTransferTime(
        int noteId,
        float judgeLineX,
        float speed,
        out double travelTime)
    {
        travelTime = 0.0;

        if (speed <= 0f)
        {
            return false;
        }

        foreach (NoteEntry entry in spawnedGhostNotes)
        {
            if (entry.noteId != noteId || entry.rect == null)
            {
                continue;
            }

            travelTime =
                Mathf.Abs(judgeLineX - entry.initialX) / speed;

            return true;
        }

        return false;
    }

    /// <summary>
    /// 유령의 위치는 유지하고 판정선 도착 시각만 변경한다.
    /// </summary>
    public void SetGhostNoteJudgeTime(int noteId, double judgeTime)
    {
        for (int i = 0; i < spawnedGhostNotes.Count; i++)
        {
            NoteEntry entry = spawnedGhostNotes[i];

            if (entry.noteId != noteId)
            {
                continue;
            }

            entry.judgeTime = judgeTime;
            spawnedGhostNotes[i] = entry;
            return;
        }
    }
}
