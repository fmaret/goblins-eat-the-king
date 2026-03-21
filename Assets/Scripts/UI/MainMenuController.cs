using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Unity.Netcode;
using Goblins.Localization;

public class MainMenuController : MonoBehaviour
{
    [Header("Optional references - will try to find by name if empty")]
    public Button playButton;
    public Button optionsButton;
    public Button upgradesButton;
    public Button quitButton;

    public GameObject optionsPanel;
    public GameObject playButtonsGroup; // should be placed to the right of Play button in scene
    public GameObject lobbyPanel; // optional: assign your Lobby panel here
    public GameObject joinModal; // optional: assign your Join modal here

    void Awake()
    {
        // try to find buttons by name if not assigned
        if (playButton == null) playButton = FindButton("PlayButton");
        if (optionsButton == null) optionsButton = FindButton("OptionsButton");
        if (upgradesButton == null) upgradesButton = FindButton("UpgradesButton");
        if (quitButton == null) quitButton = FindButton("QuitButton");

        // try to find options panel
        if (optionsPanel == null)
        {
            var go = GameObject.Find("OptionsPanel");
            if (go != null) optionsPanel = go;
        }

        LocalizeButtons();
        HookButtons();
        // try to find play buttons group
        if (playButtonsGroup == null)
        {
            var go = GameObject.Find("PlayButtonsGroup");
            if (go != null) playButtonsGroup = go;
        }

        if (lobbyPanel == null)
        {
            var go = GameObject.Find("LobbyPanel");
            if (go != null) lobbyPanel = go;
        }

        if (joinModal == null)
        {
            var go = GameObject.Find("JoinModal");
            if (go != null) joinModal = go;
        }

        if (playButtonsGroup != null) playButtonsGroup.SetActive(false);
        if (lobbyPanel != null) lobbyPanel.SetActive(false);
        if (joinModal != null) joinModal.SetActive(false);
    }

    Button FindButton(string name)
    {
        var go = GameObject.Find(name);
        if (go == null) return null;
        return go.GetComponent<Button>();
    }

    void LocalizeButtons()
    {
        try
        {
            var lm = LocalizationManager.Instance;
            if (lm == null) return;

            SetButtonText(playButton, lm.Translate("Play"));
            SetButtonText(optionsButton, lm.Translate("Options"));
            SetButtonText(upgradesButton, lm.Translate("Upgrades"));
            SetButtonText(quitButton, lm.Translate("Quit"));
        }
        catch (Exception e)
        {
            Debug.LogWarning("MainMenuController localization error: " + e.Message);
        }
    }

    void SetButtonText(Button b, string text)
    {
        if (b == null) return;
        // try TextMeshPro first
        var tmp = b.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null) { tmp.text = text; return; }
        var t = b.GetComponentInChildren<Text>();
        if (t != null) t.text = text;
    }

    void HookButtons()
    {
        if (playButton != null)
        {
            playButton.onClick.RemoveAllListeners();
            playButton.onClick.AddListener(OnPlayClicked);
        }
        if (optionsButton != null)
        {
            optionsButton.onClick.RemoveAllListeners();
            optionsButton.onClick.AddListener(OnOptionsClicked);
        }
        if (upgradesButton != null)
        {
            upgradesButton.onClick.RemoveAllListeners();
            upgradesButton.onClick.AddListener(OnUpgradesClicked);
        }
        if (quitButton != null)
        {
            quitButton.onClick.RemoveAllListeners();
            quitButton.onClick.AddListener(OnQuitClicked);
        }
    }

    void OnPlayClicked()
    {
        // show play buttons (Host / Join) next to Play if group exists
        Debug.Log("Play clicked");
        Debug.Log("playButtonsGroup: " + (playButtonsGroup != null ? playButtonsGroup.name : "null"));
        if (playButtonsGroup != null)
        {
            playButtonsGroup.SetActive(!playButtonsGroup.activeSelf);
        }
    }

    // Called by Join button inside playButtonsGroup
    public void OnPlayJoinClicked()
    {
        if (joinModal != null)
        {
            joinModal.SetActive(true);
        }
        if (playButtonsGroup != null) playButtonsGroup.SetActive(false);
    }

    void OnOptionsClicked()
    {
        if (optionsPanel != null)
        {
            optionsPanel.SetActive(!optionsPanel.activeSelf);
        }
        else
        {
            Debug.Log("MainMenu: OptionsPanel not found");
        }
    }

    void OnUpgradesClicked()
    {
        var ucType = Type.GetType("UpgradeChoice");
        if (ucType != null)
        {
            // try to access singleton
            try
            {
                var prop = ucType.GetProperty("Instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                var inst = prop?.GetValue(null);
                if (inst != null)
                {
                    var setActive = ucType.GetMethod("SetActive");
                    var gen = ucType.GetMethod("GenerateNewChoices");
                    setActive?.Invoke(inst, new object[] { true });
                    gen?.Invoke(inst, null);
                    return;
                }
            }
            catch { }
        }
        // fallback: try GameObject.Find
        var go = GameObject.Find("UpgradeChoice");
        if (go != null)
        {
            go.SetActive(true);
            var comp = go.GetComponentInChildren<UnityEngine.MonoBehaviour>();
            if (comp != null)
            {
                var m = comp.GetType().GetMethod("GenerateNewChoices");
                m?.Invoke(comp, null);
            }
            return;
        }

        Debug.LogWarning("MainMenu: UpgradeChoice not found");
    }

    void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
