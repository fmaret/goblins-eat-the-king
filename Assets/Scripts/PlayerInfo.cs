using UnityEngine;
using TMPro;

public class PlayerInfo : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameLabel;
    [SerializeField] private StatBar healthBar;
    [SerializeField] private StatBar manaBar;
    [SerializeField] private StatBar enduranceBar;

    public void SetName(string name)
    {
        if (nameLabel != null) nameLabel.text = name;
    }

    public void SetHealth(float current, float max, string text = null)
    {
        if (healthBar != null) healthBar.Set(current, max, text);
    }

    public void SetMana(float current, float max, string text = null)
    {
        if (manaBar != null) manaBar.Set(current, max, text);
    }

    public void SetEndurance(float current, float max, string text = null)
    {
        if (enduranceBar != null) enduranceBar.Set(current, max, text);
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }
}
