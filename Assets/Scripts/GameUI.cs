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
		// Les entrées joueurs sont créées uniquement par PlayerController.OnNetworkSpawn
		// avec le vrai nom — on ne pré-remplit plus avec "Player X" ici.
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
		// Ne pas créer d'entrée ici : PlayerController.OnNetworkSpawn le fera avec le vrai nom
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
			ReorderEntries();
		}
		else
		{
			Debug.LogWarning("GameUI: prefab missing PlayerInfo component");
			Destroy(go);
		}
	}

	/// <summary>Réordonne les entrées dans le conteneur par clientId croissant (host 0 en premier).</summary>
	private void ReorderEntries()
	{
		var sorted = new List<ulong>(playerEntries.Keys);
		sorted.Sort();
		for (int i = 0; i < sorted.Count; i++)
		{
			if (playerEntries.TryGetValue(sorted[i], out var info) && info != null)
				info.transform.SetSiblingIndex(i);
		}
	}

	public void RemovePlayerEntry(ulong clientId)
	{
		if (!playerEntries.TryGetValue(clientId, out var info)) return;
		if (info != null) Destroy(info.gameObject);
		playerEntries.Remove(clientId);
	}

	public void RenamePlayerEntry(ulong clientId, string name)
	{
		if (playerEntries.TryGetValue(clientId, out var info) && info != null)
			info.SetName(name);
	}

	public void SetPlayerEntryHealth(ulong clientId, float current, float max, string text = null)
	{
		if (playerEntries.TryGetValue(clientId, out var info) && info != null)
			info.SetHealth(current, max, text);
	}

	public void SetPlayerEntryMana(ulong clientId, float current, float max, string text = null)
	{
		if (playerEntries.TryGetValue(clientId, out var info) && info != null)
			info.SetMana(current, max, text);
	}

	public void SetPlayerEntryEndurance(ulong clientId, float current, float max, string text = null)
	{
		if (playerEntries.TryGetValue(clientId, out var info) && info != null)
			info.SetEndurance(current, max, text);
	}
}
