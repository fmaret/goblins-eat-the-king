using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Goblins.Data;

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
            if (upgradeTitleLabel != null) { upgradeTitleLabel.gameObject.SetActive(true); upgradeTitleLabel.text = upgradePowerup.GetTitle(); }
            if (upgradeDescriptionLabel != null) { upgradeDescriptionLabel.gameObject.SetActive(true); upgradeDescriptionLabel.text = upgradePowerup.GetDescription(); }
        }
        else
        {
            if (upgradeTitleLabel != null) upgradeTitleLabel.gameObject.SetActive(false);
            if (upgradeDescriptionLabel != null) upgradeDescriptionLabel.gameObject.SetActive(false);
        }

        // populate downgrade fields
        if (downgradePowerup != null)
        {
            if (downgradeTitleLabel != null) { downgradeTitleLabel.gameObject.SetActive(true); downgradeTitleLabel.text = downgradePowerup.GetTitle(); }
            if (downgradeDescriptionLabel != null) { downgradeDescriptionLabel.gameObject.SetActive(true); downgradeDescriptionLabel.text = downgradePowerup.GetDescription(); }
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

    public Powerup GetUpgrade() => upgradePowerup;
    public Powerup GetDowngrade() => downgradePowerup;
}
