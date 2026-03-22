using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using TMPro;

using Unity.Netcode;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

namespace Goblins.Lobby
{
    [Serializable]
    public class LobbyPlayer
    {
        public string name;
        public Color color;
    }

    public class LobbyManager : NetworkBehaviour
    {
        public static LobbyManager Instance { get; private set; }

        [Header("Networking")]
        public bool useRelay = true;

        [Header("Game")]
        public string gameSceneName = "NGO_MapGenerator";

        public string lobbyCode { get; private set; }
        public bool isHost { get; private set; }
        public List<LobbyPlayer> players = new List<LobbyPlayer>();

        public event Action OnPlayersChanged;

        [Serializable]
        public class LobbyState
        {
            public string code;
            public List<LobbyPlayer> players;
        }

        [SerializeField] private RectTransform playersContainer;
        public GameObject LobbyPlayerSelectionPrefab;
        public TMP_Text lobbyCodeLabel;
        public Button startGameButton;

        void Awake()
        {
            Instance = this;
            Debug.Log("[Lobby] Awake: LobbyManager instance assigned.");
        }

        void Start() {
            Debug.Log("[Lobby] Start: LobbyManager Start() called. active=" + gameObject.activeSelf + " enabled=" + enabled);
            // this.gameObject.SetActive(false);
            if (startGameButton != null)
            {
                startGameButton.onClick.AddListener(OnStartGameButtonPressed);
                startGameButton.interactable = false; // only host can start
            }
            // Network spawn handling will trigger RequestLobbyState for clients
        }

        private bool hasSentPlayerInfo = false;

        public override void OnNetworkSpawn()
        {
            Debug.Log($"[Lobby] OnNetworkSpawn called. IsClient={IsClient} IsServer={IsServer} OwnerClientId={OwnerClientId}");
            base.OnNetworkSpawn();

            if (IsClient && !IsServer)
            {
                RequestLobbyStateServerRpc();

                // ✅ AJOUT ICI
                if (!hasSentPlayerInfo)
                {
                    hasSentPlayerInfo = true;

                    var color = Color.white; // ou stocke le choix du joueur ailleurs
                    RegisterPlayerInfoServerRpc("Player", color.r, color.g, color.b, color.a);
                }
            }

            if (IsServer)
            {
                BroadcastLobbyState();
            }

            // Subscribe to low-level network connect/disconnect callbacks (server-side)
            if (NetworkManager.Singleton != null && IsServer)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
                Debug.Log("[Lobby] Subscribed to NetworkManager connection callbacks.");
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
                Debug.Log("[Lobby] Unsubscribed from NetworkManager connection callbacks.");
            }
        }

        private void OnClientConnected(ulong clientId)
        {
            Debug.Log($"[Lobby] Network client connected: {clientId}");
        }

        private void OnClientDisconnected(ulong clientId)
        {
            Debug.Log($"[Lobby] Network client disconnected: {clientId}");
        }

        public async void CreateLobby()
        {
            // Try Relay first if configured
            if (useRelay)
            {
                try
                {
                    await StartHostRelayAsync();
                    return;
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning("StartHostRelayAsync failed, falling back to local lobby: " + ex.Message);
                    // fall through to local creation
                }
            }

            // local fallback (or when useRelay is false)
            isHost = true;
            lobbyCode = GenerateCode();
            lobbyCodeLabel.text = "Code : " + lobbyCode;
            players.Clear();
            Debug.Log($"Lobby created with code {lobbyCode}, and players count {players.Count}");
            // add host as first player placeholder
            players.Add(new LobbyPlayer { name = "Host", color = Color.white });
            OnPlayersChanged?.Invoke();
            // broadcast state if we're server
            if (IsServer) BroadcastLobbyState();
            this.UpdateUI();
            // register in-editor so virtual players can join when using Multiplayer Play Mode
#if UNITY_EDITOR
            EditorLocalLobbyRegistry.RegisterHost(lobbyCode, players);
#endif
        }

