using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Minimap overlay affichée avec Tab.
/// Ajoute ce script sur n'importe quel GameObject actif dans la scène de jeu.
/// </summary>
public class MinimapUI : MonoBehaviour
{
    [Header("Style")]
    [SerializeField] private float tileSize = 70f;
    [SerializeField] private float tileGap  = 6f;

    [Header("Couleurs")]
    [SerializeField] private Color colorUnvisited = new Color(0.12f, 0.12f, 0.14f, 0.80f);
    [SerializeField] private Color colorVisited   = new Color(0.40f, 0.42f, 0.55f, 0.95f);
    [SerializeField] private Color colorCleared   = new Color(0.22f, 0.55f, 0.22f, 0.95f);
    [SerializeField] private Color colorBoss      = new Color(0.65f, 0.10f, 0.10f, 0.95f);


    // ── Runtime ──────────────────────────────────────────────────────────────
    private GameObject overlay;
    private RectTransform mapContainer;

    private readonly Dictionary<(int, int), (Image img, GameObject go)> tiles = new();
    private readonly List<(Transform t, PlayerColor pc, RectTransform dot, Image img)> playerDots = new();

    private int builtMinX, builtMinY, builtMaxX, builtMaxY;

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    void Start() => BuildOverlay();

    void Update()
    {
        if (InputSystem.actions["Minimap"].WasPressedThisFrame())
            Toggle();

        if (overlay != null && overlay.activeSelf)
            RefreshMap();
    }

    // ── Overlay construction (une seule fois) ─────────────────────────────────
    void BuildOverlay()
    {
        // Canvas dédié
        var canvasGo = new GameObject("MinimapCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode       = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // Fond semi-transparent
        overlay = new GameObject("Overlay", typeof(RectTransform));
        overlay.transform.SetParent(canvasGo.transform, false);
        Stretch(overlay.GetComponent<RectTransform>());
        overlay.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

        // Titre
        MakeLabel(overlay.transform, "CARTE DU DONJON", 28f,
            new Vector2(0.5f, 0.88f), new Vector2(0.5f, 0.96f), new Vector2(600f, 50f));

        // Hint touche
        MakeLabel(overlay.transform, "[TAB] pour fermer", 16f,
            new Vector2(0.5f, 0.05f), new Vector2(0.5f, 0.10f), new Vector2(400f, 30f),
            new Color(1f, 1f, 1f, 0.5f));

        // Conteneur de la carte (centré)
        var mapGo = new GameObject("MapContainer", typeof(RectTransform));
        mapGo.transform.SetParent(overlay.transform, false);
        mapContainer = mapGo.GetComponent<RectTransform>();
        mapContainer.anchorMin        = new Vector2(0.5f, 0.5f);
        mapContainer.anchorMax        = new Vector2(0.5f, 0.5f);
        mapContainer.pivot            = new Vector2(0.5f, 0.5f);
        mapContainer.anchoredPosition = Vector2.zero;
        mapContainer.sizeDelta        = Vector2.zero;

        overlay.SetActive(false);
    }

    // ── Toggle ────────────────────────────────────────────────────────────────
    void Toggle()
    {
        if (!overlay.activeSelf)
        {
            BuildMap();
            overlay.SetActive(true);
        }
        else
        {
            overlay.SetActive(false);
        }
    }

    // ── Construction de la grille (à chaque ouverture) ────────────────────────
    void BuildMap()
    {
        foreach (Transform child in mapContainer)
            Destroy(child.gameObject);
        tiles.Clear();
        playerDots.Clear();

        var gen = DungeonGenerator.Instance;
        if (gen == null) return;

        int w = gen.GridWidth;
        int h = gen.GridHeight + 1;

        // Bounding box des salles réelles
        builtMinX = int.MaxValue; builtMaxX = int.MinValue;
        builtMinY = int.MaxValue; builtMaxY = int.MinValue;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (gen.GetRoom(x, y) != null)
                {
                    if (x < builtMinX) builtMinX = x; if (x > builtMaxX) builtMaxX = x;
                    if (y < builtMinY) builtMinY = y; if (y > builtMaxY) builtMaxY = y;
                }

        float stepX  = tileSize + tileGap;
        float stepY  = tileSize + tileGap;
        float startX = -(builtMaxX - builtMinX) * stepX * 0.5f;
        float startY =  (builtMaxY - builtMinY) * stepY * 0.5f;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var info = gen.GetRoom(x, y);
                if (info == null) continue;

                var tileGo = new GameObject($"T{x}_{y}", typeof(RectTransform));
                tileGo.transform.SetParent(mapContainer, false);
                var rt = tileGo.GetComponent<RectTransform>();
                rt.sizeDelta        = new Vector2(tileSize, tileSize);
                rt.anchoredPosition = new Vector2(startX + (x - builtMinX) * stepX, startY - (y - builtMinY) * stepY);

                var img = tileGo.AddComponent<Image>();
                img.color = colorUnvisited;
                tiles[(x, y)] = (img, tileGo);

                // Point coffre (jaune, coin haut-droit)
                if (gen.HasChest(x, y))
                {
                    var dot = new GameObject("Chest", typeof(RectTransform));
                    dot.transform.SetParent(tileGo.transform, false);
                    var dRT = dot.GetComponent<RectTransform>();
                    dRT.anchorMin        = new Vector2(0.62f, 0.62f);
                    dRT.anchorMax        = new Vector2(0.92f, 0.92f);
                    dRT.sizeDelta        = Vector2.zero;
                    dot.AddComponent<Image>().color = new Color(1f, 0.85f, 0.1f, 1f);
                }
            }
        }

