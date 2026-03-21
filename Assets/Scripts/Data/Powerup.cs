using UnityEngine;
using Unity.Netcode;
using System;
using Goblins.Localization;

namespace Goblins.Data
{
    [Serializable]
    public class Powerup
    {
        public PowerupDefinition definition;
        public float value;
        // 0 means everyone, otherwise 1-based index of the target player in the connected clients list
        public int targetPlayerIndex = 0;

        public Powerup(PowerupDefinition def)
        {
            definition = def;
            if (def != null)
                value = UnityEngine.Random.Range(def.minValue, def.maxValue);
            else
                value = 0f;
        }

        public string GetTitle()
        {
            if (definition == null) return "";
            // use localization if available
            try
            {
                if (LocalizationManager.Instance != null)
                {
                    var typeText = LocalizationManager.Instance.TranslateType(definition.type);
                    var statText = LocalizationManager.Instance.TranslateStat(definition.stat);
                    return $"{typeText} {statText}";
                }
            }
            catch { }
            return $"{definition.type} {definition.stat}";
        }

        public string GetDescription()
        {
            if (definition == null) return "";
            string targetDesc;
            if (targetPlayerIndex == 0)
            {
                if (LocalizationManager.Instance != null) targetDesc = LocalizationManager.Instance.Translate("AllPlayers");
                else targetDesc = "All Players";
            }
            else
            {
                if (NetworkManager.Singleton != null)
                {
                    var clients = NetworkManager.Singleton.ConnectedClientsList;
                    int count = clients != null ? clients.Count : 0;
                    if (targetPlayerIndex >= 1 && targetPlayerIndex <= count)
                    {
                        var client = clients[targetPlayerIndex - 1];
                        if (LocalizationManager.Instance != null) targetDesc = LocalizationManager.Instance.Translate("PlayerFormat", client.ClientId);
                        else targetDesc = $"Player {client.ClientId}";
                    }
                    else
                    {
                        targetDesc = $"PlayerIndex {targetPlayerIndex}";
                    }
                }
                else
                {
                    targetDesc = $"PlayerIndex {targetPlayerIndex}";
                }
            }

            // translate stat name when possible
            string statText = definition.stats;
            try
            {
                if (LocalizationManager.Instance != null)
                {
                    statText = LocalizationManager.Instance.TranslateStat(definition.stat);
                }
            }
            catch { }

            return $"{statText} — {value:0.##} ({targetDesc})";
        }
    }
}
