using System;
using UnityEngine;

// ─────────────────────────────────────────────────────────────
// 기습 이벤트에서 공통으로 쓰는 타입 모음
// ─────────────────────────────────────────────────────────────

/// <summary>
/// 기습 이벤트가 어느 페이즈에 적용되는지 나타낸다.
/// </summary>
public enum SurpriseEventPhase
{
    None,    // 없음 
    Attack,  // 공격 4박 구간에 적용
    Defense  // 방어 4박 구간에 적용
}

/// <summary>
/// 기습 이벤트의 고유 ID.
/// EventID는 "이 이벤트가 정확히 어떤 이벤트인지" 구분하기 위한 값이다.
/// </summary>
public enum SurpriseEventId
{
    None,

    // 공격 페이즈 전용 이벤트
    EVT_ATK_01_IncompleteTransmission, // 불완전 송신
    EVT_ATK_02_PulseInterference,      // 펄스 간섭

    // 방어 페이즈 전용 이벤트
    EVT_DEF_01_GhostSignal,            // 유령 신호
    EVT_DEF_02_AudioReception          // 음향 수신
}

/// <summary>
/// 이벤트 하나의 설정 데이터를 담는 클래스.
/// </summary>
[Serializable]
public class SurpriseEventDefinition
{
    [Tooltip("이벤트 고유 ID. 이벤트를 식별하는 데 사용")]
    public SurpriseEventId eventId;

    [Tooltip("개발자가 인스펙터에서 알아보기 위한 이름. 실제 게임 화면에는 표시하지 않는 것을 기본으로 한다.")]
    public string displayName;

    [Tooltip("이 이벤트가 적용 가능한 페이즈. Attack이면 공격 페이즈 후보, Defense면 방어 페이즈 후보가 된다.")]
    public SurpriseEventPhase applicablePhase;

    [Header("Entry")]
    [Tooltip("이벤트 진입 시 1회 재생할 효과음.")]
    public AudioClip entrySfx;

    [Header("Temporary")]
    [Tooltip("false면 이벤트 풀에서 제외한다. 테스트 중 특정 이벤트를 잠깐 끄고 싶을 때 사용한다.")]
    public bool enabled = true;
}