using System;
using System.Collections.Generic;
using UnityEngine;

namespace Goblins.Lobby
{
    [Serializable]
    public class LobbyPlayer
    {
        public string name;
        public Color color;
    }

    public class LobbyManager : MonoBehaviour
    {
        public static LobbyManager Instance { get; private set; }

        public string lobbyCode { get; private set; }
        public bool isHost { get; private set; }
        public List<LobbyPlayer> players = new List<LobbyPlayer>();

        public event Action OnPlayersChanged;

        void Awake()
        {
            if (Instance != null && Instance != this) Destroy(this.gameObject);
            Instance = this;
            DontDestroyOnLoad(this.gameObject);
        }

        public void CreateLobby()
        {
            isHost = true;
            lobbyCode = GenerateCode();
            players.Clear();
            // add host as first player placeholder
            players.Add(new LobbyPlayer { name = "Host", color = Color.white });
            OnPlayersChanged?.Invoke();
        }

        public string GenerateCode()
        {
            var rnd = new System.Random();
            int code = rnd.Next(100000, 999999);
            return code.ToString();
        }

        // local-logic join (works if host exists in same process). Returns true if successful
        public bool JoinLobby(string code, string playerName, Color color)
        {
            if (string.IsNullOrEmpty(lobbyCode)) return false;
            if (code != lobbyCode) return false;
            players.Add(new LobbyPlayer { name = playerName, color = color });
            OnPlayersChanged?.Invoke();
            return true;
        }

        public void SetLocalPlayerInfo(int index, string name, Color color)
        {
            if (index >= 0 && index < players.Count)
            {
                players[index].name = name;
                players[index].color = color;
                OnPlayersChanged?.Invoke();
            }
        }
    }
}
