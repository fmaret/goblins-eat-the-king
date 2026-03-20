using UnityEngine;

[CreateAssetMenu(fileName = "RoomData", menuName = "Dungeon/Room Data")]
public class RoomData : ScriptableObject
{
    [Header("Ouvertures")]
    public bool openNorth;
    public bool openSouth;
    public bool openEast;
    public bool openWest;

    [Header("Elements")]
    public GameObject[] possibleElements; // prefabs : piege, coffre, table...
    public int minElements = 1;
    public int maxElements = 3;

    [Header("Type")]
    public RoomType roomType;
}

public enum RoomType
{
    Normal,
    Boss,
    Start
}