        // Start a relay-backed host and set the lobby code
        public async Task StartHostRelayAsync()
        {
            try
            {
                await UnityServices.InitializeAsync();
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();

                Allocation alloc = await RelayService.Instance.CreateAllocationAsync(1);
                string joinCode = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);
                Debug.Log($"Relay allocation created. Join code: {joinCode}");
                var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                transport.SetHostRelayData(
                    alloc.RelayServer.IpV4,
                    (ushort)alloc.RelayServer.Port,
                    alloc.AllocationIdBytes,
                    alloc.Key,
                    alloc.ConnectionData
                );

                // set local lobby state
                isHost = true;
                lobbyCode = joinCode;
                if (lobbyCodeLabel != null) lobbyCodeLabel.text = "Code : " + lobbyCode;
                players.Clear();
                players.Add(new LobbyPlayer { name = "Host", color = Color.white });
                OnPlayersChanged?.Invoke();
                UpdateUI();

                if (startGameButton != null) startGameButton.interactable = true;

                NetworkManager.Singleton.StartHost();

                // as host/server, broadcast state
                if (IsServer) BroadcastLobbyState();
            }
            catch (Exception ex)
            {
                Debug.LogError("StartHostRelayAsync failed: " + ex);
                throw;
            }
        }

        // Join a relay-backed host using its join code. Returns true on success.
        public async Task<bool> StartClientRelayAsync(string code)
        {
            try
            {
                Debug.Log($"Attempting to join Relay lobby with code {code}...");
                await UnityServices.InitializeAsync();
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log("Authenticated with Unity Services. Joining Relay allocation...");
                JoinAllocation alloc = await RelayService.Instance.JoinAllocationAsync(code);
                Debug.Log($"Joined Relay allocation. Server IP: {alloc.RelayServer.IpV4}, Port: {alloc.RelayServer.Port}");
                var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                transport.SetClientRelayData(
                    alloc.RelayServer.IpV4,
                    (ushort)alloc.RelayServer.Port,
                    alloc.AllocationIdBytes,
                    alloc.Key,
                    alloc.ConnectionData,
                    alloc.HostConnectionData
                );
                Debug.Log("Configured UnityTransport with Relay data, starting client...");
                // set local lobby state
                isHost = false;
                lobbyCode = code;
                OnPlayersChanged?.Invoke();
                Debug.Log($"Set local lobby code to {lobbyCode}. Starting client...");
                if (startGameButton != null) startGameButton.interactable = false;

                NetworkManager.Singleton.StartClient();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError("StartClientRelayAsync failed: " + ex);
                return false;
            }
        }

        public string GenerateCode()
        {
            var rnd = new System.Random();
            int code = rnd.Next(100000, 999999);
            return code.ToString();
        }

        // local-logic join (works if host exists in same process). Returns true if successful
        public bool JoinLobby(string code, Color color)
        {
            // First try the editor-local registry (used by virtual editor windows)
            #if UNITY_EDITOR
            if (EditorLocalLobbyRegistry.TryJoin(code, new LobbyPlayer { name = "Player", color = color }))
            {
                // mirror registry state locally
                lobbyCode = EditorLocalLobbyRegistry.LobbyCode;
                players = EditorLocalLobbyRegistry.Players;
                isHost = false;
                OnPlayersChanged?.Invoke();
                UpdateUI();
                return true;
            }
            #endif

            if (string.IsNullOrEmpty(lobbyCode)) return false;
            if (code != lobbyCode) return false;
            players.Add(new LobbyPlayer { name = "Player", color = color });
            OnPlayersChanged?.Invoke();
            if (IsServer) BroadcastLobbyState();
            return true;
        }

        public void SetLocalPlayerInfo(int index, string name, Color color)
        {
            if (index >= 0 && index < players.Count)
            {
                // If we're a client, ask server to update the player info for this client
                if (IsClient && !IsServer)
                {
                    RegisterPlayerInfoServerRpc(name, color.r, color.g, color.b, color.a);
                    return;
                }

                players[index].name = name;
                players[index].color = color;
                OnPlayersChanged?.Invoke();
                #if UNITY_EDITOR
                EditorLocalLobbyRegistry.UpdatePlayer(index, name, color);
                #endif
                if (IsServer) BroadcastLobbyState();
            }
        }

        // Allow editor registry to push state into the LobbyManager without accessing private setters/events
        public void ApplyRegistryState(string code, List<LobbyPlayer> playersFromRegistry)
        {
            lobbyCode = code;
            players = playersFromRegistry ?? new List<LobbyPlayer>();
            OnPlayersChanged?.Invoke();
            UpdateUI();
        }

        // ServerRpc called by clients to register their player (name + color)
        [ServerRpc(RequireOwnership = false)]
        public void RegisterPlayerInfoServerRpc(string name, float r, float g, float b, float a, ServerRpcParams rpcParams = default)
        {
            var col = new Color(r, g, b, a);
            Debug.Log($"RegisterPlayerInfoServerRpc from client {rpcParams.Receive.SenderClientId}: name={name} color={col}");
            players.Add(new LobbyPlayer { name = name, color = col });
            OnPlayersChanged?.Invoke();
            BroadcastLobbyState();
        }

        // Client requests the current lobby state from server
        [ServerRpc(RequireOwnership = false)]
        public void RequestLobbyStateServerRpc(ServerRpcParams rpcParams = default)
        {
            Debug.Log($"RequestLobbyStateServerRpc from client {rpcParams.Receive.SenderClientId}");
            var state = new LobbyState { code = lobbyCode, players = players };
            string json = JsonUtility.ToJson(state);
            var clientParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { rpcParams.Receive.SenderClientId } }
            };
            UpdateClientLobbyStateClientRpc(json, clientParams);
        }

        // Server broadcasts state to all clients (or a specific client via clientRpcParams)
        public void BroadcastLobbyState()
        {
            if (!IsServer) return;
            Debug.Log($"BroadcastLobbyState: code={lobbyCode} playersCount={players?.Count}");
            var state = new LobbyState { code = lobbyCode, players = players };
            string json = JsonUtility.ToJson(state);
            UpdateClientLobbyStateClientRpc(json);
        }

        [ClientRpc]
        void UpdateClientLobbyStateClientRpc(string json, ClientRpcParams clientRpcParams = default)
        {
            Debug.Log($"UpdateClientLobbyStateClientRpc received on client. jsonLen={json?.Length}");
            var state = JsonUtility.FromJson<LobbyState>(json);
            if (state == null)
            {
                Debug.LogWarning("UpdateClientLobbyStateClientRpc: deserialized state is null");
                return;
            }
            lobbyCode = state.code;
            players = state.players ?? new List<LobbyPlayer>();
            Debug.Log($"Client updated lobby state: code={lobbyCode} playersCount={players.Count}");
            OnPlayersChanged?.Invoke();
            UpdateUI();
        }

        void OnStartGameButtonPressed()
        {
            StartCoroutine(StartGameWithMusicFade());
        }

        private System.Collections.IEnumerator StartGameWithMusicFade()
        {
            Debug.Log("Starting game with music fade...");
            var sound = SoundManager.Instance;
            if (sound != null)
            {
                sound.StopFightMusic();
                yield return new WaitForSeconds(sound.FadeDuration);
            }

            if (NetworkManager.Singleton != null)
            {
                if (NetworkManager.Singleton.IsServer)
                {
                    NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
                    Debug.Log("Host started game scene: " + gameSceneName);
                }
                else
                {
                    Debug.LogWarning("Only the host can start the game. Current client cannot request scene load.");
                }
            }
            else
            {
                SceneManager.LoadScene(gameSceneName);
                Debug.Log("Loaded scene locally: " + gameSceneName);
            }
            gameObject.SetActive(false);
            Debug.Log("LobbyManager deactivated after starting game.");
        }
        
        public void UpdateUI() {
            Debug.Log("Updating lobby UI...");
            if (playersContainer == null)
            {
                Debug.LogWarning("LobbyManager.UpdateUI: playersContainer not assigned");
                return;
            }

            // clear existing children
            Debug.Log($"Updating lobby UI with {players.Count} players");
            for (int i = playersContainer.childCount - 1; i >= 0; i--)
            {
                var c = playersContainer.GetChild(i);
                if (Application.isPlaying) Destroy(c.gameObject);
                else DestroyImmediate(c.gameObject);
            }
            Debug.Log($"Cleared existing player UI elements. Remaining children: {playersContainer.childCount}");
            Debug.Log($"Players to display: {players.Count}");
            foreach (var p in players)
            {
                GameObject go = Instantiate(LobbyPlayerSelectionPrefab, playersContainer);
                // try to set simple text/image if prefab has components
                var tmp = go.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (tmp != null) tmp.text = p.name;
                var img = go.GetComponentInChildren<UnityEngine.UI.Image>();
                if (img != null) img.color = p.color;
            }
            Debug.Log($"Finished updating lobby UI. Total children: {playersContainer.childCount}");
        }
    }
}
