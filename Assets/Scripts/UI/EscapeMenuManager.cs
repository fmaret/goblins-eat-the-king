using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using TMPro;
using Goblins.Localization;

public class EscapeMenuManager : MonoBehaviour
{
    public static EscapeMenuManager Instance { get; private set; }

    [Tooltip("Assign the UI panel for the escape menu in the Inspector")]
    public GameObject menuPanel;

    [Header("Buttons (assign in Inspector)")]
    public Button resumeButton;
    public Button optionsButton;
    public Button mainMenuButton;
    public Button quitButton;

    [Header("Scenes")]
    public string mainMenuSceneName = "MainMenu";

    public bool IsOpen { get; private set; }

    public event Action<bool> OnToggled;

    void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this.gameObject);
        Instance = this;
    }

    void Start()
    {
        if (menuPanel != null) menuPanel.SetActive(false);
        IsOpen = false;
        if (resumeButton    != null) { resumeButton.onClick.RemoveAllListeners();    resumeButton.onClick.AddListener(Close); }
        if (optionsButton   != null) { optionsButton.onClick.RemoveAllListeners();   optionsButton.onClick.AddListener(OnOptionsClicked); }
        if (mainMenuButton  != null) { mainMenuButton.onClick.RemoveAllListeners();  mainMenuButton.onClick.AddListener(GoToMainMenu); }
        if (quitButton      != null) { quitButton.onClick.RemoveAllListeners();      quitButton.onClick.AddListener(QuitGame); }

        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged += LocalizeUI;
        LocalizeUI();
    }

    void OnDestroy()
    {
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged -= LocalizeUI;
    }

    void LocalizeUI()
    {
        var lm = LocalizationManager.Instance;
        if (lm == null) return;
        SetButtonText(resumeButton,   lm.Translate("Resume"));
        SetButtonText(optionsButton,  lm.Translate("Options"));
        SetButtonText(mainMenuButton, lm.Translate("MainMenu"));
        SetButtonText(quitButton,     lm.Translate("Quit"));
    }

    static void SetButtonText(Button btn, string text)
    {
        if (btn == null) return;
        var tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null) tmp.text = text;
    }

    public void Open()
    {
        if (menuPanel != null) menuPanel.SetActive(true);
        IsOpen = true;
        OnToggled?.Invoke(true);
    }

    public void Close()
    {
        if (menuPanel != null) menuPanel.SetActive(false);
        IsOpen = false;
        OnToggled?.Invoke(false);
    }

    public void Toggle()
    {
        if (IsOpen) Close(); else Open();
    }

    private void OnOptionsClicked()
    {
        Debug.Log("EscapeMenu: Options clicked (not implemented)");
    }

    private void GoToMainMenu()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();
        if (CoinManager.Instance != null)
            CoinManager.Instance.gameObject.SetActive(false);
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void QuitGame()
    {
        Debug.Log("EscapeMenu: Quit requested");
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
