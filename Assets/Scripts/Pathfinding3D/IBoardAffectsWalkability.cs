using System.Collections.Generic;
using UnityEngine;

public interface IBoardAffectsWalkability : IBoardCellOccupant
{
    /// <summary>World-space board cells this occupant currently blocks.</summary>
    IEnumerable<Vector2Int> GetBlockedCells();
}