using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Goblins.Data;
using Goblins.Localization;

public class UpgradeChoiceItem : MonoBehaviour
{
    [Header("Upgrade UI")]
    [SerializeField] private TextMeshProUGUI upgradeTitleLabel;
    [SerializeField] private TextMeshProUGUI upgradeDescriptionLabel;
    [Header("Downgrade UI")]
    [SerializeField] private TextMeshProUGUI downgradeTitleLabel;
    [SerializeField] private TextMeshProUGUI downgradeDescriptionLabel;
    [SerializeField] private Button selectButton;

    private Powerup upgradePowerup;
    private Powerup downgradePowerup;
    private Action onSelected;

    public void Configure(Powerup upgrade, Powerup downgrade, Action onSelect)
    {
        upgradePowerup = upgrade;
        downgradePowerup = downgrade;
        onSelected = onSelect;

        // populate upgrade fields
        if (upgradePowerup != null)
        {
            if (upgradeTitleLabel != null) { upgradeTitleLabel.gameObject.SetActive(true); upgradeTitleLabel.text = GetPowerupTitleText(upgradePowerup); }
            if (upgradeDescriptionLabel != null) { upgradeDescriptionLabel.gameObject.SetActive(true); upgradeDescriptionLabel.text = GetPowerupDescriptionText(upgradePowerup, true); }
        }
        else
        {
            if (upgradeTitleLabel != null) upgradeTitleLabel.gameObject.SetActive(false);
            if (upgradeDescriptionLabel != null) upgradeDescriptionLabel.gameObject.SetActive(false);
        }

        // populate downgrade fields
        if (downgradePowerup != null)
        {
            if (downgradeTitleLabel != null) { downgradeTitleLabel.gameObject.SetActive(true); downgradeTitleLabel.text = GetPowerupTitleText(downgradePowerup); }
            if (downgradeDescriptionLabel != null) { downgradeDescriptionLabel.gameObject.SetActive(true); downgradeDescriptionLabel.text = GetPowerupDescriptionText(downgradePowerup, false); }
        }
        else
        {
            if (downgradeTitleLabel != null) downgradeTitleLabel.gameObject.SetActive(false);
            if (downgradeDescriptionLabel != null) downgradeDescriptionLabel.gameObject.SetActive(false);
        }

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() => onSelected?.Invoke());
        }
    }

    private string GetPowerupTitleText(Powerup p)
    {
        if (p == null || p.definition == null) return "";
        if (LocalizationManager.Instance != null)
        {
            var typeText = LocalizationManager.Instance.TranslateType(p.definition.type);
            var statText = LocalizationManager.Instance.TranslateStat(p.definition.stat);
            // if translation didn't resolve (returns enum name), try the raw stats key as a fallback
            if (string.Equals(statText, p.definition.stat.ToString(), System.StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(p.definition.stats))
            {
                var alt = LocalizationManager.Instance.Translate(p.definition.stats);
                if (!string.IsNullOrEmpty(alt)) statText = alt;
            }
            return $"{typeText} {statText}";
        }
        // fallback
        return p.definition.type == PowerupType.UPGRADE ? $"+ {p.definition.stats}" : $"- {p.definition.stats}";
    }

    private string GetPowerupDescriptionText(Powerup p, bool isUpgrade)
    {
        if (p == null || p.definition == null) return "";
        // targetPlayerIndex: 0 = everyone, otherwise 1-based index into ConnectedClientsList
        int target = p.targetPlayerIndex;
        float delta = p.value;

        // if everyone, show difference like "+5 (Tout le monde)"
        if (target == 0)
        {
            string sign = isUpgrade ? "+" : "-";
            string allText = LocalizationManager.Instance != null ? LocalizationManager.Instance.Translate("AllPlayers") : "Tout le monde";
            return $"{sign}{FormatStatValue(p.definition.stat, delta)} ({allText})";
        }

        // try to find target client and player object
        if (Unity.Netcode.NetworkManager.Singleton != null)
        {
            var clients = Unity.Netcode.NetworkManager.Singleton.ConnectedClientsList;
            int count = clients != null ? clients.Count : 0;
            if (target >= 1 && target <= count)
            {
                var client = clients[target - 1];
                var po = client.PlayerObject;
                if (po != null)
                {
                    var pc = po.GetComponent<PlayerController>();
                    if (pc != null)
                    {
                        // read current stat and compute new
                        float current = ReadStatFromPlayer(pc, p.definition.stat);
                        float newVal = ComputeNewStatValue(p.definition.stat, current, delta, isUpgrade);
                        string playerLabel = LocalizationManager.Instance != null ? LocalizationManager.Instance.Translate("PlayerFormat", client.ClientId) : $"(Joueur {client.ClientId})";
                        // format as "80 -> 85 (Joueur 1)"
                        return $"{FormatStatValueForDisplay(p.definition.stat, current)} -> {FormatStatValueForDisplay(p.definition.stat, newVal)} {playerLabel}";
                    }
                }
            }
        }

        // fallback to simple description (Powerup has its own localized description)
        return p.GetDescription();
    }

    private float ReadStatFromPlayer(PlayerController pc, StatType stat)
    {
        try
        {
            return pc.GetStatValue(stat);
        }
        catch
        {
            return 0f;
        }
    }

    private float ComputeNewStatValue(StatType stat, float current, float delta, bool isUpgrade)
    {
        float sign = isUpgrade ? 1f : -1f;
        // for HP/MP/ENDURANCE, treat delta as absolute change
        return current + sign * delta;
    }

    private string FormatStatValue(StatType stat, float v)
    {
        // used for showing differences
        if (stat == StatType.HP || stat == StatType.MP || stat == StatType.ENDURANCE)
            return Mathf.RoundToInt(v).ToString();
        if (stat == StatType.LIFESTEAL || stat == StatType.MANASTEAL || stat == StatType.ENDURANCESTEAL)
            return (v * 100f).ToString("0") + "%";
        return v.ToString("0.##");
    }

    private string FormatStatValueForDisplay(StatType stat, float v)
    {
        if (stat == StatType.HP || stat == StatType.MP || stat == StatType.ENDURANCE)
            return Mathf.CeilToInt(v).ToString();
        if (stat == StatType.LIFESTEAL || stat == StatType.MANASTEAL || stat == StatType.ENDURANCESTEAL)
            return (v * 100f).ToString("0") + "%";
        return v.ToString("0.##");
    }

    private string StatTypeToFrench(StatType stat)
    {
        // replaced by LocalizationManager.TranslateStat
        return stat.ToString();
    }

    public Powerup GetUpgrade() => upgradePowerup;
    public Powerup GetDowngrade() => downgradePowerup;
}
