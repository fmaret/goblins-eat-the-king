using System;
using UnityEngine;

namespace Goblins.Data
{
    public enum PowerupType { UPGRADE, DOWNGRADE }

    public enum StatType
    {
        HP,
        MP,
        ENDURANCE,
        HP_REGENERATION,
        MP_REGENERATION,
        ENDURANCE_REGENERATION,
        SPEED,
        ATTACK,
        MAGIC_ATTACK,
        DEFENSE,
        MAGIC_DEFENSE,
        ATTACK_SPEED,
        CRITICAL_RATE,
        CRITICAL_DAMAGE,
        LIFESTEAL,
        MANASTEAL,
        ENDURANCESTEAL,
        DODGE_RATE,
        LUCK,
        VISION_RANGE,
        RANGE
    }

    [Serializable]
    public class PowerupDefinition
    {
        public PowerupType type;
        // keep original string for debug when parsing unknown enums
        public string stats;
        public StatType stat;
        public float minValue;
        public float maxValue;
    }
}
