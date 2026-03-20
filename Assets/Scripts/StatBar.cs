using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StatBar : MonoBehaviour
{
    [Header("Références UI")]
    [SerializeField] private Slider slider;
    [SerializeField] private Image fill;
    [SerializeField] private TextMeshProUGUI label;

    [Header("Paramètres")]
    public string text = "HP";
    public Color color = Color.red;
    [Range(0f, 1f)] public float value = 1f;

    void Start()
    {
        UpdateUI();
    }

    public void Set(float current, float max, string newText = null, Color? newColor = null)
    {
        value = Mathf.Clamp01(current / max);
        if (newText != null) text = newText;
        if (newColor.HasValue) color = newColor.Value;
        UpdateUI();
    }

    private void UpdateUI()
    {
        if (slider != null)
            slider.value = value;

        if (fill != null)
            fill.color = color;

        if (label != null)
            label.text = text;
    }
}
