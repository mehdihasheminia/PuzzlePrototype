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
    public CellStatusGridAsset Asset;
    public int CellSize = 24;
    public int Padding = 8;

    enum PaintMode { Toggle, ForceWalkable, ForceBlocked, ForceUnspecified }
    PaintMode _mode = PaintMode.Toggle;
    bool _isDragging;

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

    public void SetPaintModeToggle() => _mode = PaintMode.Toggle;
    public void SetPaintModeWalkable() => _mode = PaintMode.ForceWalkable;
    public void SetPaintModeBlocked() => _mode = PaintMode.ForceBlocked;
    public void SetPaintModeUnspecified() => _mode = PaintMode.ForceUnspecified;

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
        if (Asset == null || evt.button != 0) return;
        _isDragging = true;
        Focus();
        HandlePaint(evt.localMousePosition, click: true);
    }

    void OnMouseUp(MouseUpEvent evt)
    {
        if (evt.button != 0) return;
        _isDragging = false;
    }

    void OnMouseMove(MouseMoveEvent evt)
    {
        if (!_isDragging || Asset == null) return;
        HandlePaint(evt.localMousePosition, click: false);
    }

    void HandlePaint(Vector2 localPos, bool click)
    {
        if (Asset == null) return;
        var (x, y, inside) = LocalToCell(localPos);
        if (!inside) return;

        Undo.RecordObject(Asset, "Paint Cell Status");
        Asset.EnsureSize();

        var cur = Asset.GetStatus(x, y);
        var next = cur;

        switch (_mode)
        {
            case PaintMode.Toggle:
                if (!click) return;
                next = cur switch
                {
                    CellStatusGridAsset.CellStatus.Walkable    => CellStatusGridAsset.CellStatus.Blocked,
                    CellStatusGridAsset.CellStatus.Blocked     => CellStatusGridAsset.CellStatus.Unspecified,
                    _                                         => CellStatusGridAsset.CellStatus.Walkable,
                };
                break;
            case PaintMode.ForceWalkable:
                next = CellStatusGridAsset.CellStatus.Walkable;
                break;
            case PaintMode.ForceBlocked:
                next = CellStatusGridAsset.CellStatus.Blocked;
                break;
            case PaintMode.ForceUnspecified:
                next = CellStatusGridAsset.CellStatus.Unspecified;
                break;
        }

        if (next != cur)
        {
            Asset.SetStatus(x, y, next);
            EditorUtility.SetDirty(Asset);
            MarkDirtyRepaint();
        }
    }

    (int x, int y, bool inside) LocalToCell(Vector2 p)
    {
        if (Asset == null) return (0, 0, false);
        int w = Mathf.Max(1, Asset.width);
        int h = Mathf.Max(1, Asset.height);

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
        if (Asset == null) return;
        Asset.EnsureSize();

        var painter = mgc.painter2D;
        int w = Mathf.Max(1, Asset.width);
        int h = Mathf.Max(1, Asset.height);
        float ox = Padding, oy = Padding;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var status = Asset.GetStatus(x, y);
                float cellX = ox + x * CellSize;
                float cellY = oy + (h - 1 - y) * CellSize;
                var r = new Rect(cellX, cellY, CellSize, CellSize);

                bool check = ((x + y) & 1) == 1;
                Color bg = check ? new Color(0.18f, 0.18f, 0.18f) : new Color(0.16f, 0.16f, 0.16f);
                DrawRect(painter, r, bg, doFill: true, stroke: Color.clear, lineWidth: 0, doStroke: false);

                Color overlay = status switch
                {
                    CellStatusGridAsset.CellStatus.Walkable    => new Color(0f, 1f, 0f, 0.2f),
                    CellStatusGridAsset.CellStatus.Blocked     => new Color(1f, 0f, 0f, 0.3f),
                    _                                          => new Color(0.6f, 0.6f, 0.6f, 0.12f),
                };
                var inner = new Rect(r.x + 1, r.y + 1, r.width - 2, r.height - 2);
                DrawRect(painter, inner, overlay, doFill: true, stroke: Color.clear, lineWidth: 0, doStroke: false);

                DrawRect(painter, r, Color.clear, doFill: false,
                         stroke: new Color(0.35f, 0.35f, 0.35f), lineWidth: 1, doStroke: true);
            }
        }

        var outer = new Rect(ox, oy, w * CellSize, h * CellSize);
        DrawRect(painter, outer, Color.clear, doFill: false,
                 stroke: new Color(0.7f, 0.7f, 0.7f), lineWidth: 2, doStroke: true);
    }
}

// ==========================
// Main Editor Window
// ==========================
public class CellStatusGridEditorWindow : EditorWindow
{
    CellStatusGridAsset _asset;
    IntegerField _widthField;
    IntegerField _heightField;
    GridCanvas _canvas;

    [MenuItem("Tools/Puzzle/Cell Status Grid Editor")]
    public static void ShowWindow()
    {
        var win = GetWindow<CellStatusGridEditorWindow>();
        win.titleContent = new GUIContent("Cell Status Grid Editor");
        win.minSize = new Vector2(380, 240);
        win.Show();
    }

    void OnEnable()
    {
        CreateGUI();
    }

