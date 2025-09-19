using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BoardPattern", menuName = "Puzzle/Board Pattern")]
public class BoardPattern : ScriptableObject
{
    [Tooltip("Absolute board cells that become UNWALKABLE when the pattern is applied.")]
    public List<Vector2Int> makeUnwalkable = new();

    [Tooltip("Absolute board cells that become WALKABLE when the pattern is applied.")]
    public List<Vector2Int> makeWalkable = new();
}