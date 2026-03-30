using UnityEngine;
using UnityEngine.Events;

public class BudgetManager : MonoBehaviour
{
    public static BudgetManager Instance { get; private set; }

    [Header("Starting Balance")]
    [Min(0)] public float startingBalance = 1000f;

    public UnityEvent<float> onBalanceChanged = new UnityEvent<float>();

    private const string SAVE_KEY = "PlayerBudget";
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

    public void ResetBudget()
    {
        PlayerPrefs.DeleteKey(SAVE_KEY);
        SetBalance(startingBalance);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Load();
    }

    private void Save()
    {
        PlayerPrefs.SetFloat(SAVE_KEY, _balance);
        PlayerPrefs.Save();
    }

    private void Load()
    {
        _balance = PlayerPrefs.HasKey(SAVE_KEY)
            ? PlayerPrefs.GetFloat(SAVE_KEY)
            : startingBalance;
        onBalanceChanged.Invoke(_balance);
    }
}