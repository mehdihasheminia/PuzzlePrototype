using UnityEngine;

[ExecuteAlways]
public class GridWalkabilityBrush : MonoBehaviour
{
    public GridBoard gridBoard;
    public bool editorMode = true; // allow painting in Edit mode Scene view
    public KeyCode walkableKey = KeyCode.W;
    public KeyCode blockedKey = KeyCode.B;

    void Update()
    {
        if (!Application.isPlaying && !editorMode) return;

        // Only react to left click
        if (!Input.GetMouseButton(0)) return;

        if (gridBoard == null) gridBoard = FindObjectOfType<GridBoard>();
        if (gridBoard == null) return;

        var cam = Camera.main;
        if (cam == null) return;

        Vector3 mouse = Input.mousePosition;
        mouse.z = Mathf.Abs(cam.transform.position.z); // 2D projection
        Vector2 world = cam.ScreenToWorldPoint(mouse);

        var cell = gridBoard.WorldToCell(world);

        bool setWalkable = !Input.GetKey(blockedKey); // default walkable unless B is held
        if (Input.GetKey(walkableKey)) setWalkable = true;
        if (Input.GetKey(blockedKey)) setWalkable = false;

        gridBoard.SetWalkable(cell.x, cell.y, setWalkable);
    }
}