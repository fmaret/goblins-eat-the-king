using System.Collections.Generic;
using UnityEngine;
using Goblins.Data;

[CreateAssetMenu(fileName = "PowerupDatabase", menuName = "Data/Powerup Database")]
public class PowerupDatabase : ScriptableObject
{
    public List<PowerupDefinition> entries = new List<PowerupDefinition>();
}
