using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using Goblins.Lobby;

public class PlayButtonsGroup : MonoBehaviour
{
    public Button hostButton;
    public Button joinButton;
    public GameObject lobbyPanel; // panel to show when hosting
    public GameObject joinModal; // modal to show when joining

    void Awake()
    {
        if (hostButton == null)
            hostButton = transform.Find("HostButton")?.GetComponent<Button>();
        if (joinButton == null)
            joinButton = transform.Find("JoinButton")?.GetComponent<Button>();
        
        Debug.Log($"PlayButtonsGroup.Awake: hostButton={(hostButton!=null)}, joinButton={(joinButton!=null)}");

        if (hostButton != null)
        {
            hostButton.onClick.RemoveAllListeners();
            hostButton.onClick.AddListener(OnHostClicked);
            Debug.Log("PlayButtonsGroup: hooked hostButton listener");
        }
        if (joinButton != null)
        {
            joinButton.onClick.RemoveAllListeners();
            joinButton.onClick.AddListener(OnJoinClicked);
            Debug.Log("PlayButtonsGroup: hooked joinButton listener");
        }
        Debug.Log($"PlayButtonsGroup.Awake: lobbyPanel={(lobbyPanel!=null)}, joinModal={(joinModal!=null)}");
    }

    void Start() {
    }

    void OnHostClicked()
    {
        Debug.Log("PlayButtonsGroup: Host clicked");
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            Debug.Log($"PlayButtonsGroup: NetworkManager present. IsServer={NetworkManager.Singleton.IsServer}, IsClient={NetworkManager.Singleton.IsClient}");
            var netConfig = NetworkManager.Singleton.NetworkConfig;
            Debug.Log($"PlayButtonsGroup: NetworkTransport={(netConfig != null && netConfig.NetworkTransport != null)}; PlayerPrefab={(netConfig != null && netConfig.PlayerPrefab != null)}");
            Debug.Log($"PlayButtonsGroup: StartHost called. IsServer={NetworkManager.Singleton.IsServer}, IsClient={NetworkManager.Singleton.IsClient}");
        }
        else
        {
            Debug.Log("PlayButtonsGroup: NetworkManager not available or already running");
        }
        LobbyManager.Instance?.CreateLobby();
        Debug.Log("Lobbypanel: " + (lobbyPanel != null ? lobbyPanel.name : "null"));
        if (lobbyPanel != null) {
            lobbyPanel.SetActive(true);
            Debug.Log("Lobby panel set active");
            Debug.Log($"lobbyPanel.activeSelf={lobbyPanel.activeSelf}, activeInHierarchy={lobbyPanel.activeInHierarchy}");
            Debug.Log($"lobbyPanel parent={(lobbyPanel.transform.parent!=null ? lobbyPanel.transform.parent.name : "(root)")}");
        }
        Debug.Log($"PlayButtonsGroup: LobbyManager.Instance present={(LobbyManager.Instance!=null)}");
        Debug.Log($"NM null? {NetworkManager.Singleton == null}");
        Debug.Log($"IsServer: {NetworkManager.Singleton?.IsServer}");
        Debug.Log($"IsClient: {NetworkManager.Singleton?.IsClient}");

        gameObject.SetActive(false);
    }

    void OnJoinClicked()
    {
        Debug.Log("PlayButtonsGroup: Join clicked");
        if (joinModal != null) joinModal.SetActive(true);
        gameObject.SetActive(false);
    }
}