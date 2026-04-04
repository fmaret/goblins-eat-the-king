using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Goblins.Localization;

/// <summary>
/// Contrôleur du panneau Options.
/// Gère : volume master / musique / SFX et langue.
/// Les réglages sont persistés via PlayerPrefs et appliqués au démarrage.
/// </summary>
public class OptionsMenuController : MonoBehaviour
{
    // ── PlayerPrefs keys ─────────────────────────────────────────────────────
    public const string KEY_MASTER = "MasterVolume";
    public const string KEY_MUSIC  = "MusicVolume";
    public const string KEY_SFX    = "SfxVolume";
    public const string KEY_LANG   = "Language";

    // ── Références UI — Sliders ───────────────────────────────────────────────
    [Header("Son — sliders")]
    public Slider masterSlider;
    public Slider musicSlider;
    public Slider sfxSlider;

    [Header("Son — labels valeur (%)")]
    public TextMeshProUGUI masterLabel;
    public TextMeshProUGUI musicLabel;
    public TextMeshProUGUI sfxLabel;

    [Header("Son — labels de ligne (nom)")]
    [Tooltip("TextMeshPro affichant 'Son global', 'Musique', etc.")]
    public TextMeshProUGUI masterRowLabel;
    public TextMeshProUGUI musicRowLabel;
    public TextMeshProUGUI sfxRowLabel;

    // ── Références UI — Langue ────────────────────────────────────────────────
    [Header("Langue")]
    [Tooltip("Dropdown dont les options correspondent à l'ordre de LanguageCodes[]")]
    public TMP_Dropdown languageDropdown;
    public TextMeshProUGUI languageRowLabel;

    // ── Références UI — Divers ────────────────────────────────────────────────
    [Header("Divers")]
    [Tooltip("Titre du panneau Options")]
    public TextMeshProUGUI titleLabel;
    public Button closeButton;

    // ── Codes de langue (ordre = index dropdown) ──────────────────────────────
    private static readonly string[] LanguageCodes = { "fr", "en" };

    // ── État interne ──────────────────────────────────────────────────────────
    private string _currentLanguage = "fr";
    private bool _refreshingDropdown; // guard pour éviter le callback lors du refresh programmatique

    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>Applique les réglages sauvegardés. À appeler dans MainMenuController.Start().</summary>
    public static void ApplySavedSettings()
    {
        AudioListener.volume = PlayerPrefs.GetFloat(KEY_MASTER, 1f);

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.SetMusicVolume(PlayerPrefs.GetFloat(KEY_MUSIC, 0.6f));
            SoundManager.Instance.SetSfxVolume(PlayerPrefs.GetFloat(KEY_SFX, 1f));
        }

        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.SetLanguage(PlayerPrefs.GetString(KEY_LANG, "fr"));
    }

    // ── Unity lifecycle ───────────────────────────────────────────────────────
    void Awake()
    {
        // Câblage dans Awake pour que les listeners soient prêts avant OnEnable
        if (masterSlider != null) masterSlider.onValueChanged.AddListener(OnMasterChanged);
        if (musicSlider  != null) musicSlider.onValueChanged.AddListener(OnMusicChanged);
        if (sfxSlider    != null) sfxSlider.onValueChanged.AddListener(OnSfxChanged);
        if (languageDropdown != null) languageDropdown.onValueChanged.AddListener(OnLanguageDropdownChanged);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
    }

    void OnEnable()
    {
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged += LocalizeUI;
        RefreshUI();
    }

    void OnDisable()
    {
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged -= LocalizeUI;
    }

    // ── UI refresh ────────────────────────────────────────────────────────────
    void RefreshUI()
    {
        _currentLanguage = PlayerPrefs.GetString(KEY_LANG, "fr");

        if (masterSlider != null) masterSlider.value = PlayerPrefs.GetFloat(KEY_MASTER, 1f);
        if (musicSlider  != null) musicSlider.value  = PlayerPrefs.GetFloat(KEY_MUSIC,  0.6f);
        if (sfxSlider    != null) sfxSlider.value    = PlayerPrefs.GetFloat(KEY_SFX,    1f);

        UpdateValueLabel(masterLabel, masterSlider);
        UpdateValueLabel(musicLabel,  musicSlider);
        UpdateValueLabel(sfxLabel,    sfxSlider);

        _refreshingDropdown = true;
        if (languageDropdown != null)
        {
            int idx = System.Array.IndexOf(LanguageCodes, _currentLanguage);
            languageDropdown.value = idx >= 0 ? idx : 0;
        }
        _refreshingDropdown = false;

        LocalizeUI();
    }

    void LocalizeUI()
    {
        var lm = LocalizationManager.Instance;
        if (lm == null) return;

        if (titleLabel       != null) titleLabel.text       = lm.Translate("OptionsTitle");
        if (masterRowLabel   != null) masterRowLabel.text   = lm.Translate("MasterVolume");
        if (musicRowLabel    != null) musicRowLabel.text    = lm.Translate("Music");
        if (sfxRowLabel      != null) sfxRowLabel.text      = lm.Translate("SFX");
        if (languageRowLabel != null) languageRowLabel.text = lm.Translate("Language");
        if (closeButton != null)
        {
            var tmp = closeButton.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null) tmp.text = lm.Translate("Close");
        }
    }

    static void UpdateValueLabel(TextMeshProUGUI label, Slider slider)
    {
        if (label == null || slider == null) return;
        label.text = Mathf.RoundToInt(slider.value * 100f) + " %";
    }

    // ── Callbacks ─────────────────────────────────────────────────────────────
    void OnMasterChanged(float v)
    {
        AudioListener.volume = v;
        PlayerPrefs.SetFloat(KEY_MASTER, v);
        UpdateValueLabel(masterLabel, masterSlider);
    }

    void OnMusicChanged(float v)
    {
        SoundManager.Instance?.SetMusicVolume(v);
        PlayerPrefs.SetFloat(KEY_MUSIC, v);
        UpdateValueLabel(musicLabel, musicSlider);
    }

    void OnSfxChanged(float v)
    {
        SoundManager.Instance?.SetSfxVolume(v);
        PlayerPrefs.SetFloat(KEY_SFX, v);
        UpdateValueLabel(sfxLabel, sfxSlider);
    }

    void OnLanguageDropdownChanged(int index)
    {
        if (_refreshingDropdown) return; // changement programmatique — on ignore
        if (index < 0 || index >= LanguageCodes.Length) return;
        string lang = LanguageCodes[index];
        if (_currentLanguage == lang) return;
        _currentLanguage = lang;
        PlayerPrefs.SetString(KEY_LANG, lang);
        LocalizationManager.Instance?.SetLanguage(lang); // déclenche OnLanguageChanged → LocalizeUI sur tous les abonnés
    }

    void Close()
    {
        PlayerPrefs.Save();
        gameObject.SetActive(false);
    }
}
