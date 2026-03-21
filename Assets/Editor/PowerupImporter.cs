using System.IO;
using UnityEngine;
using UnityEditor;
using Goblins.Data;

public class PowerupImporter
{
    private const string JsonPath = "Assets/Data/powerups.json";
    private const string AssetPath = "Assets/Data/PowerupDatabase.asset";

    [MenuItem("Tools/Import Powerups JSON")]
    public static void Import()
    {
        if (!File.Exists(JsonPath))
        {
            Debug.LogError($"Powerup JSON not found at {JsonPath}");
            return;
        }

        string raw = File.ReadAllText(JsonPath);
        // JsonUtility cannot parse a root array, so wrap it into an object
        string wrapped = "{\"items\":" + raw + "}";

        var wrapper = JsonUtility.FromJson<PowerupListWrapper>(wrapped);
        if (wrapper == null || wrapper.items == null)
        {
            Debug.LogError("Failed to parse powerups JSON");
            return;
        }

        PowerupDatabase db = AssetDatabase.LoadAssetAtPath<PowerupDatabase>(AssetPath);
        if (db == null)
        {
            db = ScriptableObject.CreateInstance<PowerupDatabase>();
            AssetDatabase.CreateAsset(db, AssetPath);
            Debug.Log($"Created new PowerupDatabase at {AssetPath}");
        }

        db.entries.Clear();
        foreach (var rawEntry in wrapper.items)
        {
            var entry = new PowerupDefinition();
            // parse type
            if (System.Enum.TryParse<PowerupType>(rawEntry.type, true, out var ptype))
                entry.type = ptype;
            else
                entry.type = PowerupType.UPGRADE;

            entry.stats = rawEntry.stats;
            if (System.Enum.TryParse<StatType>(rawEntry.stats, true, out var stat))
                entry.stat = stat;
            else
                entry.stat = StatType.HP;

            entry.minValue = rawEntry.minValue;
            entry.maxValue = rawEntry.maxValue;
            db.entries.Add(entry);
        }

        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        Debug.Log($"Imported {db.entries.Count} powerups into {AssetPath}");
    }

    [System.Serializable]
    private class RawPowerup
    {
        public string type;
        public string stats;
        public float minValue;
        public float maxValue;
    }

    [System.Serializable]
    private class PowerupListWrapper
    {
        public RawPowerup[] items;
    }
}
