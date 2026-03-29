using UnityEngine;
using System.Collections;

/// <summary>
/// Interface pour tous les types d'attaques ennemies.
/// Implémentée par: AuraPulseAttack, ConeAttack, RandomSpikesAttack, ProjectileAttack
/// </summary>
public interface IEnemyAttack
{
    /// <summary>Retourne vrai si l'attaque peut être lancée maintenant (cooldown, range, etc)</summary>
    bool CanStart(Transform enemyTransform, Vector2 targetDirection, PlayerController nearestPlayer);

    /// <summary>Lance l'attaque (lance la coroutine ou setup initial)</summary>
    void StartAttack(Transform enemyTransform, Vector2 targetDirection, PlayerController nearestPlayer);

    /// <summary>Retourne le cooldown restant</summary>
    float GetRemainingCooldown();

    /// <summary>Nom pour debug</summary>
    string GetAttackName();
}
