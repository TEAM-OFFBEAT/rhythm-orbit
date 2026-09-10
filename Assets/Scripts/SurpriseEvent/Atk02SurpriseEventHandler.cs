using System.Collections;
using UnityEngine;

/// <summary>
/// EVT_ATK_02 펄스 간섭 이벤트 핸들러.
/// 공격 레인에 반투명 신호 파동을 주기적으로 통과시켜 시야를 방해한다.
/// 입력 판정·BPM·정신력 규칙은 변경하지 않는다.
/// </summary>
public class Atk02SurpriseEventHandler : MonoBehaviour, ISurpriseEventHandler
{
    [Tooltip("파동이 레인을 가로지르는 시간 (초). 기본: 0.12")]
    [SerializeField] private float waveDuration = 0.12f;

    [Tooltip("파동 발생 간격 (초, 시작→시작 기준). 기본: 0.45")]
    [SerializeField] private float waveInterval = 0.45f;

    [Tooltip("파동 SpriteRenderer. 이 핸들러 GO의 자식에 배치한다.")]
    [SerializeField] private SpriteRenderer waveRenderer;

    [SerializeField] private AttackTurnRenderer attackTurnRenderer;

    private Coroutine entryWaveCoroutine;
    private Coroutine waveLoopCoroutine;
    private AttackSide activeSide;

    private void Awake()
    {
        if (attackTurnRenderer == null)
            attackTurnRenderer = FindObjectOfType<AttackTurnRenderer>();
    }

    public SurpriseEventId EventId => SurpriseEventId.EVT_ATK_02_PulseInterference;

    /// <summary>
    /// 진입 시 신호 파동 1회 통과. 진입 파동은 진행 파동과 별도.
    /// </summary>
    public void EnterEvent(SurpriseEventContext context)
    {
        activeSide = context.targetPlayerId == 1 ? AttackSide.P1 : AttackSide.P2;
    }

    /// <summary>
    /// 공격 턴 시작 시점에 진입 파동 1회 + 반복 루프 시작.
    /// </summary>
    public void BeginEventPhase(SurpriseEventContext context)
    {
        entryWaveCoroutine = StartCoroutine(AnimateSingleWave(activeSide));
        waveLoopCoroutine = StartCoroutine(WaveLoop(activeSide));
    }

    /// <summary>
    /// 페이즈 종료 시 두 코루틴 모두 명시적으로 중단하고 파동 오브젝트 비활성화.
    /// </summary>
    public void EndEvent(SurpriseEventContext context)
    {
        if (entryWaveCoroutine != null)
        {
            StopCoroutine(entryWaveCoroutine);
            entryWaveCoroutine = null;
        }

        if (waveLoopCoroutine != null)
        {
            StopCoroutine(waveLoopCoroutine);
            waveLoopCoroutine = null;
        }

        if (waveRenderer != null)
            waveRenderer.gameObject.SetActive(false);
    }

    private IEnumerator WaveLoop(AttackSide side)
    {
        yield return new WaitForSeconds(waveInterval);
        while (true)
        {
            yield return AnimateSingleWave(side);
            yield return new WaitForSeconds(Mathf.Max(0f, waveInterval - waveDuration));
        }
    }

    private IEnumerator AnimateSingleWave(AttackSide side)
    {
        if (waveRenderer == null || attackTurnRenderer == null) yield break;

        float fromX = attackTurnRenderer.GetStartX(side);
        float toX = attackTurnRenderer.GetEndX(side);

        waveRenderer.gameObject.SetActive(true);

        float elapsed = 0f;
        while (elapsed < waveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / waveDuration);
            Vector3 pos = waveRenderer.transform.position;
            pos.x = Mathf.Lerp(fromX, toX, t);
            waveRenderer.transform.position = pos;
            yield return null;
        }

        waveRenderer.gameObject.SetActive(false);
    }
}
