#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

// ==========================
// Paintable grid canvas
// ==========================
class GridCanvas : VisualElement
{
    public BoardAsset Board;
    public int CellSize = 24;
    public int Padding = 8;

    enum PaintMode { Toggle, ForceWalkable, ForceBlocked }
    PaintMode _mode = PaintMode.Toggle;
    bool _isDragging;
    bool _dragToValue;

    public GridCanvas()
    {
        focusable = true;
        RegisterCallback<MouseDownEvent>(OnMouseDown);
        RegisterCallback<MouseUpEvent>(OnMouseUp);
        RegisterCallback<MouseMoveEvent>(OnMouseMove);
        RegisterCallback<WheelEvent>(OnWheel);
        generateVisualContent += OnGenerateVisualContent;

        style.flexGrow = 1f;
        style.backgroundColor = new StyleColor(new Color(0.12f, 0.12f, 0.12f));
    }

    public void SetPaintModeToggle()   => _mode = PaintMode.Toggle;
    public void SetPaintModeWalkable() { _mode = PaintMode.ForceWalkable; _dragToValue = true; }
    public void SetPaintModeBlocked()  { _mode = PaintMode.ForceBlocked;  _dragToValue = false; }

    void OnWheel(WheelEvent evt)
    {
        if (evt.ctrlKey || evt.commandKey)
        {
            int delta = evt.delta.y < 0 ? +2 : -2;
            CellSize = Mathf.Clamp(CellSize + delta, 8, 64);
            MarkDirtyRepaint();
            evt.StopImmediatePropagation();
        }
    }

    void OnMouseDown(MouseDownEvent evt)
    {
        if (Board == null || evt.button != 0) return;
        _isDragging = true;
        Focus();
        HandlePaint(evt.localMousePosition, click:true);
    }

    void OnMouseUp(MouseUpEvent evt)
    {
        if (evt.button != 0) return;
        _isDragging = false;
    }

    void OnMouseMove(MouseMoveEvent evt)
    {
        if (!_isDragging || Board == null) return;
        HandlePaint(evt.localMousePosition, click:false);
    }

    void HandlePaint(Vector2 localPos, bool click)
    {
        if (Board == null) return;
        var (x, y, inside) = LocalToCell(localPos);
        if (!inside) return;

        Undo.RecordObject(Board, "Paint Board");
        Board.EnsureSize();

        bool cur = Board.GetWalkable(x, y);
        bool next = cur;

        switch (_mode)
        {
            case PaintMode.Toggle:
                if (click) next = !cur; else return;
                break;
            case PaintMode.ForceWalkable:
                next = true; break;
            case PaintMode.ForceBlocked:
                next = false; break;
        }

        if (next != cur)
        {
            Board.SetWalkable(x, y, next);
            EditorUtility.SetDirty(Board);
            MarkDirtyRepaint();
        }
    }

    (int x, int y, bool inside) LocalToCell(Vector2 p)
    {
        if (Board == null) return (0, 0, false);
        int w = Board.width, h = Board.height;

        float ox = Padding, oy = Padding;
        float totalW = w * CellSize, totalH = h * CellSize;

        if (p.x < ox || p.y < oy || p.x > ox + totalW || p.y > oy + totalH)
            return (0, 0, false);

        int cx = Mathf.FloorToInt((p.x - ox) / CellSize);
        int cy = Mathf.FloorToInt((p.y - oy) / CellSize);
        cy = (h - 1) - cy;

        return (cx, cy, true);
    }

    void DrawRect(Painter2D painter, Rect r, Color fill, bool doFill, Color stroke, float lineWidth, bool doStroke)
    {
        painter.BeginPath();
        painter.MoveTo(new Vector2(r.x, r.y));
        painter.LineTo(new Vector2(r.x + r.width, r.y));
        painter.LineTo(new Vector2(r.x + r.width, r.y + r.height));
        painter.LineTo(new Vector2(r.x, r.y + r.height));
        painter.ClosePath();

        if (doFill)
        {
            painter.fillColor = fill;
            painter.Fill();
        }

        if (doStroke)
        {
            painter.strokeColor = stroke;
            painter.lineWidth = lineWidth;
            painter.Stroke();
        }
    }

    void OnGenerateVisualContent(MeshGenerationContext mgc)
    {
        if (Board == null) return;
        Board.EnsureSize();

        var painter = mgc.painter2D;
        int w = Board.width, h = Board.height;
        float ox = Padding, oy = Padding;

        // Cells
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                bool walk = Board.GetWalkable(x, y);
                float cellX = ox + x * CellSize;
                float cellY = oy + (h - 1 - y) * CellSize;
                var r = new Rect(cellX, cellY, CellSize, CellSize);

                // Checker background
                bool check = ((x + y) & 1) == 1;
                Color bg = check ? new Color(0.18f, 0.18f, 0.18f) : new Color(0.16f, 0.16f, 0.16f);
                DrawRect(painter, r, bg, doFill:true, stroke:Color.clear, lineWidth:0, doStroke:false);

                // Walkable/blocked overlay
                Color overlay = walk ? new Color(0f, 1f, 0f, 0.2f) : new Color(1f, 0f, 0f, 0.3f);
                var inner = new Rect(r.x+1, r.y+1, r.width-2, r.height-2);
                DrawRect(painter, inner, overlay, doFill:true, stroke:Color.clear, lineWidth:0, doStroke:false);

                // Border
                DrawRect(painter, r, Color.clear, doFill:false,
                         stroke:new Color(0.35f, 0.35f, 0.35f), lineWidth:1, doStroke:true);
            }
        }

        // Outer border
        var outer = new Rect(ox, oy, w * CellSize, h * CellSize);
        DrawRect(painter, outer, Color.clear, doFill:false,
                 stroke:new Color(0.7f, 0.7f, 0.7f), lineWidth:2, doStroke:true);
    }
}

