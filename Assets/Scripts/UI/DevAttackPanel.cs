using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DevAttackPanel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button startP1AttackButton;
    [SerializeField] private Button startP2AttackButton;
    [SerializeField] private TMP_Text attackStatusLabel;
    [SerializeField] private AttackTurnRenderer attackTurnRenderer;

    private void Awake()
    {
        if (startP1AttackButton != null)
        {
            startP1AttackButton.onClick.AddListener(OnStartP1Attack);
        }

        if (startP2AttackButton != null)
        {
            startP2AttackButton.onClick.AddListener(OnStartP2Attack);
        }
    }

    private void Start()
    {
        RegisterViewToAttackTurn();
    }

    private void OnDestroy()
    {
        if (startP1AttackButton != null)
        {
            startP1AttackButton.onClick.RemoveListener(OnStartP1Attack);
        }

        if (startP2AttackButton != null)
        {
            startP2AttackButton.onClick.RemoveListener(OnStartP2Attack);
        }

        if (AttackTurn.Instance != null)
        {
            AttackTurn.Instance.UnregisterView(attackTurnRenderer, attackStatusLabel);
        }
    }

    /// <summary>
    /// DevAttackPanel이 가진 공격 테스트 UI를 AttackTurn에 등록.
    /// </summary>
    private void RegisterViewToAttackTurn()
    {
        if (AttackTurn.Instance == null)
        {
            Debug.LogWarning("AttackTurn.Instance가 없습니다.");
            return;
        }

        AttackTurn.Instance.RegisterView(attackTurnRenderer, attackStatusLabel);
    }

    /// <summary>
    /// P1 공격 테스트 버튼 클릭 시 AttackTurn의 P1 공격을 시작.
    /// </summary>
    private void OnStartP1Attack()
    {
        if (AttackTurn.Instance == null)
        {
            Debug.LogWarning("AttackTurn.Instance가 없습니다.");
            return;
        }

        AttackTurn.Instance.StartLocalPlayerAttack();
    }

    /// <summary>
    /// P2 공격 데모 버튼 클릭 시 AttackTurn의 P2 공격 데모를 시작.
    /// </summary>
    private void OnStartP2Attack()
    {
        if (AttackTurn.Instance == null)
        {
            Debug.LogWarning("AttackTurn.Instance가 없습니다.");
            return;
        }

        AttackTurn.Instance.StartOpponentAttackDemo();
    }
}