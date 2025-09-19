using UnityEngine;

public interface IBoardCellOccupant
{
    /// <summary>Return the current grid used to compute the cell from world position.</summary>
    Grid2D GetGrid();

    /// <summary>Synchronize the cached Cell from the current Transform.worldPosition.</summary>
    void SyncCellFromWorld();
}

