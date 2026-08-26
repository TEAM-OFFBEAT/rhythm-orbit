using System.Collections.Generic;
using UnityEngine;
using TMPro;

// ─────────────────────────────────────────────────────────────
// 기습 이벤트 전체 흐름을 관리하는 매니저
// ─────────────────────────────────────────────────────────────

/// <summary>
/// 기습 이벤트의 발생 판정, 이벤트 선정, 진입, 적용, 복귀를 담당한다.
/// 
/// 이 클래스는 개별 이벤트의 세부 규칙을 직접 구현하지 않는다.
/// 예를 들어 유령 신호가 노트를 어떻게 속이는지는 여기서 하지 않는다.
/// 
/// 여기서 하는 일:
/// 1. 이번 페이즈 전환에서 이벤트가 발생 가능한지 확인
/// 2. 확률에 따라 발생 여부 결정
/// 3. 다음 페이즈에 맞는 이벤트 후보 중 하나 선택
/// 4. 선택된 이벤트를 진입/적용/복귀 순서로 호출
/// </summary>
public class SurpriseEventManager : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────
    // 인스펙터 설정값
    // ─────────────────────────────────────────────────────────

    [Header("Event Pool")]
    [Tooltip("게임에서 사용할 기습 이벤트 목록. 공격 2종, 방어 2종을 등록하는 것을 기본으로 한다.")]
    [SerializeField] private List<SurpriseEventDefinition> eventDefinitions = new();

    [Header("Common Rules")]
    [Tooltip("유효 페이즈 전환마다 이벤트가 발생할 기본 확률. 0.2 = 20%.")]
    [SerializeField, Range(0f, 1f)] private float baseTriggerChance = 0.2f;

    [Tooltip("게임 전체에서 최소로 발생시키려는 이벤트 수.")]
    [SerializeField, Min(0)] private int minEventCount = 6;

    [Tooltip("게임 전체에서 최대로 발생 가능한 이벤트 수.")]
    [SerializeField, Min(0)] private int maxEventCount = 12;

    [Header("Entry Timing")]
    [Tooltip("다음 페이즈 시작 몇 초 전에 이벤트 진입 토스트를 보여줄지 정한다.")]
    [SerializeField, Min(0f)] private float eventEntryLeadSeconds = 0.7f;

    [Header("UI")]
    [Tooltip("이벤트 진입 시 양쪽 화면에 보여줄 '이벤트 발생!' 토스트 UI 루트.")]
    [SerializeField] private GameObject eventToastRoot;

    [Header("Debug UI")]
    [SerializeField] private TMP_Text eventStateDebugText;
    [SerializeField] private bool showEventStateDebugText = true;

    [Header("Debug")]
    [Tooltip("테스트용. true면 확률과 관계없이 가능한 전환마다 이벤트를 발생시킨다.")]
    [SerializeField] private bool forceEventForTest = false;

    [Tooltip("이벤트 발생/적용/복귀 로그를 출력할지 여부.")]
    [SerializeField] private bool logEventFlow = true;

    // ─────────────────────────────────────────────────────────
    // 내부 저장용 변수
    // ─────────────────────────────────────────────────────────

    // EventId로 이벤트 설정을 빠르게 찾기 위한 Dictionary.
    private readonly Dictionary<SurpriseEventId, SurpriseEventDefinition> definitionMap = new();

    // EventId로 실제 이벤트 핸들러를 찾기 위한 Dictionary.
    private readonly Dictionary<SurpriseEventId, ISurpriseEventHandler> handlerMap = new();

    // 다음 페이즈에 적용하기로 미리 뽑아둔 이벤트.
    private SurpriseEventContext preparedEvent;

    // 지금 실제로 적용 중인 이벤트.
    private SurpriseEventContext activeEvent;

    // 이벤트가 실제 적용 페이즈에 들어갔는지 여부.
    private bool activeEventPhaseBegun;

    // 게임 전체에서 지금까지 이벤트가 몇 번 선정됐는지.
    private int totalEventCount;

    /// <summary>
    /// GameManager가 진입 타이밍 계산에 사용할 리드타임.
    /// </summary>
    public float EventEntryLeadSeconds => eventEntryLeadSeconds;

    /// <summary>
    /// 현재 적용 중인 이벤트 정보.
    /// 다른 시스템이 읽기만 할 수 있게 public getter로 둔다.
    /// </summary>
    public SurpriseEventContext ActiveEvent => activeEvent;

    /// <summary>
    /// 현재 적용 중인 이벤트가 있는지 여부.
    /// </summary>
    public bool HasActiveEvent => activeEvent != null && activeEvent.IsValid;

    // ─────────────────────────────────────────────────────────
    // Unity 생명주기
    // ─────────────────────────────────────────────────────────

    private void Awake()
    {
        BuildDefinitionMap();
        RegisterHandlersInChildren();

        if (eventToastRoot != null)
        {
            eventToastRoot.SetActive(false);
        }

        ClearEventStateDebugText();
    }

    private void OnDisable()
    {
        HideEventToast();
        ClearEventStateDebugText();
    }

    // ─────────────────────────────────────────────────────────
    // 초기화 / 등록
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// 인스펙터에 등록된 이벤트 정의 목록을 Dictionary로 변환한다.
    /// 
    /// List는 순서대로 훑는 데 좋고,
    /// Dictionary는 특정 ID로 빠르게 찾는 데 좋다.
    /// </summary>
    private void BuildDefinitionMap()
    {
        definitionMap.Clear();

        foreach (SurpriseEventDefinition definition in eventDefinitions)
        {
            if (definition == null)
            {
                continue;
            }

            if (definition.eventId == SurpriseEventId.None)
            {
                continue;
            }

            definitionMap[definition.eventId] = definition;
        }
    }

    /// <summary>
    /// SurpriseEventManager 자식 오브젝트들에서
    /// ISurpriseEventHandler를 구현한 컴포넌트를 찾아 등록한다.
    /// 
    /// 이렇게 해두면 나중에 개별 이벤트 스크립트를 자식으로 붙이기만 해도
    /// Manager가 자동으로 찾아 쓸 수 있다.
    /// 
    /// 세빈: ㄴ 라고하네요. 
    ///      기습 이벤트 매니저가 (게임 매니저처럼) 너무 길어지는 것보다 
    ///      핸들러 구현하는 스크립트를 따로 분리해서 작성하면 좋을 것 같아요!
    ///      길이에 따라 모아놓거나 네 개 다 분리하거나...
    /// </summary>
    private void RegisterHandlersInChildren()
    {
        handlerMap.Clear();

        ISurpriseEventHandler[] handlers = GetComponentsInChildren<ISurpriseEventHandler>(true);

        foreach (ISurpriseEventHandler handler in handlers)
        {
            if (handler == null)
            {
                continue;
            }

            handlerMap[handler.EventId] = handler;
        }
    }

    // ─────────────────────────────────────────────────────────
    // 1단계: 이벤트 발생 판정 및 선정
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// 페이즈 전환 시점에 GameManager가 호출한다.
    /// 
    /// 이 함수는 "다음 페이즈에 이벤트를 걸지 말지"를 결정하고,
    /// 발생한다면 어떤 이벤트를 걸지 preparedEvent에 저장한다.
    /// 
    /// 매개변수 설명:
    /// - nextPhase: 다음에 시작할 페이즈. Attack 또는 Defense.
    /// - targetPlayerId: 이벤트 영향을 받을 플레이어.
    /// - nextPhaseStartDspTime: 다음 페이즈 시작 DSP 시각.
    /// - isExcludedTransition: 이번 전환이 이벤트 발생 제외 구간인지 여부.
    /// - remainingValidTransitions: 앞으로 남은 유효 전환 횟수. 최소 횟수 보장 계산에 사용.
    /// </summary>
    public bool TryPrepareEvent(
        SurpriseEventPhase nextPhase,
        int targetPlayerId,
        double nextPhaseStartDspTime,
        bool isExcludedTransition,
        int remainingValidTransitions
    )
    {
        // 이미 진입했거나 적용 중인 이벤트가 있으면 새 이벤트를 준비하지 않는다.
        if (HasActiveEvent)
        {
            return false;
        }

        // 이전에 준비된 이벤트가 남아 있으면 지운다.
        preparedEvent = null;

        // 첫 공격/첫 방어, 라운드 전환, 게임 종료 구간 등은 제외.
        if (isExcludedTransition)
        {
            Log("발생 제외 전환이므로 이벤트 판정 생략");
            return false;
        }

        if (nextPhase == SurpriseEventPhase.None)
        {
            return false;
        }

        if (targetPlayerId <= 0)
        {
            return false;
        }

        // 최대 발생 횟수에 도달하면 더 이상 발생하지 않는다.
        if (totalEventCount >= maxEventCount)
        {
            Log("최대 발생 횟수 도달");
            return false;
        }

        // 다음 페이즈에 적용 가능한 후보 이벤트만 모은다.
        List<SurpriseEventDefinition> candidates = GetCandidates(nextPhase);

        if (candidates.Count == 0)
        {
            Log($"다음 페이즈에 적용 가능한 이벤트 없음 / phase:{nextPhase}");
            return false;
        }

        // 확률 또는 최소 횟수 보장 규칙으로 발생 여부를 정한다.
        bool shouldTrigger = ShouldTriggerEvent(remainingValidTransitions);

        if (!shouldTrigger)
        {
            Log("확률 판정 결과 미발생");
            return false;
        }

        // 후보 중 하나를 같은 확률로 랜덤 선정한다.
        SurpriseEventDefinition selected = candidates[Random.Range(0, candidates.Count)];

        preparedEvent = new SurpriseEventContext
        {
            eventId = selected.eventId,
            phase = nextPhase,
            targetPlayerId = targetPlayerId,
            phaseStartDspTime = nextPhaseStartDspTime
        };

        // 이벤트 선정 시점에 누적 횟수를 증가시킨다.
        totalEventCount++;

        Log(
            $"이벤트 선정 / id:{preparedEvent.eventId}, " +
            $"phase:{preparedEvent.phase}, target:{preparedEvent.targetPlayerId}, " +
            $"count:{totalEventCount}"
        );

        return true;
    }

    /// <summary>
    /// 특정 페이즈에 적용 가능한 이벤트 후보만 골라낸다.
    /// 
    /// 예:
    /// nextPhase가 Defense면,
    /// applicablePhase가 Defense인 이벤트만 후보가 된다.
    /// </summary>
    private List<SurpriseEventDefinition> GetCandidates(SurpriseEventPhase phase)
    {
        List<SurpriseEventDefinition> candidates = new();

        foreach (SurpriseEventDefinition definition in eventDefinitions)
        {
            if (definition == null)
            {
                continue;
            }

            if (!definition.enabled)
            {
                continue;
            }

            if (definition.eventId == SurpriseEventId.None)
            {
                continue;
            }

            if (definition.applicablePhase != phase)
            {
                continue;
            }

            candidates.Add(definition);
        }

        return candidates;
    }

    /// <summary>
    /// 이벤트를 실제로 발생시킬지 결정한다.
    /// 
    /// 기본은 20% 확률이지만,
    /// 남은 전환 횟수로 봤을 때 최소 발생 횟수를 채워야 한다면 강제 발생시킨다.
    /// </summary>
    private bool ShouldTriggerEvent(int remainingValidTransitions)
    {
        if (forceEventForTest)
        {
            return true;
        }

        int missingForMinimum = Mathf.Max(0, minEventCount - totalEventCount);

        // 예:
        // 최소 6번은 나와야 하는데 아직 4번만 나왔고,
        // 앞으로 유효 전환이 2번밖에 안 남았다면
        // 남은 2번은 모두 강제 발생해야 최소 6번을 맞출 수 있다.
        if (missingForMinimum > 0 && remainingValidTransitions <= missingForMinimum)
        {
            return true;
        }

        return Random.value < baseTriggerChance;
    }

    // ─────────────────────────────────────────────────────────
    // 2단계: 이벤트 진입
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// 준비된 이벤트를 실제 활성 이벤트로 전환한다.
    /// 
    /// 진입 단계에서는:
    /// - 양쪽 화면에 '이벤트 발생!' 토스트 표시
    /// - 고유 진입 효과음 재생
    /// - 개별 이벤트의 EnterEvent 호출
    /// 을 처리한다.
    /// </summary>
    public void EnterPreparedEvent()
    {
        if (preparedEvent == null || !preparedEvent.IsValid)
        {
            return;
        }

        activeEvent = preparedEvent;
        preparedEvent = null;
        activeEventPhaseBegun = false;

        ShowEventToast();
        SetEventStateDebugText($"진입\n{activeEvent.phase} / P{activeEvent.targetPlayerId}");

        if (definitionMap.TryGetValue(activeEvent.eventId, out SurpriseEventDefinition definition))
        {
            PlayEntrySfx(definition);
        }

        if (handlerMap.TryGetValue(activeEvent.eventId, out ISurpriseEventHandler handler))
        {
            handler.EnterEvent(activeEvent);
        }

        Log($"이벤트 진입 / id:{activeEvent.eventId}");
    }

    // ─────────────────────────────────────────────────────────
    // 3단계: 이벤트 적용 시작
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// 실제 공격/방어 4박 페이즈가 시작될 때 호출한다.
    /// 
    /// 이 시점부터 대상 플레이어에게 이벤트 규칙이 적용된다.
    /// </summary>
    public void BeginActiveEventPhase()
    {
        if (!HasActiveEvent)
        {
            return;
        }

        if (activeEventPhaseBegun)
        {
            return;
        }

        activeEventPhaseBegun = true;

        SetEventStateDebugText($"적용중\n{activeEvent.phase} / P{activeEvent.targetPlayerId}");

        if (handlerMap.TryGetValue(activeEvent.eventId, out ISurpriseEventHandler handler))
        {
            handler.BeginEventPhase(activeEvent);
        }

        Log($"이벤트 적용 시작 / id:{activeEvent.eventId}");
    }

    // ─────────────────────────────────────────────────────────
    // 4단계: 이벤트 복귀
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// 이벤트가 적용된 4박 페이즈가 끝났을 때 호출한다.
    /// 
    /// 여기서 개별 이벤트의 상태를 원래대로 돌리고,
    /// 토스트 UI도 숨긴다.
    /// </summary>
    public void EndActiveEvent()
    {
        if (!HasActiveEvent)
        {
            return;
        }

        // 진입만 된 상태라면 아직 다음 페이즈가 시작되지 않은 것이므로 종료하지 않는다.
        if (!activeEventPhaseBegun)
        {
            return;
        }

        if (handlerMap.TryGetValue(activeEvent.eventId, out ISurpriseEventHandler handler))
        {
            handler.EndEvent(activeEvent);
        }

        HideEventToast();
        ClearEventStateDebugText();

        Log($"이벤트 복귀 / id:{activeEvent.eventId}");

        activeEvent = null;
        activeEventPhaseBegun = false;
    }

    // ─────────────────────────────────────────────────────────
    // 외부 조회용 함수
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// 현재 이벤트가 특정 페이즈/플레이어에게 적용 중인지 확인한다.
    /// 
    /// 예:
    /// DefenseTurn에서 "지금 내 방어에 이벤트가 걸려 있나?"를 확인할 때 사용할 수 있다.
    /// </summary>
    public bool IsActiveFor(SurpriseEventPhase phase, int playerId)
    {
        return HasActiveEvent &&
               activeEvent.phase == phase &&
               activeEvent.targetPlayerId == playerId &&
               activeEventPhaseBegun;
    }

    /// <summary>
    /// 현재 적용 중인 이벤트가 특정 EventId인지 확인한다.
    /// </summary>
    public bool IsActiveEvent(SurpriseEventId eventId)
    {
        return HasActiveEvent &&
               activeEvent.eventId == eventId &&
               activeEventPhaseBegun;
    }

    // ─────────────────────────────────────────────────────────
    // UI / 사운드
    // ─────────────────────────────────────────────────────────

    private void ShowEventToast()
    {
        if (eventToastRoot != null)
        {
            eventToastRoot.SetActive(true);
        }
    }

    private void HideEventToast()
    {
        if (eventToastRoot != null)
        {
            eventToastRoot.SetActive(false);
        }
    }

    /// <summary>
    /// 이벤트 진입 효과음을 재생한다.
    /// 현재는 큰 틀 테스트용으로 AudioSource.PlayClipAtPoint를 사용한다.
    /// 나중에 SoundManager나 전용 AudioSource 방식으로 바꿀 수 있다.
    /// </summary>
    private void PlayEntrySfx(SurpriseEventDefinition definition)
    {
        if (definition == null || definition.entrySfx == null)
        {
            return;
        }

        AudioSource.PlayClipAtPoint(definition.entrySfx, Vector3.zero);
    }

    // ─────────────────────────────────────────────────────────
    // 게임 시작/재시작 초기화
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// 새 게임 시작 시 호출해 이벤트 상태를 초기화한다.
    /// </summary>
    public void ResetForNewGame()
    {
        preparedEvent = null;
        activeEvent = null;
        activeEventPhaseBegun = false;
        totalEventCount = 0;

        HideEventToast();
        ClearEventStateDebugText();
    }

    // ─────────────────────────────────────────────────────────
    // 디버그
    // ─────────────────────────────────────────────────────────

    private void Log(string message)
    {
        if (!logEventFlow)
        {
            return;
        }

        Debug.Log($"SurpriseEventManager: {message}");
    }

    private void SetEventStateDebugText(string message)
    {
        if (!showEventStateDebugText || eventStateDebugText == null)
        {
            return;
        }

        eventStateDebugText.gameObject.SetActive(true);
        eventStateDebugText.text = message;
    }

    private void ClearEventStateDebugText()
    {
        if (eventStateDebugText != null)
        {
            eventStateDebugText.text = "";
        }
    }
}