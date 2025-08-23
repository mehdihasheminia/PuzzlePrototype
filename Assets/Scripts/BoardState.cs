using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class BoardState
{
    public int version;
    public int rows;
    public int cols;
    public float cellSize;
    public Vector2 origin;

    public bool[] walkableFlat;          // rows*cols
    public List<SwitchData> switches;    // switch definitions
}

[Serializable]
public class SwitchData
{
    public Vector2Int cell;
    public bool requireAdjacency;
    public List<Vector2Int> targets;
}