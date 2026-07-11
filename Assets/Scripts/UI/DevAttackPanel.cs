using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DevAttackPanel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button startP1AttackButton;
    [SerializeField] private Button startP2AttackButton;
    [SerializeField] private TMP_Text attackStatusLabel;
    [SerializeField] private TMP_Text attackPenaltyLabel;
    [SerializeField] private AttackTurnRenderer attackTurnRenderer;
    [SerializeField] private AttackTurn attackTurn;
    
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

        if (attackTurn != null)
        {
            attackTurn.UnregisterView(attackTurnRenderer, attackStatusLabel, attackPenaltyLabel);
        }
    }

    /// <summary>
    /// DevAttackPanel이 가진 공격 테스트 UI를 AttackTurn에 등록.
    /// </summary>
    private void RegisterViewToAttackTurn()
    {
        if (attackTurn == null)
        {
            Debug.LogWarning("AttackTurn이 연결되지 않았습니다.");
            return;
        }

        attackTurn.RegisterView(attackTurnRenderer, attackStatusLabel, attackPenaltyLabel);
    }

    /// <summary>
    /// P1 공격 테스트 버튼 클릭 시 AttackTurn의 P1 공격을 시작.
    /// </summary>
    private void OnStartP1Attack()
    {
        if (attackTurn == null)
        {
            Debug.LogWarning("AttackTurn이 연결되지 않았습니다.");
            return;
        }

        attackTurn.StartLocalPlayerAttack();
    }

    /// <summary>
    /// P2 공격 데모 버튼 클릭 시 AttackTurn의 P2 공격 데모를 시작.
    /// </summary>
    private void OnStartP2Attack()
    {
        if (attackTurn == null)
        {
            Debug.LogWarning("AttackTurn이 연결되지 않았습니다.");
            return;
        }

        attackTurn.StartOpponentAttackDemo();
    }
}