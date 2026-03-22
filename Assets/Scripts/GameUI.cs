using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class GameUI : MonoBehaviour
{
	public static GameUI Instance { get; private set; }

	[Header("Player UI")]
	[SerializeField] private StatBar playerHealthBar;
	[SerializeField] private StatBar playerManaBar;
	[SerializeField] private StatBar playerEnduranceBar;
	[Header("Header Players")]
	[SerializeField] private Transform playersHeaderContainer;
	[SerializeField] private GameObject playerInfoPrefab;

private Dictionary<ulong, PlayerInfo> playerEntries = new Dictionary<ulong, PlayerInfo>();

	void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Destroy(gameObject);
			return;
		}
		Instance = this;
		DontDestroyOnLoad(gameObject);
	}

	void Start()
	{
		// Do not auto-disable the GameUI on Start; keep the singleton active so
		// it remains visible after scene transitions (it is marked DontDestroyOnLoad).
		// Visibility will be managed by connection callbacks.
		// populate existing connected clients
		if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
		{
			foreach (var kv in NetworkManager.Singleton.ConnectedClients)
			{
				AddPlayerEntry(kv.Key, $"Player {kv.Key}");
			}
		}
	}

	void OnEnable()
	{
		if (NetworkManager.Singleton != null)
		{
			NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
			NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
		}
	}

	void OnDisable()
	{
		if (NetworkManager.Singleton != null)
		{
			NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
			NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
		}
	}

	private void OnClientConnected(ulong clientId)
	{
		if (NetworkManager.Singleton == null) return;
		if (clientId == NetworkManager.Singleton.LocalClientId)
			gameObject.SetActive(false);

		if (!playerEntries.ContainsKey(clientId))
			AddPlayerEntry(clientId, $"Player {clientId}");
	}

	private void OnClientDisconnected(ulong clientId)
	{
		if (NetworkManager.Singleton == null) return;
		if (clientId == NetworkManager.Singleton.LocalClientId)
			gameObject.SetActive(true);

		RemovePlayerEntry(clientId);
	}

public void SetPlayerHealth(float current, float max, string text = null)
	{
		if (playerHealthBar != null)
			playerHealthBar.Set(current, max, text);
	}

	public void SetPlayerMana(float current, float max, string text = null)
	{
		if (playerManaBar != null)
			playerManaBar.Set(current, max, text);
	}

	public void SetPlayerEndurance(float current, float max, string text = null)
	{
		if (playerEnduranceBar != null)
			playerEnduranceBar.Set(current, max, text);
	}

	public void AddPlayerEntry(ulong clientId, string displayName)
	{
		if (playerInfoPrefab == null || playersHeaderContainer == null) return;
		if (playerEntries.ContainsKey(clientId)) return;

		var go = Instantiate(playerInfoPrefab, playersHeaderContainer);
		go.SetActive(true);
		var rt = go.GetComponent<RectTransform>(); if (rt != null) rt.localScale = Vector3.one;
		var info = go.GetComponent<PlayerInfo>();
		if (info != null)
		{
			info.SetName(displayName);
			info.SetHealth(1f, 1f, "");
			info.SetMana(1f, 1f, "");
			info.SetEndurance(1f, 1f, "");
			playerEntries.Add(clientId, info);
		}
		else
		{
			Debug.LogWarning("GameUI: prefab missing PlayerInfo component");
			Destroy(go);
		}
	}

	public void RemovePlayerEntry(ulong clientId)
	{
		if (!playerEntries.TryGetValue(clientId, out var info)) return;
		if (info != null) Destroy(info.gameObject);
		playerEntries.Remove(clientId);
	}

	public void SetPlayerEntryHealth(ulong clientId, float current, float max, string text = null)
	{
		if (!playerEntries.TryGetValue(clientId, out var info))
		{
			AddPlayerEntry(clientId, $"Player {clientId}");
			playerEntries.TryGetValue(clientId, out info);
		}

		if (info != null) info.SetHealth(current, max, text);
	}

	public void SetPlayerEntryMana(ulong clientId, float current, float max, string text = null)
	{
		if (!playerEntries.TryGetValue(clientId, out var info))
		{
			AddPlayerEntry(clientId, $"Player {clientId}");
			playerEntries.TryGetValue(clientId, out info);
		}
		if (info != null) info.SetMana(current, max, text);
	}

	public void SetPlayerEntryEndurance(ulong clientId, float current, float max, string text = null)
	{
		if (!playerEntries.TryGetValue(clientId, out var info))
		{
			AddPlayerEntry(clientId, $"Player {clientId}");
			playerEntries.TryGetValue(clientId, out info);
		}
		if (info != null) info.SetEndurance(current, max, text);
	}
}
