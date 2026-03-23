using UnityEngine;
using TMPro;

public class DisplayStats : MonoBehaviour
{
    public static DisplayStats Instance { get; private set; }
    [SerializeField] private TextMeshProUGUI textAttackDamage;
    [SerializeField] private TextMeshProUGUI textMagicAttackDamage;
    [SerializeField] private TextMeshProUGUI textDefense;
    [SerializeField] private TextMeshProUGUI textMagicDefense;
    [SerializeField] private TextMeshProUGUI textSpeed;
    [SerializeField] private TextMeshProUGUI textCriticalChance;
    [SerializeField] private TextMeshProUGUI textLifeSteal;
    [SerializeField] private TextMeshProUGUI textManaSteal;
    [SerializeField] private TextMeshProUGUI textEnduranceSteal;
    [SerializeField] private TextMeshProUGUI textHpRegeneration;
    [SerializeField] private TextMeshProUGUI textMpRegeneration;
    [SerializeField] private TextMeshProUGUI textEnduranceRegeneration;
    [SerializeField] private TextMeshProUGUI textDodgeChance;

    void Awake()
    {
        Instance = this;
        SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    [Header("References")]
    [SerializeField] private GameObject statsPanel; // panel to display stats
    // No prefab required: create a TextMeshPro (3D) object at runtime

    public bool IsOpen => statsPanel != null && statsPanel.activeSelf;

    public void SetActive(bool active)
    {
        if (this != null && statsPanel != null)
            statsPanel.SetActive(active);
    }

    public void DisplayPlayerStats(PlayerController playerController)
    {
        Debug.Log("DisplayStats: Displaying player stats for player " + playerController?.name);
        if (playerController == null)
        {
            Debug.LogWarning("DisplayStats: no player controller assigned");
            return;
        }
        // if (statsPanel != null) {
        //     for (int i = statsPanel.transform.childCount - 1; i >= 0; i--)
        //         Destroy(statsPanel.transform.GetChild(i).gameObject);
        // }
        // var stats = playerController.GetStats();
        // Debug.Log("DisplayStats: Player stats: " + stats);
        // var go = new GameObject("StatsText");
        // if (statsPanel != null)
        //     go.transform.SetParent(statsPanel.transform, false);
        // var tmp = go.AddComponent<TextMeshProUGUI>();
        // tmp.fontSize = 30;
        // tmp.text = stats;
        textAttackDamage.text = playerController.AttackDamage.ToString("0.##");
        textMagicAttackDamage.text = playerController.MagicAttackDamage.ToString();
        textDefense.text = playerController.Defense.ToString("0.##");
        textMagicDefense.text = playerController.MagicDefense.ToString();
        textSpeed.text = playerController.MoveSpeed.ToString("0.##");
        textCriticalChance.text = playerController.CriticalRate.ToString("P0");
        textLifeSteal.text = playerController.LifeSteal.ToString("0.##");
        textManaSteal.text = playerController.ManaSteal.ToString();
        // textEnduranceSteal.text = playerController.EnduranceSteal.ToString();
        // textHpRegeneration.text = playerController.HpRegeneration.ToString();
        textMpRegeneration.text = playerController.MpRegeneration.ToString();
        textEnduranceRegeneration.text = playerController.EnduranceRegeneration.ToString();
        textDodgeChance.text = playerController.DodgeRate.ToString("P0");
    }
}