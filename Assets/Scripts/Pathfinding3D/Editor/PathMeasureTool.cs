#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Scene tool: click to select start and end cells on a Grid2D and visualize the A* path + energy.
/// Editor-only, not included in builds.
/// </summary>
[EditorTool("Grid Path Measure", typeof(Grid2D))]
public class PathMeasureTool : EditorTool
{
    // Optional toolbar icon
    static Texture2D _icon;
    public override GUIContent toolbarIcon
        => new GUIContent(GetIcon(), "Grid Path Measure");

    Grid2D _grid;
    BoardAsset _board;
    bool _hadTarget;

    Vector2Int? _startCell;
    Vector2Int? _endCell;
    List<Vector2Int> _path = null;
    int _energy = 0;
    bool _reachable = false;

    // Style
    Color _startColor = new Color(0.2f, 0.8f, 1f, 1f);
    Color _endColor   = new Color(1f, 0.6f, 0.1f, 1f);
    Color _pathColor  = new Color(0.2f, 1f, 0.2f, 1f);
    Color _cellFill   = new Color(0.2f, 1f, 0.2f, 0.15f);
    Color _unreachTxt = new Color(1f, 0.4f, 0.4f, 1f);

    // Tool UI toggles
    bool _drawCells = true;       // draw filled quads on path cells
    bool _drawLine = true;        // draw AAPolyLine along centers
    bool _consumeClicks = true;   // consume scene clicks (prevents other scene actions)
    bool _snapToGridPlane = true; // project clicks to gridY plane

    public override void OnActivated()
    {
        base.OnActivated();
        TryBindGrid();
        SceneView.duringSceneGui += DuringSceneGUI;
    }

    public override void OnWillBeDeactivated()
    {
        base.OnWillBeDeactivated();
        SceneView.duringSceneGui -= DuringSceneGUI;
    }

    void TryBindGrid()
    {
        _grid = target as Grid2D;
        if (_grid == null)
            _grid = Object.FindFirstObjectByType<Grid2D>();

        _board = _grid != null ? _grid.board : null;
        _hadTarget = _grid != null && _board != null;
    }

    static Texture2D GetIcon()
    {
        if (_icon != null) return _icon;
        // minimalist generated texture to avoid asset dependency
        _icon = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
                _icon.SetPixel(x, y, ((x ^ y) & 1) == 0 ? new Color(0.15f, 0.85f, 0.2f) : new Color(0.1f, 0.4f, 0.15f));
        _icon.Apply();
        return _icon;
    }

    void DuringSceneGUI(SceneView sv)
    {
        if (_grid == null || _board == null)
        {
            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(10, 10, 420, 56), EditorStyles.helpBox);
            GUILayout.Label("Grid Path Measure: Select a GameObject with Grid2D (and an assigned BoardAsset).", EditorStyles.wordWrappedLabel);
            if (GUILayout.Button("Find Grid2D in Scene")) TryBindGrid();
            GUILayout.EndArea();
            Handles.EndGUI();
            return;
        }

