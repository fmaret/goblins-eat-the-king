using System;
using System.Collections.Generic;
using Goblins.Data;
using UnityEngine;

namespace Goblins.Localization
{
    [Serializable]
    public class LocalizationEntry
    {
        public string key;
        public string value;
    }

    [Serializable]
    public class LocalizationData
    {
        public LocalizationEntry[] entries;
    }

    public class LocalizationManager : MonoBehaviour
    {
        public static LocalizationManager Instance { get; private set; }

        /// <summary>Fired after the language is changed and the dictionary is reloaded.</summary>
        public event Action OnLanguageChanged;

        public string language = "fr"; // default to French for quick dev

        private Dictionary<string, string> dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        void Awake()
        {
            if (Instance != null && Instance != this) Destroy(this.gameObject);
            Instance = this;
            DontDestroyOnLoad(this.gameObject);
            LoadFromResources(language);
        }

        public void LoadFromResources(string lang)
        {
            dict.Clear();
            if (string.IsNullOrEmpty(lang)) lang = "fr";
            language = lang;

            try
            {
                // Resources/Localization/localization_{lang}.json
                var path = $"Localization/localization_{language}";
                var ta = Resources.Load<TextAsset>(path);
                if (ta != null)
                {
                    var data = JsonUtility.FromJson<LocalizationData>(ta.text);
                    if (data != null && data.entries != null)
                    {
                        foreach (var e in data.entries)
                        {
                            if (string.IsNullOrEmpty(e?.key)) continue;
                            dict[e.key] = e.value ?? string.Empty;
                        }
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Localization load error for '{language}': {e.Message}");
            }

            // Les fichiers JSON sont la source de vérité — on ne met pas de fallback hardcodé.
            Debug.LogWarning($"[Localization] Could not load 'Resources/Localization/localization_{language}.json'");
        }

        public string Translate(string key, params object[] args)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            string val;
            if (dict.TryGetValue(key, out val))
            {
                return args != null && args.Length > 0 ? string.Format(val, args) : val;
            }
            return args != null && args.Length > 0 ? string.Format(key, args) : key;
        }

        public string TranslateType(PowerupType type)
        {
            return Translate($"TYPE_{type}");
        }

        public string TranslateStat(StatType stat)
        {
            var key = stat.ToString();
            return Translate(key);
        }

        // public API to change language at runtime
        public void SetLanguage(string lang)
        {
            LoadFromResources(lang);
            Debug.Log($"[Localization] Language set to '{language}' ({dict.Count} entries)");
            OnLanguageChanged?.Invoke();
        }
    }
}