    void CreateGUI()
    {
        rootVisualElement.Clear();

        var toolbar = new Toolbar();

        var loadObj = new ObjectField("Cell Data")
        {
            objectType = typeof(CellStatusGridAsset),
            allowSceneObjects = false
        };
        loadObj.style.minWidth = 220;
        loadObj.RegisterValueChangedCallback(evt =>
        {
            _asset = evt.newValue as CellStatusGridAsset;
            BindAssetToUI();
        });
        toolbar.Add(loadObj);

        var newMenu = new ToolbarMenu { text = "New" };
        newMenu.menu.AppendAction("Cell Status Grid", _ => CreateAsset<CellStatusGridAsset>("CellStatusGrid", loadObj));
        newMenu.menu.AppendAction("Board Asset", _ => CreateAsset<BoardAsset>("BoardAsset", loadObj));
        toolbar.Add(newMenu);

        var btnSave = new ToolbarButton(() =>
        {
            if (_asset == null) return;
            _asset.EnsureSize();
            EditorUtility.SetDirty(_asset);
            AssetDatabase.SaveAssets();
        }) { text = "Save" };
        toolbar.Add(btnSave);

        rootVisualElement.Add(toolbar);

        var row = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 4, marginBottom = 4 } };

        _widthField = new IntegerField("Width") { value = 8 };
        _widthField.style.width = 160;
        _widthField.RegisterValueChangedCallback(evt =>
        {
            if (_asset == null) return;
            Undo.RecordObject(_asset, "Resize Grid");
            _asset.width = Mathf.Max(1, evt.newValue);
            _asset.EnsureSize();
            EditorUtility.SetDirty(_asset);
            _canvas.MarkDirtyRepaint();
        });
        row.Add(_widthField);

        _heightField = new IntegerField("Height") { value = 8 };
        _heightField.style.width = 160;
        _heightField.RegisterValueChangedCallback(evt =>
        {
            if (_asset == null) return;
            Undo.RecordObject(_asset, "Resize Grid");
            _asset.height = Mathf.Max(1, evt.newValue);
            _asset.EnsureSize();
            EditorUtility.SetDirty(_asset);
            _canvas.MarkDirtyRepaint();
        });
        row.Add(_heightField);

        rootVisualElement.Add(row);

        var modeRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 4 } };
        modeRow.Add(new Button(() => _canvas.SetPaintModeToggle()) { text = "Toggle (Click)" });
        modeRow.Add(new Button(() => _canvas.SetPaintModeWalkable()) { text = "Paint Walkable (Drag)" });
        modeRow.Add(new Button(() => _canvas.SetPaintModeBlocked()) { text = "Paint Blocked (Drag)" });
        modeRow.Add(new Button(() => _canvas.SetPaintModeUnspecified()) { text = "Erase (Unspecified)" });
        rootVisualElement.Add(modeRow);

        _canvas = new GridCanvas();
        _canvas.style.flexGrow = 1f;
        _canvas.style.marginTop = 4;
        _canvas.style.marginBottom = 6;
        rootVisualElement.Add(_canvas);

        var footer = new VisualElement { style = { flexDirection = FlexDirection.Row } };
        footer.Add(new Button(() => Fill(CellStatusGridAsset.CellStatus.Walkable)) { text = "All Walkable" });
        footer.Add(new Button(() => Fill(CellStatusGridAsset.CellStatus.Blocked)) { text = "All Blocked" });
        footer.Add(new Button(() => Fill(CellStatusGridAsset.CellStatus.Unspecified)) { text = "All Unspecified" });
        footer.Add(new Button(() => Invert()) { text = "Invert" });
        rootVisualElement.Add(footer);

        if (Selection.activeObject is CellStatusGridAsset asset)
        {
            _asset = asset;
            loadObj.value = _asset;
            BindAssetToUI();
        }
    }

    void Fill(CellStatusGridAsset.CellStatus status)
    {
        if (_asset == null) return;
        Undo.RecordObject(_asset, "Fill Grid");
        _asset.Fill(status);
        EditorUtility.SetDirty(_asset);
        _canvas.MarkDirtyRepaint();
    }

    void Invert()
    {
        if (_asset == null) return;
        Undo.RecordObject(_asset, "Invert Grid");
        _asset.EnsureSize();
        for (int y = 0; y < _asset.height; y++)
        for (int x = 0; x < _asset.width; x++)
        {
            var cur = _asset.GetStatus(x, y);
            switch (cur)
            {
                case CellStatusGridAsset.CellStatus.Walkable:
                    _asset.SetStatus(x, y, CellStatusGridAsset.CellStatus.Blocked);
                    break;
                case CellStatusGridAsset.CellStatus.Blocked:
                    _asset.SetStatus(x, y, CellStatusGridAsset.CellStatus.Walkable);
                    break;
            }
        }
        EditorUtility.SetDirty(_asset);
        _canvas.MarkDirtyRepaint();
    }

    void BindAssetToUI()
    {
        _canvas.Asset = _asset;
        if (_asset != null)
        {
            _asset.EnsureSize();
            _widthField.SetValueWithoutNotify(_asset.width);
            _heightField.SetValueWithoutNotify(_asset.height);
        }
        _canvas.MarkDirtyRepaint();
    }

    void CreateAsset<T>(string defaultName, ObjectField field) where T : CellStatusGridAsset
    {
        string path = EditorUtility.SaveFilePanelInProject($"Create {typeof(T).Name}", defaultName, "asset", "");
        if (string.IsNullOrEmpty(path)) return;

        var asset = CreateInstance<T>();
        asset.width = 8;
        asset.height = 8;
        asset.EnsureSize();
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();

        _asset = asset;
        field.value = _asset;
        BindAssetToUI();
    }
}
#endif
