using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UpgradeMenuUI : MonoBehaviour
{
    [SerializeField] private Transform container;
    [SerializeField] private TMP_Text  coinsLabel;

    // Style
    [SerializeField] private float rowHeight      = 50f;
    [SerializeField] private Color rowColor       = new Color(0.15f, 0.15f, 0.15f, 0.9f);
    [SerializeField] private Color buttonColor    = new Color(0.8f, 0.6f, 0.1f, 1f);
    [SerializeField] private Color buttonMaxColor = new Color(0.3f, 0.3f, 0.3f, 1f);
    [SerializeField] private int   fontSize       = 18;

    private static readonly (string key, string label)[] Stats =
    {
        ("MaxHp",        "HP Max"),
        ("MaxMp",        "MP Max"),
        ("MaxEndurance", "Endurance Max"),
        ("AttackDamage", "Attaque"),
        ("MagicAttack",  "Attaque Magique"),
        ("Defense",      "Défense"),
        ("MagicDefense", "Défense Magique"),
        ("HpRegen",      "Regen HP"),
        ("MpRegen",      "Regen MP"),
        ("AttackRange",  "Portée"),
    };

    private void OnEnable() => Refresh();

    private void Refresh()
    {
        for (int i = container.childCount - 1; i >= 0; i--)
            Destroy(container.GetChild(i).gameObject);

        var mgr   = StatUpgradeManager.Instance;
        var coins = CoinManager.Instance;

        if (coinsLabel != null && coins != null)
            coinsLabel.text = $"MONEY : {coins.TotalCoins}";

        foreach (var (key, label) in Stats)
        {
            int  lvl    = mgr != null ? mgr.GetLevel(key) : 0;
            int  cost   = mgr != null ? mgr.CostForNextLevel(lvl) : 0;
            bool maxed  = lvl >= StatUpgradeManager.MaxLevel;
            bool afford = !maxed && coins != null && coins.TotalCoins >= cost;

            var row = BuildRow(label, lvl, cost, maxed, afford, key);
            row.transform.SetParent(container, false);
        }
    }

    private GameObject BuildRow(string label, int lvl, int cost, bool maxed, bool afford, string key)
    {
        // ── Conteneur de la ligne ────────────────────────────────────────────
        var row = new GameObject($"Row_{key}", typeof(RectTransform));
        var rowRT = row.GetComponent<RectTransform>();
        rowRT.sizeDelta = new Vector2(0, rowHeight);

        var bg = row.AddComponent<Image>();
        bg.color = rowColor;

        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.childControlWidth  = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = true;
        hlg.padding = new RectOffset(8, 8, 4, 4);
        hlg.spacing = 8;

        var le = row.AddComponent<LayoutElement>();
        le.minHeight = rowHeight;
        le.flexibleWidth = 1;

        // ── Nom de la stat ───────────────────────────────────────────────────
        var nameGo  = MakeText($"Name_{key}", label, TextAlignmentOptions.MidlineLeft, 2);
        nameGo.transform.SetParent(row.transform, false);

        // ── Niveau (étoiles) ─────────────────────────────────────────────────
        string stars = $"Lv {lvl} / {StatUpgradeManager.MaxLevel}";
        var lvlGo   = MakeText($"Level_{key}", stars, TextAlignmentOptions.Center, 1);
        lvlGo.transform.SetParent(row.transform, false);

        // ── Coût ─────────────────────────────────────────────────────────────
        string costTxt = maxed ? "MAX" : $"{cost} coins";
        var costGo  = MakeText($"Cost_{key}", costTxt, TextAlignmentOptions.Center, 1);
        costGo.transform.SetParent(row.transform, false);

        // ── Bouton + ─────────────────────────────────────────────────────────
        var btnGo = new GameObject($"Btn_{key}", typeof(RectTransform));
        var btnLE = btnGo.AddComponent<LayoutElement>();
        btnLE.minWidth = 40;
        btnLE.preferredWidth = 40;

        var btnImg = btnGo.AddComponent<Image>();
        btnImg.color = (maxed || !afford) ? buttonMaxColor : buttonColor;

        var btn = btnGo.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        btn.interactable  = !maxed && afford;

        var btnLabel = new GameObject("Label", typeof(RectTransform));
        btnLabel.transform.SetParent(btnGo.transform, false);
        var btnRT = btnLabel.GetComponent<RectTransform>();
        btnRT.anchorMin = Vector2.zero;
        btnRT.anchorMax = Vector2.one;
        btnRT.sizeDelta = Vector2.zero;
        var btnTxt = btnLabel.AddComponent<TextMeshProUGUI>();
        btnTxt.text      = maxed ? "✓" : "+";
        btnTxt.fontSize  = fontSize + 4;
        btnTxt.alignment = TextAlignmentOptions.Center;
        btnTxt.color     = Color.white;

        string k = key;
        btn.onClick.AddListener(() =>
        {
            StatUpgradeManager.Instance?.TryUpgrade(k);
            Refresh();
        });

        btnGo.transform.SetParent(row.transform, false);

        return row;
    }

    private GameObject MakeText(string goName, string text, TextAlignmentOptions align, float flexWidth)
    {
        var go = new GameObject(goName, typeof(RectTransform));
        var le = go.AddComponent<LayoutElement>();
        le.flexibleWidth = flexWidth;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.alignment = align;
        tmp.color     = Color.white;
        return go;
    }
}
