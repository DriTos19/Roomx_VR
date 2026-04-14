using UnityEngine;
using UnityEngine.Events;

public class BudgetManager : MonoBehaviour
{
    public static BudgetManager Instance { get; private set; }

    public UnityEvent<float> onBalanceChanged = new UnityEvent<float>();

    private const string SAVE_KEY = "PlayerBudget";
    private const string BUDGET_SET_KEY = "BudgetHasBeenSet";
    private float _balance;

    public float Balance => _balance;

    public bool CanAfford(float cost) => _balance >= cost;

    public bool TrySpend(float cost)
    {
        if (!CanAfford(cost)) return false;
        SetBalance(_balance - cost);
        return true;
    }

    public void AddFunds(float amount)
    {
        if (amount <= 0) return;
        SetBalance(_balance + amount);
    }

    public void SetBalance(float newBalance)
    {
        _balance = Mathf.Max(0, newBalance);
        Save();
        onBalanceChanged.Invoke(_balance);
    }

    public void InitialiseWithAmount(float amount)
    {
        if (PlayerPrefs.HasKey(BUDGET_SET_KEY)) return;
        SetBalance(amount);
        PlayerPrefs.SetInt(BUDGET_SET_KEY, 1);
        PlayerPrefs.Save();
    }

    public void ResetBudget()
    {
        PlayerPrefs.DeleteKey(SAVE_KEY);
        PlayerPrefs.DeleteKey(BUDGET_SET_KEY);
        Load();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

#if UNITY_EDITOR
        PlayerPrefs.DeleteAll();
#endif

        Load();
    }

    private void Save()
    {
        PlayerPrefs.SetFloat(SAVE_KEY, _balance);
        PlayerPrefs.Save();
    }

    private void Load()
    {
        if (PlayerPrefs.HasKey(SAVE_KEY))
            _balance = PlayerPrefs.GetFloat(SAVE_KEY);
        else
            _balance = 1000f;
        onBalanceChanged.Invoke(_balance);
    }
}