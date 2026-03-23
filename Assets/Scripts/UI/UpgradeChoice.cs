using System.Collections.Generic;
using UnityEngine;
using Goblins.Data;

public class UpgradeChoice : MonoBehaviour
{
    public static UpgradeChoice Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this.gameObject);
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void SetActive(bool active)
    {
        if (this != null && this.gameObject != null)
            this.gameObject.SetActive(active);
    }

    [Header("References")]
    [SerializeField] private GameObject itemPrefab; // prefab with UpgradeChoiceItem
    [SerializeField] private Transform container; // horizontal layout container
    [SerializeField] private PowerupDatabase database;
    [SerializeField] private int choiceCount = 3;

    public void Start()
    {
        SetActive(false);
    }

    public void ClearChoices()
    {
        if (container == null) return;
        for (int i = container.childCount - 1; i >= 0; i--)
            Destroy(container.GetChild(i).gameObject);
    }

    public void GenerateNewChoices()
    {
        ClearChoices();

        if (database == null || database.entries == null || database.entries.Count == 0)
        {
            Debug.LogWarning("UpgradeChoice: no database assigned or empty");
            return;
        }

        var entries = database.entries;
        var rnd = new System.Random();
        var upgrades = new List<PowerupDefinition>();
        var downgrades = new List<PowerupDefinition>();
        foreach (var e in entries)
        {
            if (e.type == PowerupType.UPGRADE) upgrades.Add(e);
            else if (e.type == PowerupType.DOWNGRADE) downgrades.Add(e);
        }

        if (upgrades.Count == 0 || downgrades.Count == 0)
        {
            Debug.LogWarning("UpgradeChoice: not enough upgrade or downgrade entries in database");
            return;
        }

        // shuffle lists
        for (int i = 0; i < upgrades.Count; i++) { int j = rnd.Next(i, upgrades.Count); var tmp = upgrades[i]; upgrades[i] = upgrades[j]; upgrades[j] = tmp; }
        for (int i = 0; i < downgrades.Count; i++) { int j = rnd.Next(i, downgrades.Count); var tmp = downgrades[i]; downgrades[i] = downgrades[j]; downgrades[j] = tmp; }

        for (int i = 0; i < choiceCount; i++)
        {
            var up = upgrades[i % upgrades.Count];
            var down = downgrades[i % downgrades.Count];

            var go = Instantiate(itemPrefab, container);
            go.SetActive(true);
            var rt = go.GetComponent<RectTransform>(); if (rt != null) rt.localScale = Vector3.one;
            var item = go.GetComponent<UpgradeChoiceItem>();
            if (item != null)
            {
                // create Powerup instances (randomized values) and pass them
                var upPower = new Goblins.Data.Powerup(up);
                var downPower = new Goblins.Data.Powerup(down);

                // assign random target indices for upgrade and downgrade separately
                // 0 = everyone, otherwise 1..N mapping to connected clients
                int upTarget = 0, downTarget = 0;
                if (Unity.Netcode.NetworkManager.Singleton != null)
                {
                    var clients = Unity.Netcode.NetworkManager.Singleton.ConnectedClientsList;
                    int count = clients != null ? clients.Count : 0;
                    if (count > 0)
                    {
                        var rndLocal = new System.Random();

                        // probability to pick 'everyone' (0)
                        double upgradeEveryoneProb = 0.10; // rarer for bonuses
                        double downgradeEveryoneProb = 0.50; // more frequent for maluses

                        // build player index pool (1..count)
                        var players = new List<int>();
                        for (int p = 1; p <= count; p++) players.Add(p);

                        int required = choiceCount * 2; // we need two targets per choice
                        var picks = new List<int>();
                        var available = new List<int>(players);

                        for (int k = 0; k < required; k++)
                        {
                            bool isUpgradePick = (k % 2 == 0);
                            double prob = isUpgradePick ? upgradeEveryoneProb : downgradeEveryoneProb;

                            if (rndLocal.NextDouble() < prob)
                            {
                                picks.Add(0);
                            }
                            else
                            {
                                if (available.Count == 0)
                                    available = new List<int>(players); // refill if exhausted

                                int idx = rndLocal.Next(0, available.Count);
                                picks.Add(available[idx]);
                                available.RemoveAt(idx);
                            }
                        }

                        // take the two picks for this item
                        int baseIdx = i * 2;
                        if (baseIdx < picks.Count)
                        {
                            upTarget = picks[baseIdx];
                            if (baseIdx + 1 < picks.Count) downTarget = picks[baseIdx + 1];
                        }

                        // ensure upgrade and downgrade targets are not the same non-zero player
                        if (upTarget != 0 && upTarget == downTarget)
                        {
                            // try to find an unused player
                            int alt = -1;
                            foreach (var p in players)
                            {
                                if (p != upTarget && !picks.Contains(p)) { alt = p; break; }
                            }
                            if (alt == -1)
                            {
                                // as fallback pick any different player
                                if (players.Count > 1)
                                {
                                    do { alt = players[rndLocal.Next(players.Count)]; } while (alt == upTarget);
                                }
                            }
                            if (alt != -1) downTarget = alt;
                        }
                    }
                }

                upPower.targetPlayerIndex = upTarget;
                downPower.targetPlayerIndex = downTarget;

                // capture local vars for callback
                var u = upPower; var d = downPower;
                item.Configure($"Choix {i + 1}", u, d, () => OnChoiceSelected(u, d));
            }
            else
            {
                Debug.LogWarning("UpgradeChoice: item prefab missing UpgradeChoiceItem component");
                Destroy(go);
            }
        }
    }

    private void OnChoiceSelected(Goblins.Data.Powerup upgrade, Goblins.Data.Powerup downgrade)
    {
        Debug.Log($"UpgradeChoice: Selected upgrade {upgrade?.definition?.stats} ({upgrade?.value:0.##}) + downgrade {downgrade?.definition?.stats} ({downgrade?.value:0.##}) target={upgrade?.targetPlayerIndex}");
        // try to request server to apply the powerups via the local player's PlayerController
        try
        {
            if (Unity.Netcode.NetworkManager.Singleton != null)
            {
                var local = Unity.Netcode.NetworkManager.Singleton.LocalClient;
                if (local != null && local.PlayerObject != null)
                {
                    var pc = local.PlayerObject.GetComponent<PlayerController>();
                    if (pc != null)
                    {
                        if (upgrade != null && upgrade.definition != null)
                        {
                            pc.RequestApplyPowerupServerRpc((int)upgrade.definition.stat, upgrade.value, upgrade.targetPlayerIndex, true);
                        }
                        if (downgrade != null && downgrade.definition != null)
                        {
                            pc.RequestApplyPowerupServerRpc((int)downgrade.definition.stat, downgrade.value, downgrade.targetPlayerIndex, false);
                        }
                    }
                    else Debug.LogWarning("UpgradeChoice: local player PlayerController not found");
                }
                else Debug.LogWarning("UpgradeChoice: local client/playerobject not available");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("UpgradeChoice: failed to request apply powerups: " + ex.Message);
        }
        ClearChoices();
        SetActive(false);
    }
}
