using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LobbyUI : MonoBehaviour
{
    [SerializeField] private TMP_Text joinCodeText;
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private GameObject menuPanel;

    private Lobby currentLobby;

    async void Start()
{
    // Le J2 virtuel n'a pas besoin de gérer l'UI
    if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
        return;

    if (hostButton == null || joinButton == null)
        return;

    await UnityServices.InitializeAsync();
    if (!AuthenticationService.Instance.IsSignedIn)
        await AuthenticationService.Instance.SignInAnonymouslyAsync();

    hostButton.onClick.AddListener(() => _ = StartHost());
    joinButton.onClick.AddListener(() => _ = StartClient());
}
    async Task StartHost()
    {
        // Crée une allocation Relay pour 1 joueur max (toi + 1 ami)
        Allocation alloc = await RelayService.Instance.CreateAllocationAsync(1);
        string joinCode = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);

        // Configure le transport
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetHostRelayData(
            alloc.RelayServer.IpV4,
            (ushort)alloc.RelayServer.Port,
            alloc.AllocationIdBytes,
            alloc.Key,
            alloc.ConnectionData
        );
        // Affiche le code
        joinCodeText.text = $"Code : {joinCode}";
        menuPanel.SetActive(false);

        NetworkManager.Singleton.StartHost();
    }

    async Task StartClient()
    {
        string code = joinCodeInput.text.Trim();

        JoinAllocation alloc = await RelayService.Instance.JoinAllocationAsync(code);

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetClientRelayData(
            alloc.RelayServer.IpV4,
            (ushort)alloc.RelayServer.Port,
            alloc.AllocationIdBytes,
            alloc.Key,
            alloc.ConnectionData,
            alloc.HostConnectionData
        );
        menuPanel.SetActive(false);

        NetworkManager.Singleton.StartClient();
    }
}