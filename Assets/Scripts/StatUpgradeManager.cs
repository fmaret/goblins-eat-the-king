using UnityEngine;

/// <summary>
/// Gère les niveaux d'upgrade des stats (5 niveaux max par stat).
/// Persiste via PlayerPrefs. Indépendant du réseau — chaque joueur a ses propres upgrades.
/// </summary>
public class StatUpgradeManager : MonoBehaviour
{
    public static StatUpgradeManager Instance => FindFirstObjectByType<StatUpgradeManager>();

    public const int MaxLevel = 5;

    // Coût en pièces pour passer au niveau suivant (index = niveau actuel 0→4)
    private static readonly int[] LevelCosts = { 10, 25, 50, 100, 200 };

    // Bonus FLAT ajouté par niveau pour chaque stat
    private static readonly float[] BonusMaxHp             = { 10, 20, 35, 50, 75 };
    private static readonly float[] BonusMaxMp             = { 5,  10, 20, 35, 50 };
    private static readonly float[] BonusMaxEndurance      = { 10, 20, 30, 45, 60 };
    private static readonly float[] BonusAttackDamage      = { 3,  6,  10, 15, 22 };
    private static readonly float[] BonusMagicAttack       = { 2,  5,   8, 12, 18 };
    private static readonly float[] BonusDefense           = { 1,  2,   4,  7, 10 };
    private static readonly float[] BonusMagicDefense      = { 1,  2,   3,  5,  8 };
    private static readonly float[] BonusHpRegen           = { 0.5f, 1, 2, 3, 5 };
    private static readonly float[] BonusMpRegen           = { 0.5f, 1, 2, 3, 5 };
    private static readonly float[] BonusAttackRange       = { 0.05f, 0.1f, 0.15f, 0.2f, 0.3f };

    // Niveaux actuels
    public int LvlMaxHp        { get; private set; }
    public int LvlMaxMp        { get; private set; }
    public int LvlMaxEndurance { get; private set; }
    public int LvlAttackDamage { get; private set; }
    public int LvlMagicAttack  { get; private set; }
    public int LvlDefense      { get; private set; }
    public int LvlMagicDefense { get; private set; }
    public int LvlHpRegen      { get; private set; }
    public int LvlMpRegen      { get; private set; }
    public int LvlAttackRange  { get; private set; }

    private void Awake() => Load();

    // ── Coût ─────────────────────────────────────────────────────────────────
    public int CostForNextLevel(int currentLevel)
        => currentLevel < MaxLevel ? LevelCosts[currentLevel] : int.MaxValue;

    // ── Upgrade ───────────────────────────────────────────────────────────────
    public bool TryUpgrade(string statKey)
    {
        int current = GetLevel(statKey);
        if (current >= MaxLevel) return false;

        int cost = CostForNextLevel(current);
        if (CoinManager.Instance == null || CoinManager.Instance.TotalCoins < cost) return false;

        CoinManager.Instance.Spend(cost);
        SetLevel(statKey, current + 1);
        Save();
        return true;
    }

    // ── Bonus totaux calculés ─────────────────────────────────────────────────
    public float GetMaxHpBonus()        => Sum(BonusMaxHp,        LvlMaxHp);
    public float GetMaxMpBonus()        => Sum(BonusMaxMp,        LvlMaxMp);
    public float GetMaxEnduranceBonus() => Sum(BonusMaxEndurance,  LvlMaxEndurance);
    public float GetAttackDamageBonus() => Sum(BonusAttackDamage,  LvlAttackDamage);
    public float GetMagicAttackBonus()  => Sum(BonusMagicAttack,   LvlMagicAttack);
    public float GetDefenseBonus()      => Sum(BonusDefense,       LvlDefense);
    public float GetMagicDefenseBonus() => Sum(BonusMagicDefense,  LvlMagicDefense);
    public float GetHpRegenBonus()      => Sum(BonusHpRegen,       LvlHpRegen);
    public float GetMpRegenBonus()      => Sum(BonusMpRegen,       LvlMpRegen);
    public float GetAttackRangeBonus()  => Sum(BonusAttackRange,   LvlAttackRange);

    private static float Sum(float[] table, int level)
    {
        float total = 0f;
        for (int i = 0; i < level; i++) total += table[i];
        return total;
    }

    // ── Helpers get/set par clé ───────────────────────────────────────────────
    public int GetLevel(string key) => key switch
    {
        "MaxHp"        => LvlMaxHp,
        "MaxMp"        => LvlMaxMp,
        "MaxEndurance" => LvlMaxEndurance,
        "AttackDamage" => LvlAttackDamage,
        "MagicAttack"  => LvlMagicAttack,
        "Defense"      => LvlDefense,
        "MagicDefense" => LvlMagicDefense,
        "HpRegen"      => LvlHpRegen,
        "MpRegen"      => LvlMpRegen,
        "AttackRange"  => LvlAttackRange,
        _ => 0
    };

    private void SetLevel(string key, int value)
    {
        switch (key)
        {
            case "MaxHp":        LvlMaxHp        = value; break;
            case "MaxMp":        LvlMaxMp        = value; break;
            case "MaxEndurance": LvlMaxEndurance = value; break;
            case "AttackDamage": LvlAttackDamage = value; break;
            case "MagicAttack":  LvlMagicAttack  = value; break;
            case "Defense":      LvlDefense      = value; break;
            case "MagicDefense": LvlMagicDefense = value; break;
            case "HpRegen":      LvlHpRegen      = value; break;
            case "MpRegen":      LvlMpRegen      = value; break;
            case "AttackRange":  LvlAttackRange  = value; break;
        }
    }

    // ── Save / Load ───────────────────────────────────────────────────────────
    public void Save()
    {
        PlayerPrefs.SetInt("upg_MaxHp",        LvlMaxHp);
        PlayerPrefs.SetInt("upg_MaxMp",        LvlMaxMp);
        PlayerPrefs.SetInt("upg_MaxEndurance", LvlMaxEndurance);
        PlayerPrefs.SetInt("upg_AttackDamage", LvlAttackDamage);
        PlayerPrefs.SetInt("upg_MagicAttack",  LvlMagicAttack);
        PlayerPrefs.SetInt("upg_Defense",      LvlDefense);
        PlayerPrefs.SetInt("upg_MagicDefense", LvlMagicDefense);
        PlayerPrefs.SetInt("upg_HpRegen",      LvlHpRegen);
        PlayerPrefs.SetInt("upg_MpRegen",      LvlMpRegen);
        PlayerPrefs.SetInt("upg_AttackRange",  LvlAttackRange);
        PlayerPrefs.Save();
    }

    private void Load()
    {
        LvlMaxHp        = PlayerPrefs.GetInt("upg_MaxHp",        0);
        LvlMaxMp        = PlayerPrefs.GetInt("upg_MaxMp",        0);
        LvlMaxEndurance = PlayerPrefs.GetInt("upg_MaxEndurance",  0);
        LvlAttackDamage = PlayerPrefs.GetInt("upg_AttackDamage",  0);
        LvlMagicAttack  = PlayerPrefs.GetInt("upg_MagicAttack",   0);
        LvlDefense      = PlayerPrefs.GetInt("upg_Defense",       0);
        LvlMagicDefense = PlayerPrefs.GetInt("upg_MagicDefense",  0);
        LvlHpRegen      = PlayerPrefs.GetInt("upg_HpRegen",       0);
        LvlMpRegen      = PlayerPrefs.GetInt("upg_MpRegen",       0);
        LvlAttackRange  = PlayerPrefs.GetInt("upg_AttackRange",   0);
    }
}
