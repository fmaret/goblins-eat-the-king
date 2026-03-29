using UnityEngine;

/// <summary>
/// Met à jour automatiquement le sortingOrder du SpriteRenderer en fonction de la
/// position Y de l'objet : plus l'objet est bas à l'écran, plus il s'affiche devant.
///
/// À ajouter sur : joueur, ennemis, pots, bancs, coffres, PNJ — tout sprite qui doit
/// respecter la perspective top-down.
///
/// Réglages :
///   pivotOffsetY  — décalage vertical du point de référence (ex: mettre le "pied" du
///                   sprite au bon endroit). Positif = monte le pivot, négatif = descend.
///   multiplier    — précision du tri (100 = 1 unité Unity = 100 ordres de tri).
///                   Augmenter si des objets à moins d'un centième d'unité se croisent.
/// </summary>
[DisallowMultipleComponent]
public class SortByY : MonoBehaviour
{
    [Tooltip("Décalage vertical du point de référence pour le tri (en unités Unity).\n" +
             "Ex : -0.5 pour utiliser le bas du sprite plutôt que son centre.")]
    public float pivotOffsetY = 0f;

    [Tooltip("Multiplicateur de précision. Valeur standard : 100.")]
    public int multiplier = 100;

    private Renderer cachedRenderer;

    void Awake()
    {
        cachedRenderer = GetComponent<Renderer>();
        if (cachedRenderer == null)
            cachedRenderer = GetComponentInChildren<Renderer>();
    }

    void LateUpdate()
    {
        if (cachedRenderer == null) return;
        // Plus Y est petit (bas de l'écran) → sortingOrder plus élevé → dessiné devant
        cachedRenderer.sortingOrder = Mathf.RoundToInt(-(transform.position.y + pivotOffsetY) * multiplier);
    }
}
