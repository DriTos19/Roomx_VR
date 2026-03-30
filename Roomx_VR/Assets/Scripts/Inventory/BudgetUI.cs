using UnityEngine;
using TMPro;

public class BudgetUI : MonoBehaviour
{
    public TMP_Text balanceText;
    public string prefix = "$";

    void Start()
    {
        BudgetManager.Instance.onBalanceChanged.AddListener(UpdateDisplay);
        UpdateDisplay(BudgetManager.Instance.Balance);
    }

    void OnDestroy()
    {
        if (BudgetManager.Instance != null)
            BudgetManager.Instance.onBalanceChanged.RemoveListener(UpdateDisplay);
    }

    private void UpdateDisplay(float balance)
    {
        if (balanceText != null)
            balanceText.text = $"{prefix}{balance:F2}";
    }
}