using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "BoardPattern", menuName = "Puzzle/Board Pattern")]
public class BoardPattern : ScriptableObject
{
    [Tooltip("Grid describing cells affected by this pattern. Walkable cells become walkable, blocked cells become unwalkable. Unspecified cells are ignored.")]
    public CellStatusGridAsset cellStatuses;

    [Tooltip("Offset applied when mapping the pattern grid onto the board. The pattern cell (0,0) maps to this board cell.")]
    public Vector2Int boardOrigin;

    [SerializeField, HideInInspector, FormerlySerializedAs("makeUnwalkable")]
    List<Vector2Int> legacyMakeUnwalkable = new();

    [SerializeField, HideInInspector, FormerlySerializedAs("makeWalkable")]
    List<Vector2Int> legacyMakeWalkable = new();

    public IEnumerable<(Vector2Int cell, CellStatusGridAsset.CellStatus status)> EnumerateBoardCells()
    {
        if (cellStatuses != null)
        {
            cellStatuses.EnsureSize();
            foreach (var (cell, status) in cellStatuses.EnumerateCells())
            {
                if (status == CellStatusGridAsset.CellStatus.Unspecified)
                    continue;
                yield return (boardOrigin + cell, status);
            }
        }
        else
        {
            for (int i = 0; i < legacyMakeUnwalkable.Count; i++)
                yield return (legacyMakeUnwalkable[i], CellStatusGridAsset.CellStatus.Blocked);
            for (int i = 0; i < legacyMakeWalkable.Count; i++)
                yield return (legacyMakeWalkable[i], CellStatusGridAsset.CellStatus.Walkable);
        }
    }
}
