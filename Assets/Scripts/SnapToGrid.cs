using UnityEngine;

[ExecuteAlways]
public class SnapToGrid : MonoBehaviour
{
    public GridBoard grid;
    public Vector2Int cell; // row, col

    void OnValidate() { Snap(); }
    void Reset() { Snap(); }

    public void Snap()
    {
        if (grid == null) grid = FindObjectOfType<GridBoard>();
        if (grid == null) return;
        transform.position = grid.CellToWorldCenter(cell.x, cell.y, transform.position.z);
    }
}