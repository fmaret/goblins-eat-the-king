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
        public Color color;      // Synchronisé avec ColorPalette[colorIndex]
        public int colorIndex;   // Index dans LobbyManager.ColorPalette
        public string clientIdStr = "0"; // ulong stocké en string pour la sérialisation JSON

        public ulong ClientId
        {
            get => ulong.TryParse(clientIdStr, out var id) ? id : 0;
            set => clientIdStr = value.ToString();
        }
    }

    public class LobbyManager : NetworkBehaviour
    {
        public static LobbyManager Instance { get; private set; }

        // ── Nom local persistant entre les scènes ────────────────────────────
        /// <summary>Nom du joueur local sauvegardé avant le chargement de la scène de jeu.
        /// Lu par PlayerController.OnNetworkSpawn pour afficher le vrai pseudo dans la GameUI.</summary>
        public static string LocalPlayerName = "Player";

        // ── Palette de couleurs disponibles ─────────────────────────────────────────────
        public static readonly Color[] ColorPalette =
        {
            new Color(0.90f, 0.20f, 0.20f), // Rouge
            new Color(0.20f, 0.45f, 0.90f), // Bleu
            new Color(0.15f, 0.75f, 0.30f), // Vert
            new Color(0.95f, 0.80f, 0.10f), // Jaune
            new Color(0.65f, 0.20f, 0.90f), // Violet
            new Color(0.95f, 0.50f, 0.10f), // Orange
            new Color(0.90f, 0.40f, 0.70f), // Rose
            new Color(0.15f, 0.80f, 0.85f), // Cyan
        };
        public static readonly string[] ColorNames =
            { "Rouge", "Bleu", "Vert", "Jaune", "Violet", "Orange", "Rose", "Cyan" };

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
                    RegisterPlayerInfoServerRpc(LocalPlayerName);
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

        private void OnClientConnected(ulong clientId) { }
        private void OnClientDisconnected(ulong clientId) { }

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
            players.Add(MakeLobbyPlayer("Host", 0, clientId: 0));
            OnPlayersChanged?.Invoke();
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
                players.Add(MakeLobbyPlayer("Host", 0, clientId: 0));
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
            // Trouve le premier colorIndex libre
            players.Add(MakeLobbyPlayer("Player", FindFreeColorIndex()));
            OnPlayersChanged?.Invoke();
            if (IsServer) BroadcastLobbyState();
            return true;
        }

        public void SetLocalPlayerInfo(int index, string name, Color color)
        {
            if (index >= 0 && index < players.Count)
            {
                // Si client : délègue au serveur via RPC
                if (IsClient && !IsServer)
                {
                    RequestNameChangeServerRpc(name);
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

        // ── Helpers ──────────────────────────────────────────────────────────────────────

        /// <summary>Crée un LobbyPlayer initialisé avec la couleur correspondante dans la palette.</summary>
        private static LobbyPlayer MakeLobbyPlayer(string name, int colorIdx, ulong clientId = 0)
            => new LobbyPlayer { name = name, colorIndex = colorIdx, color = ColorPalette[colorIdx], clientIdStr = clientId.ToString() };

        /// <summary>Retourne le premier colorIndex libre (non utilisé par d'autres joueurs).
        /// Si excludePlayerIndex >= 0, ce joueur est exclu de la recherche (utile pour le changement de couleur).</summary>
        private int FindFreeColorIndex(int excludePlayerIndex = -1)
        {
            var taken = new HashSet<int>();
            for (int i = 0; i < players.Count; i++)
                if (i != excludePlayerIndex) taken.Add(players[i].colorIndex);
            int idx = 0;
            while (taken.Contains(idx) && idx < ColorPalette.Length - 1) idx++;
            return idx;
        }

        // ServerRpc called by clients to register their player
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RegisterPlayerInfoServerRpc(string name, RpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            players.Add(MakeLobbyPlayer(name, FindFreeColorIndex(), clientId: senderClientId));
            OnPlayersChanged?.Invoke();
            BroadcastLobbyState();
        }

        /// <summary>Le joueur demande à cycler sa couleur d'un pas (+1 droite / -1 gauche).
        /// Le serveur saute automatiquement les couleurs déjà prises par les autres joueurs.</summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestColorChangeServerRpc(int direction, RpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            int idx = players.FindIndex(p => p.ClientId == senderClientId);
            if (idx < 0) return;

            // Indices pris par les AUTRES joueurs
            var takenColor = new HashSet<int>();
            for (int i = 0; i < players.Count; i++)
                if (i != idx) takenColor.Add(players[i].colorIndex);

            // Cherche la prochaine couleur disponible dans la direction donnée
            int newColorIndex = players[idx].colorIndex;
            int attempts = 0;
            do
            {
                newColorIndex = ((newColorIndex + direction) % ColorPalette.Length + ColorPalette.Length) % ColorPalette.Length;
                attempts++;
            }
            while (takenColor.Contains(newColorIndex) && attempts < ColorPalette.Length);

            players[idx].colorIndex = newColorIndex;
            players[idx].color = ColorPalette[newColorIndex];
            OnPlayersChanged?.Invoke();
            BroadcastLobbyState();
        }

        /// <summary>Le joueur demande à changer son pseudo.</summary>
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestNameChangeServerRpc(string name, RpcParams rpcParams = default)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            ulong senderClientId = rpcParams.Receive.SenderClientId;
            int idx = players.FindIndex(p => p.ClientId == senderClientId);
            if (idx < 0) return;
            players[idx].name = name;
            OnPlayersChanged?.Invoke();
            BroadcastLobbyState();
        }

        // Client requests the current lobby state from server
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestLobbyStateServerRpc(RpcParams rpcParams = default)
        {
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
            var state = new LobbyState { code = lobbyCode, players = players };
            string json = JsonUtility.ToJson(state);
            UpdateClientLobbyStateClientRpc(json);
        }

        [ClientRpc]
        void UpdateClientLobbyStateClientRpc(string json, ClientRpcParams clientRpcParams = default)
        {
            var state = JsonUtility.FromJson<LobbyState>(json);
            if (state == null)
            {
                Debug.LogWarning("UpdateClientLobbyStateClientRpc: deserialized state is null");
                return;
            }
            lobbyCode = state.code;
            players = state.players ?? new List<LobbyPlayer>();

            // Garde LocalPlayerName toujours en phase avec le serveur pour tous les clients
            // (host ET clients), sans attendre StartGameWithMusicFade.
            if (NetworkManager.Singleton != null)
            {
                ulong localId = NetworkManager.Singleton.LocalClientId;
                var localPlayer = players.Find(p => p.ClientId == localId);
                if (localPlayer != null && !string.IsNullOrWhiteSpace(localPlayer.name))
                    LocalPlayerName = localPlayer.name;
            }

            OnPlayersChanged?.Invoke();
            UpdateUI();
        }

        void OnStartGameButtonPressed()
        {
            StartCoroutine(StartGameWithMusicFade());
        }

        private System.Collections.IEnumerator StartGameWithMusicFade()
        {
            var sound = SoundManager.Instance;
            if (sound != null)
            {
                sound.StopFightMusic();
                yield return new WaitForSeconds(sound.FadeDuration);
            }

            // Sauvegarde finale du pseudo local avant le changement de scène
            // (UpdateClientLobbyStateClientRpc le maintient à jour en continu ; cette
            // sauvegarde sert de filet de sécurité, notamment pour le host.)
            if (NetworkManager.Singleton != null)
            {
                ulong localId = NetworkManager.Singleton.LocalClientId;
                var localPlayer = players.Find(p => p.ClientId == localId);
                if (localPlayer != null && !string.IsNullOrWhiteSpace(localPlayer.name))
                    LocalPlayerName = localPlayer.name;
            }

            if (NetworkManager.Singleton != null)
            {
                if (NetworkManager.Singleton.IsServer)
                    NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
                else
                    Debug.LogWarning("Only the host can start the game.");
            }
            else
            {
                SceneManager.LoadScene(gameSceneName);
            }
            gameObject.SetActive(false);
        }
        
        public void UpdateUI()
        {
            if (playersContainer == null) return;

            for (int i = playersContainer.childCount - 1; i >= 0; i--)
            {
                var c = playersContainer.GetChild(i);
                if (Application.isPlaying) Destroy(c.gameObject);
                else DestroyImmediate(c.gameObject);
            }
            foreach (var p in players)
            {
                var go = Instantiate(LobbyPlayerSelectionPrefab, playersContainer);
                var sel = go.GetComponent<LobbyPlayerSelection>();
                if (sel != null)
                    sel.Initialize(p.ClientId, p.name, p.colorIndex);
                else
                {
                    var tmp = go.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                    if (tmp != null) tmp.text = p.name;
                    var img = go.GetComponentInChildren<UnityEngine.UI.Image>();
                    if (img != null) img.color = p.color;
                }
            }
        }
    }
}