// ==========================
// Main Editor Window
// ==========================
public class GridBoardEditorWindow : EditorWindow
{
    BoardAsset _board;
    IntegerField _widthField;
    IntegerField _heightField;
    GridCanvas _canvas;

    [MenuItem("Tools/Puzzle/Grid Board Editor")]
    public static void ShowWindow()
    {
        var win = GetWindow<GridBoardEditorWindow>();
        win.titleContent = new GUIContent("Grid Board Editor");
        win.minSize = new Vector2(380, 240);
        win.Show();
    }

    void CreateGUI()
    {
        // Toolbar
        var toolbar = new Toolbar();

        var loadObj = new ObjectField("Board")
        {
            objectType = typeof(BoardAsset),
            allowSceneObjects = false
        };
        loadObj.style.minWidth = 220;
        loadObj.RegisterValueChangedCallback(evt =>
        {
            _board = evt.newValue as BoardAsset;
            BindBoardToUI();
        });
        toolbar.Add(loadObj);

        var btnNew = new ToolbarButton(() =>
        {
            string path = EditorUtility.SaveFilePanelInProject("Create Board Asset", "BoardAsset", "asset", "");
            if (!string.IsNullOrEmpty(path))
            {
                var asset = CreateInstance<BoardAsset>();
                asset.width = 8;
                asset.height = 8;
                asset.EnsureSize();
                AssetDatabase.CreateAsset(asset, path);
                AssetDatabase.SaveAssets();
                _board = asset;
                loadObj.value = _board;
                BindBoardToUI();
            }
        }) { text = "New" };
        toolbar.Add(btnNew);

        var btnSave = new ToolbarButton(() =>
        {
            if (_board == null) return;
            _board.EnsureSize();
            EditorUtility.SetDirty(_board);
            AssetDatabase.SaveAssets();
        }) { text = "Save" };
        toolbar.Add(btnSave);

        rootVisualElement.Add(toolbar);

        // Dimension fields
        var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 4, marginBottom = 4 } };

        _widthField = new IntegerField("Width") { value = 8 };
        _widthField.style.width = 160;
        _widthField.RegisterValueChangedCallback(evt =>
        {
            if (_board == null) return;
            Undo.RecordObject(_board, "Resize Board");
            _board.width = Mathf.Max(1, evt.newValue);
            _board.EnsureSize();
            EditorUtility.SetDirty(_board);
            _canvas.MarkDirtyRepaint();
        });
        row.Add(_widthField);

        _heightField = new IntegerField("Height") { value = 8 };
        _heightField.style.width = 160;
        _heightField.RegisterValueChangedCallback(evt =>
        {
            if (_board == null) return;
            Undo.RecordObject(_board, "Resize Board");
            _board.height = Mathf.Max(1, evt.newValue);
            _board.EnsureSize();
            EditorUtility.SetDirty(_board);
            _canvas.MarkDirtyRepaint();
        });
        row.Add(_heightField);

        rootVisualElement.Add(row);

        // Mode buttons
        var modeRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 4 } };
        modeRow.Add(new Button(() => _canvas.SetPaintModeToggle()) { text = "Toggle (Click)" });
        modeRow.Add(new Button(() => _canvas.SetPaintModeWalkable()) { text = "Paint Walkable (Drag)" });
        modeRow.Add(new Button(() => _canvas.SetPaintModeBlocked()) { text = "Paint Blocked (Drag)" });
        rootVisualElement.Add(modeRow);

        // Canvas
        _canvas = new GridCanvas();
        _canvas.style.flexGrow = 1f;
        _canvas.style.marginTop = 4;
        _canvas.style.marginBottom = 6;
        rootVisualElement.Add(_canvas);

        // Fill buttons
        var footer = new VisualElement { style = { flexDirection = FlexDirection.Row } };
        footer.Add(new Button(() => Fill(true))  { text = "All Walkable" });
        footer.Add(new Button(() => Fill(false)) { text = "All Blocked" });
        footer.Add(new Button(() => Invert())    { text = "Invert" });
        rootVisualElement.Add(footer);

        if (Selection.activeObject is BoardAsset ba)
        {
            _board = ba;
            loadObj.value = _board;
            BindBoardToUI();
        }
    }

    void Fill(bool walkable)
    {
        if (_board == null) return;
        Undo.RecordObject(_board, "Fill");
        _board.EnsureSize();
        for (int y = 0; y < _board.height; y++)
            for (int x = 0; x < _board.width; x++)
                _board.SetWalkable(x, y, walkable);
        EditorUtility.SetDirty(_board);
        _canvas.MarkDirtyRepaint();
    }

    void Invert()
    {
        if (_board == null) return;
        Undo.RecordObject(_board, "Invert");
        _board.EnsureSize();
        for (int y = 0; y < _board.height; y++)
            for (int x = 0; x < _board.width; x++)
                _board.SetWalkable(x, y, !_board.GetWalkable(x, y));
        EditorUtility.SetDirty(_board);
        _canvas.MarkDirtyRepaint();
    }

    void BindBoardToUI()
    {
        _canvas.Board = _board;
        if (_board != null)
        {
            _board.EnsureSize();
            _widthField.SetValueWithoutNotify(_board.width);
            _heightField.SetValueWithoutNotify(_board.height);
        }
        _canvas.MarkDirtyRepaint();
    }
}
#endif
