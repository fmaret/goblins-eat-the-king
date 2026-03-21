using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LobbyPlayerSelection : MonoBehaviour
{
    public int index;
    public TextMeshProUGUI nameLabel;
    public Image colorImage;

    public void SetIndex(int i)
    {
        index = i;
    }

    public void SetName(string name)
    {
        if (nameLabel != null) nameLabel.text = name;
    }

    public string GetName()
    {
        return nameLabel != null ? nameLabel.text : "";
    }

    public void SetColor(Color c)
    {
        if (colorImage != null) colorImage.color = c;
    }

    public Color GetColor()
    {
        return colorImage != null ? colorImage.color : Color.white;
    }

    // optional onChanged event placeholder
    public System.Action onChanged;

    // wire up UI callbacks if needed
}