        HandleSceneInput(sv);
        DrawPathGizmos();
        DrawOverlayGUI(sv);
    }

    void HandleSceneInput(SceneView sv)
    {
        Event e = Event.current;
        if (e == null) return;

        // Clear: right-click or 'C'
        if ((e.type == EventType.MouseDown && e.button == 1) || (e.type == EventType.KeyDown && e.keyCode == KeyCode.C))
        {
            _startCell = null;
            _endCell = null;
            _path = null;
            _reachable = false;
            _energy = 0;
            e.Use();
            sv.Repaint();
            return;
        }

        // Left click: pick cells
        if (e.type == EventType.MouseDown && e.button == 0)
        {
            if (TryScreenPosToCell(e.mousePosition, out var cell))
            {
                if (_startCell == null)
                {
                    _startCell = cell;
                    _endCell = null;
                    _path = null;
                    _reachable = false;
                    _energy = 0;
                }
                else if (_endCell == null)
                {
                    _endCell = cell;
                    ComputePath();
                }
                else
                {
                    // Start a new measure from this click
                    _startCell = cell;
                    _endCell = null;
                    _path = null;
                    _reachable = false;
                    _energy = 0;
                }

                if (_consumeClicks)
                    e.Use();

                sv.Repaint();
            }
        }
    }

    bool TryScreenPosToCell(Vector2 mousePos, out Vector2Int cell)
    {
        cell = default;

        // Convert GUI point to world ray
        Ray ray = HandleUtility.GUIPointToWorldRay(mousePos);

        // Intersect with grid plane at y = gridY
        float y = _grid.gridY;
        if (_snapToGridPlane)
        {
            var plane = new Plane(Vector3.up, new Vector3(0f, y, 0f));
            if (!plane.Raycast(ray, out float enter))
                return false;

            Vector3 hit = ray.GetPoint(enter);
            return _grid.WorldToCell(hit, out cell);
        }
        else
        {
            // fallback: try Physics raycast if you had a ground, otherwise project by y distance
            float t = (y - ray.origin.y) / ray.direction.y;
            if (t <= 0) return false;
            Vector3 hit = ray.origin + ray.direction * t;
            return _grid.WorldToCell(hit, out cell);
        }
    }

    void ComputePath()
    {
        _path = null;
        _reachable = false;
        _energy = 0;

        if (_startCell == null || _endCell == null) return;

        var start = _startCell.Value;
        var goal = _endCell.Value;

        if (!_grid.InBounds(start) || !_grid.InBounds(goal)) return;
        if (!_grid.IsWalkable(start) || !_grid.IsWalkable(goal)) return;

        if (AStar(_board, start, goal, out var p))
        {
            _path = p;
            _reachable = true;
            _energy = Mathf.Max(0, _path.Count - 1);
        }
    }

    // Editor-local A* (4-dir, Manhattan)
    static bool AStar(BoardAsset board, Vector2Int start, Vector2Int goal, out List<Vector2Int> path)
    {
        path = null;
        int W = board.width, H = board.height;

        bool InBounds(Vector2Int c) => c.x >= 0 && c.x < W && c.y >= 0 && c.y < H;
        bool IsWalk(Vector2Int c) => InBounds(c) && board.GetWalkable(c.x, c.y);
        int Hn(Vector2Int a) => Mathf.Abs(a.x - goal.x) + Mathf.Abs(a.y - goal.y);

        var open = new List<Vector2Int>(64) { start };
        var g = new Dictionary<Vector2Int, int> { [start] = 0 };
        var f = new Dictionary<Vector2Int, int> { [start] = Hn(start) };
        var came = new Dictionary<Vector2Int, Vector2Int>();
        var closed = new HashSet<Vector2Int>();

        var dirs = new[] {
            new Vector2Int( 1, 0), new Vector2Int(-1, 0),
            new Vector2Int( 0, 1), new Vector2Int( 0,-1)
        };

        while (open.Count > 0)
        {
            // pick lowest f
            int bi = 0; int bf = f[open[0]];
            for (int i = 1; i < open.Count; i++)
            {
                int fi = f[open[i]];
                if (fi < bf) { bf = fi; bi = i; }
            }
            var cur = open[bi];
            open.RemoveAt(bi);

            if (cur == goal)
            {
                // reconstruct
                var tmp = new List<Vector2Int> { cur };
                while (came.TryGetValue(cur, out var pcur))
                {
                    cur = pcur;
                    tmp.Add(cur);
                }
                tmp.Reverse();
                path = tmp;
                return true;
            }

            closed.Add(cur);

            foreach (var d in dirs)
            {
                var n = cur + d;
                if (!IsWalk(n) || closed.Contains(n)) continue;

                int tentativeG = g[cur] + 1;
                bool inOpen = open.Contains(n);

                if (!g.TryGetValue(n, out int gn) || tentativeG < gn)
                {
                    came[n] = cur;
                    g[n] = tentativeG;
                    f[n] = tentativeG + Hn(n);
                    if (!inOpen) open.Add(n);
                }
            }
        }

        return false;
    }

    void DrawPathGizmos()
    {
        Handles.zTest = CompareFunction.LessEqual;

        // Draw start/end markers
        if (_startCell.HasValue)
        {
            Vector3 p = _grid.CellToWorldCenter(_startCell.Value);
            Handles.color = _startColor;
            Handles.DrawSolidDisc(p + Vector3.up * 0.01f, Vector3.up, _grid.cellSize * 0.18f);
            Handles.color = Color.white;
            Handles.Label(p + new Vector3(0, 0.02f, 0), "Start");
        }
        if (_endCell.HasValue)
        {
            Vector3 p = _grid.CellToWorldCenter(_endCell.Value);
            Handles.color = _endColor;
            Handles.DrawSolidDisc(p + Vector3.up * 0.01f, Vector3.up, _grid.cellSize * 0.18f);
            Handles.color = Color.white;
            Handles.Label(p + new Vector3(0, 0.02f, 0), "End");
        }

        if (_path == null || _path.Count == 0) return;

        // Draw filled cells (optional)
        if (_drawCells)
        {
            Handles.color = _cellFill;
            foreach (var c in _path)
            {
                Vector3 center = _grid.CellToWorldCenter(c);
                float s = _grid.cellSize * 0.92f;
                var verts = new Vector3[]
                {
                    center + new Vector3(-s*0.5f, 0.005f, -s*0.5f),
                    center + new Vector3( s*0.5f, 0.005f, -s*0.5f),
                    center + new Vector3( s*0.5f, 0.005f,  s*0.5f),
                    center + new Vector3(-s*0.5f, 0.005f,  s*0.5f),
                };
                Handles.DrawAAConvexPolygon(verts);
            }
        }

        // Draw line along centers
        if (_drawLine && _path.Count >= 2)
        {
            Handles.color = _pathColor;
            var pts = new Vector3[_path.Count];
            for (int i = 0; i < _path.Count; i++)
                pts[i] = _grid.CellToWorldCenter(_path[i]) + Vector3.up * 0.015f;
            Handles.DrawAAPolyLine(3f, pts);
        }

        // Draw energy label near the end / mid
        Vector3 labelPos = _grid.CellToWorldCenter(_path[_path.Count - 1]) + new Vector3(0, 0.03f, 0);
        string text = _reachable ? $"Energy: {_energy}" : "Unreachable";
        var style = new GUIStyle(EditorStyles.boldLabel);
        style.normal.textColor = _reachable ? Color.white : _unreachTxt;
        Handles.Label(labelPos, text, style);
    }

    void DrawOverlayGUI(SceneView sv)
    {
        Handles.BeginGUI();

        // Floating panel
        var rect = new Rect(10, 10, 300, 110);
        GUILayout.BeginArea(rect, "Path Measure", EditorStyles.helpBox);
        GUILayout.Label(_hadTarget
            ? $"Grid: {_grid.name} ({_board.width}x{_board.height})"
            : "No Grid2D/Board found", EditorStyles.miniBoldLabel);

        using (new EditorGUI.DisabledScope(true))
        {
            GUILayout.Toggle(_drawCells, "Draw Path Cells");
            GUILayout.Toggle(_drawLine, "Draw Center Line");
        }

        // Live toggles
        _drawCells     = GUILayout.Toggle(_drawCells, "Draw Path Cells");
        _drawLine      = GUILayout.Toggle(_drawLine, "Draw Center Line");
        _consumeClicks = GUILayout.Toggle(_consumeClicks, "Consume Left Clicks");
        _snapToGridPlane = GUILayout.Toggle(_snapToGridPlane, "Project to Grid Plane (y)");

        GUILayout.Space(4);
        GUILayout.Label("Shortcuts: LeftClick pick, RightClick/C clear", EditorStyles.miniLabel);
        GUILayout.EndArea();

        Handles.EndGUI();
    }
}
#endif
