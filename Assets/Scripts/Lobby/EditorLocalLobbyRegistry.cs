using System.Collections.Generic;
using UnityEngine;

namespace Goblins.Lobby
{
    // Simple in-editor registry to allow virtual players in the Multiplayer window
    // to create/join a lobby without Relay. Only intended for local testing.
    public static class EditorLocalLobbyRegistry
    {
        public static string LobbyCode;
        public static List<LobbyPlayer> Players = new List<LobbyPlayer>();
        public static bool Active = false;

        public static void RegisterHost(string code, List<LobbyPlayer> hostPlayers)
        {
            LobbyCode = code;
            // keep the same list reference so host and registry share the players list
            Players = hostPlayers;
            Active = true;
        }

        public static bool TryJoin(string code, LobbyPlayer player)
        {
            if (!Active) return false;
            if (LobbyCode != code) return false;
            Players.Add(player);
            // try to find a LobbyManager in the scene and update its state so host UI refreshes
            var lm = UnityEngine.Object.FindFirstObjectByType<LobbyManager>();
            if (lm != null)
            {
                lm.ApplyRegistryState(LobbyCode, Players);
            }
            return true;
        }

        public static void UpdatePlayer(int index, string name, Color color)
        {
            if (index >= 0 && index < Players.Count)
            {
                Players[index].name = name;
                Players[index].color = color;
            }
        }

        public static void Clear()
        {
            LobbyCode = null;
            Players = new List<LobbyPlayer>();
            Active = false;
        }
    }
}
