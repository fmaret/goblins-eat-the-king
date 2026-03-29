# Système d'Attaques Modulaires pour Ennemis

## Vue d'ensemble

Nouvelle architecture pour les attaques ennemies complètement modulaire et data-driven. Au lieu d'avoir toute la logique d'attaque codée en dur dans `EnemyController`, on utilise:

- **AttackDefinition**: ScriptableObject qui décrit une attaque (dégâts, portée, timing, effets)
- **IEnemyAttack**: Interface implémentée par différents types d'attaques
- **EnemyAttackBrain**: Orchestre les attaques de l'ennemi
- **HitData**: Struct qui encapsule tous les effets d'une attaque (dégâts, knockback, slow, stun, DoT)

## Types d'attaques disponibles

### 1. **ConeAttack** - Attaque en cône
- Lance une attaque en forme de secteur devant l'ennemi
- Utile pour les mêlée classiques
- **Paramètres**: `coneAngle`, `attackRange`, `areaRadius`

### 2. **AuraPulseAttack** - Aura continue
- Pulse périodiquement autour de l'ennemi
- Idéal pour les boss avec une aura de dégâts
- **Paramètres**: `auraPulseInterval`, `duration`

### 3. **RandomSpikesAttack** - Piques aléatoires
- Génère des zones de dégâts à des positions aléatoires
- Avec télégraphage (délai avant le hit)
- **Paramètres**: `spikeCount`, `spikeDelay`, `areaRadius`

### 4. **ProjectileAttack** - Projectile
- Lance un projectile vers le joueur
- **Paramètres**: `projectilePrefab`, `projectileSpeed`

## Configuration d'un ennemi

### Étape 1: Ajouter les composants au GameObject ennemi

```
EnemyController (script)
├─ EnemyMovement (script)
├─ EnemyAttackBrain (script)  ← NOUVEAU
├─ ConeAttack (script)         ← NOUVEAU (et/ou autres types)
├─ Animator
├─ Rigidbody2D
└─ Collider2D
```

### Étape 2: Créer les AttackDefinition assets

1. **Dans un dossier Assets/Data/Attacks/** (ou similaire):
   - Clique droit → Create → Combat → Attack Definition
   - Crée une pour chaque type d'attaque que tu veux
   
2. **Exemple: AttackDef_BossCone**
   - Attack Type: `Cone`
   - Damage: `15`
   - Cooldown: `2.5`
   - Attack Range: `2f`
   - Area Radius: `1.2f`
   - Cone Angle: `100°`
   - Windup Time: `0.3`
   - Recovery Time: `0.2`
   - Knockback Force: `3`

3. **Exemple: AttackDef_BossAura**
   - Attack Type: `AuraPulse`
   - Damage: `5`
   - Cooldown: `1`
   - Area Radius: `1.5f`
   - Duration: `2`
   - Aura Pulse Interval: `0.2`
   - Knockback Force: `1`

### Étape 3: Assigner sur l'ennemi

1. Sélectionne le GameObject ennemi (ex: Boss)
2. Dans l'Inspector:
   - **EnemyController**:
     - Vérifie "Attack Definitions" (liste de tous les AttackDefinition créés)
   - **EnemyAttackBrain**: 
     - Se configure automatiquement avec les composants disponibles (ConeAttack, AuraPulseAttack, etc)

## Effets (HitData)

Chaque attaque peut appliquer des effets via la liste `effects` de l'AttackDefinition:

```csharp
public enum AttackEffectType
{
    Damage,      // Dégâts (déjà fait par dégâts base)
    Knockback,   // Éjection du joueur
    Slow,        // Ralentissement (valeur = % ralentis)
    Stun,        // Paralysie (duration = durée)
    Bleed,       // DoT physique (valeur = dégâts, duration = durée)
    Poison       // DoT magique (valeur = dégâts, duration = durée)
}
```

### Exemple: Boss avec aura empoisonnée

```
AttackDef_PoisonAura:
  Attack Type: AuraPulse
  Damage: 3
  Duration: 3s
  
  Effects:
    - Effect Type: Poison
      Value: 10  (dégâts base du poison)
      Duration: 5  (durée du DoT)
```

## Comment ça marche

### Côté serveur (EnemyController.cs):

1. **Update()** trouve le joueur le plus proche
2. **UpdateState()** demande au `EnemyAttackBrain` une attaque appropriée
3. **EnemyAttackBrain.SelectAttack()** retourne l'attaque la mieux adaptée
4. **IEnemyAttack.StartAttack()** lance la coroutine d'attaque
5. L'attaque applique **HitData** au joueur via `PlayerController.ApplyHit()`

### Côté joueur (PlayerController.cs):

1. **ApplyHit(HitData)** reçoit les données d'attaque complètes
2. Calcule les dégâts avec la défense
3. Applique le **knockback** via `PlayerMovement.ApplyKnockback()`
4. Applique les **effets** (stun, slow, DoT, etc)

## Exemple complet: Boss 3 phases

Voici comment configurer un boss qui change d'attaques selon sa santé:

```csharp
// Dans une sous-classe ou extension d'EnemyController:
void UpdateAttackPattern()
{
    float healthPercent = hp.Value / maxHp;
    
    if (healthPercent > 0.66f)
        attackBrain.SetAvailableAttacks(new List<AttackDefinition> { 
            coneTackDef 
        });
    else if (healthPercent > 0.33f)
        attackBrain.SetAvailableAttacks(new List<AttackDefinition> { 
            coneAttackDef, 
            aurahAttackDef 
        });
    else
        attackBrain.SetAvailableAttacks(new List<AttackDefinition> { 
            coneAttackDef, 
            auraAttackDef, 
            spikesAttackDef 
        });
}
```

## Avantages de cette architecture

✅ **Modulaire**: Ajouter un nouveau type d'attaque = 1 script + interface  
✅ **Data-driven**: Tout se configure dans l'Inspector, pas de hardcode  
✅ **Testable**: Chaque attaque est indépendante et peut être testée séparément  
✅ **Flexible**: Un ennemi peut mélanger n'importe quels types d'attaques  
✅ **Évolutif**: Facile d'ajouter des effets (stun, slow, DoT, etc)  
✅ **Multiplayer-ready**: Tout reste côté serveur, les clients n'exécutent que les visuels  

## Prochaines étapes

1. **VFX/SFX**: Ajouter des visuels pour chaque attaque
2. **Télégraphage**: Afficher le rayon/cône avant le hit (piques qui apparaissent, etc)
3. **Poids d'attaque**: Permettre aux boss de préférer certaines attaques selon les phases
4. **Défense du joueur**: Implémenter les buffs de défense, invulnérabilité, riposte, etc
5. **Attaques du joueur**: Appliquer le même système aux attaques du joueur
