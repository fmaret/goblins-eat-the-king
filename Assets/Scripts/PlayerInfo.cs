using UnityEngine;
using TMPro;

public class PlayerInfo : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameLabel;
    [SerializeField] private StatBar healthBar;

    public void SetName(string name)
    {
        if (nameLabel != null) nameLabel.text = name;
    }

    public void SetHealth(float current, float max, string text = null)
    {
        if (healthBar != null) healthBar.Set(current, max, text);
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }
}
