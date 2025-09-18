using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BoardPattern", menuName = "Puzzle/Board Pattern")]
public class BoardPattern : ScriptableObject
{
    [Tooltip("Absolute cells (board coordinates) that become UNWALKABLE when applied.")]
    public List<Vector2Int> makeUnwalkable = new();

    [Tooltip("Absolute cells (board coordinates) that become WALKABLE when applied (overrides base & obstacles).")]
    public List<Vector2Int> makeWalkable = new();
}