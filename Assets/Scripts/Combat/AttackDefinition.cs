using System;
using System.Collections.Generic;
using UnityEngine;

namespace Goblins.Combat
{
    public enum AttackType
    {
        Melee,          // Attaque mêlée classique
        AuraPulse,      // Aura qui fait dégâts autour de l'ennemi (périodique)
        Cone,           // Attaque en cône devant l'ennemi
        RandomSpikes,   // Génère des piques aléatoires au sol
        Projectile      // Lance un projectile (caillou, etc)
    }

    public enum ProjectilePattern
    {
        Targeted,   // Vise la position du joueur au moment du tir, fonce vers lui
        Straight,   // Tire en ligne droite vers le joueur (direction figée au tir)
    }

    public enum AttackEffectType
    {
        Damage,         // Dégâts simples
        Knockback,      // Éjection du joueur
        Slow,           // Ralentissement
        Stun,           // Interruption/stun
        Bleed,          // Dégâts over-time
        Poison          // Dégâts over-time (magique)
    }

    [Serializable]
    public class AttackEffect
    {
        public AttackEffectType effectType;
        public float value;           // dégâts, force knockback, ralentissement %, stun durée, etc.
        public float duration = 1f;   // durée de l'effet (pour stun, DoT, etc.)
    }

    [Serializable]
    public struct HitData
    {
        public float damage;
        public Vector2 sourcePosition;          // position de l'attaquant
        public Vector2 hitPosition;             // position du coup
        public Vector2 knockbackDirection;      // direction du knockback (normalisée)
        public float knockbackForce;            // force du knockback
        public List<AttackEffect> effects;      // effets additionnels

        public HitData(float damage, Vector2 sourcePos, Vector2 hitPos)
        {
            this.damage = damage;
            this.sourcePosition = sourcePos;
            this.hitPosition = hitPos;
            this.knockbackDirection = Vector2.zero;
            this.knockbackForce = 0f;
            this.effects = null;
        }

        public HitData WithKnockback(Vector2 direction, float force)
        {
            this.knockbackDirection = direction.normalized;
            this.knockbackForce = force;
            return this;
        }

        public HitData WithEffect(AttackEffect effect)
        {
            if (effects == null) effects = new List<AttackEffect>();
            effects.Add(effect);
            return this;
        }
    }

    [CreateAssetMenu(fileName = "AttackDef_", menuName = "Combat/Attack Definition")]
    public class AttackDefinition : ScriptableObject
    {
        [Header("Type et Base")]
        public AttackType attackType = AttackType.Melee;
        public float damage = 10f;
        public float cooldown = 1.5f;
        [Tooltip("Probabilité relative d'être choisie par rapport aux autres attaques disponibles.\nEx: A=1 et B=3 → B est choisie 3× plus souvent qu'A.")]
        public float weight = 1f;

        [Header("Géométrie")]
        [Tooltip("Portée à partir de laquelle l'ennemi DÉCLENCHE l'attaque.\n" +
                 "- Melee / Cone : doit être proche du joueur.\n" +
                 "- RandomSpikes : peut être grand (l'ennemi lance depuis loin, les piques tombent autour du joueur).\n" +
                 "- Projectile : distance de tir.\n" +
                 "- AuraPulse : ignoré (utilise areaRadius à la place).")]
        public float attackRange = 0.8f;
        [Tooltip("Rayon réel des dégâts (cercle/aura/cône).\nPour AuraPulse : c'est CE rayon qui détermine jusqu'où le pulse blesse le joueur.\nDoit être >= attackRange si tu veux que le pulse touche dès le déclenchement.")]
        public float areaRadius = 1f;
        public float coneAngle = 90f;           // angle du cône (si applicable)

        [Header("Timing  ·  windupTime + recoveryTime = durée souhaitée de l'attaque")]
        [Tooltip("Temps avant que le hit soit appliqué. Correspond au moment du coup dans l'animation.")]
        public float windupTime = 0.5f;
        [Tooltip("Temps après le hit, jusqu'à la fin de l'animation.")]
        public float recoveryTime = 0.33f;
        [Tooltip("Durée réelle du clip d'animation d'attaque (en secondes). Pour le goblin : 0.83s.\nLa vitesse de l'animation est calculée automatiquement : speed = clipDuration / (windupTime + recoveryTime).")]
        public float animationClipDuration = 0.83f;
        [Tooltip("Utilisé uniquement par AuraPulse (durée de l'aura) et RandomSpikes (durée d'apparition). Ignoré pour Melee et Cone.")]
        public float duration = 0.5f;

        [Header("Knockback")]
        public float knockbackForce = 2f;
        public bool knockbackFromHit = true;    // si knockback selon la direction du hit

        [Header("Effets")]
        public List<AttackEffect> effects = new List<AttackEffect>();

        [Header("Spécifique AuraPulse")]
        public float auraPulseInterval = 0.3f;  // intervalle entre les pulses
        [Tooltip("Prefab instantcié en enfant de l'ennemi pendant toute la durée de l'aura.\nPeut être un Particle System, un sprite glow, etc.")]
        public GameObject auraPrefab;

        [Header("Spécifique RandomSpikes")]
        public int spikeCount = 3;
        [Tooltip("Rayon de la hitbox de chaque pique au moment du hit.\nSi 0 : calcul automatique (areaRadius * 0.4).")]
        public float spikeHitRadius = 0.6f;
        public float spikeDelay = 0.2f;         // conservé pour compatibilité ascendante
        public GameObject spikePrefab;

        [Header("Spécifique Projectile")]
        public GameObject projectilePrefab;     // prefab du projectile
        public float projectileSpeed = 5f;
        [Tooltip("Targeted : suit la position du joueur au moment du tir.\nStraight : tire en ligne droite (direction figée au tir).")]
        public ProjectilePattern projectilePattern = ProjectilePattern.Targeted;
        [Tooltip("Durée de vie du projectile en secondes avant auto-destruction. 0 = infini (déconseillé).")]
        public float projectileLifetime = 5f;
    }
}