        // Points joueurs
        var allPlayerColors = FindObjectsByType<PlayerColor>(FindObjectsSortMode.None);
        foreach (var pc in allPlayerColors)
        {
            var dotGo = new GameObject("Player", typeof(RectTransform));
            dotGo.transform.SetParent(mapContainer, false);
            var dRT = dotGo.GetComponent<RectTransform>();
            dRT.sizeDelta = new Vector2(tileSize * 0.45f, tileSize * 0.45f);
            var dotImg = dotGo.AddComponent<Image>();
            dotImg.color = pc.GetColor();
            playerDots.Add((pc.transform, pc, dRT, dotImg));

        }

        RefreshMap();
    }

    // ── Rafraîchissement chaque frame ─────────────────────────────────────────
    void RefreshMap()
    {
        var gen = DungeonGenerator.Instance;
        if (gen == null) return;

        float stepX  = tileSize + tileGap;
        float stepY  = tileSize + tileGap;
        float startX = -(builtMaxX - builtMinX) * stepX * 0.5f;
        float startY =  (builtMaxY - builtMinY) * stepY * 0.5f;

        // Couleurs des salles
        foreach (var ((x, y), (img, go)) in tiles)
        {
            if (img == null) continue;
            var info = gen.GetRoom(x, y);
            if (info == null) continue;

            bool entered = gen.IsRoomEntered(x, y);
            bool adjacentToEntered = !entered && (
                gen.IsRoomEntered(x - 1, y) || gen.IsRoomEntered(x + 1, y) ||
                gen.IsRoomEntered(x, y - 1) || gen.IsRoomEntered(x, y + 1));

            bool visible = entered || adjacentToEntered;
            go.SetActive(visible);
            if (!visible) continue;

            bool cleared = gen.IsRoomCleared(x, y) && !gen.HasChest(x, y);
            bool isBoss  = gen.IsRoomBoss(x, y);

            if (!entered)
                img.color = colorUnvisited;
            else if (isBoss)
                img.color = colorBoss;
            else if (cleared)
                img.color = colorCleared;
            else
                img.color = colorVisited;
        }

        // Positions des points joueurs
        foreach (var (t, pc, dot, img) in playerDots)
        {
            if (t == null || dot == null) continue;
            int rx = Mathf.RoundToInt( t.position.x / gen.RoomSize);
            int ry = Mathf.RoundToInt(-t.position.y / gen.RoomSize);
            dot.anchoredPosition = new Vector2(startX + (rx - builtMinX) * stepX, startY - (ry - builtMinY) * stepY);
            if (img != null) img.color = pc.GetColor();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    static void Stretch(RectTransform rt)
    {
        rt.anchorMin  = Vector2.zero;
        rt.anchorMax  = Vector2.one;
        rt.sizeDelta  = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
    }

    static void MakeLabel(Transform parent, string text, float fontSize,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 size,
        Color? color = null)
    {
        var go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = anchorMin;
        rt.anchorMax        = anchorMax;
        rt.sizeDelta        = size;
        rt.anchoredPosition = Vector2.zero;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = color ?? Color.white;
    }
}
