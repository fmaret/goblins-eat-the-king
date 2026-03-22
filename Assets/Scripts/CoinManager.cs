using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class CoinManager : MonoBehaviour
{
    public static CoinManager Instance => FindFirstObjectByType<CoinManager>();

    private const string SaveKey = "TotalCoins";

    [Header("UI")]
    [SerializeField] private TMP_Text coinText;

    public int TotalCoins { get; private set; }
    public int SessionCoins { get; private set; }

    private void Awake()
    {
        TotalCoins = PlayerPrefs.GetInt(SaveKey, 0);
        RefreshUI();
    }

    private void Update()
    {
        if (Keyboard.current.lKey.wasPressedThisFrame)
            Save();
    }

    public void Spend(int amount)
    {
        TotalCoins = Mathf.Max(0, TotalCoins - amount);
        RefreshUI();
        Save();
    }

    public void AddCoin(int amount = 1)
    {
        SessionCoins += amount;
        TotalCoins   += amount;
        RefreshUI();
    }

    public void Save()
    {
        PlayerPrefs.SetInt(SaveKey, TotalCoins);
        PlayerPrefs.Save();
        Debug.Log($"[CoinManager] Sauvegardé : {TotalCoins} pièces");
    }

    private void RefreshUI()
    {
        if (coinText != null)
            coinText.text = $"MONEY : {TotalCoins}";
    }

    // Appelé en fin de partie pour sauvegarder automatiquement
    private void OnApplicationQuit() => Save();
}
