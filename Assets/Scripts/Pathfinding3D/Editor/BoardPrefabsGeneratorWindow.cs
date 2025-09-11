#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class BoardPrefabsGeneratorWindow : EditorWindow
{
    [Header("Input")]
    [SerializeField] private BoardAsset board;

    [Header("Prefabs")]
    [SerializeField] private GameObject walkablePrefab;
    [SerializeField] private GameObject unwalkablePrefab;

    [Header("Placement")]
    [SerializeField] private Vector3 origin = Vector3.zero; // bottom-left corner of (0,0)
    [SerializeField, Min(0.01f)] private float cellSize = 1f;
    [SerializeField] private Transform parent;
    [SerializeField] private bool clearParentChildrenBeforeGenerate = true;

    [Header("Options")]
    [SerializeField] private bool placeWalkableCells = true;
    [SerializeField] private bool placeUnwalkableCells = true;
    [SerializeField] private string namePrefix = "Cell";

    [MenuItem("Tools/Puzzle/Instantiate Board Prefabs")]
    public static void ShowWindow()
    {
        var win = GetWindow<BoardPrefabsGeneratorWindow>();
        win.titleContent = new GUIContent("Board → Prefabs");
        win.minSize = new Vector2(380, 260);
        win.Show();
    }

    private void OnEnable()
    {
        // Try to auto-bind selected BoardAsset
        if (Selection.activeObject is BoardAsset ba && board == null)
            board = ba;
    }

    private void OnGUI()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Board Prefab Generator", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Design-time tool: stamps a BoardAsset into the scene using your prefabs.", MessageType.Info);

        EditorGUILayout.Space();
        board = (BoardAsset)EditorGUILayout.ObjectField("Board Asset", board, typeof(BoardAsset), false);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Prefabs", EditorStyles.boldLabel);
        walkablePrefab = (GameObject)EditorGUILayout.ObjectField("Walkable Prefab", walkablePrefab, typeof(GameObject), false);
        unwalkablePrefab = (GameObject)EditorGUILayout.ObjectField("Unwalkable Prefab", unwalkablePrefab, typeof(GameObject), false);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Placement", EditorStyles.boldLabel);
        origin = EditorGUILayout.Vector3Field("Origin (bottom-left)", origin);
        cellSize = EditorGUILayout.FloatField("Cell Size", cellSize);
        parent = (Transform)EditorGUILayout.ObjectField("Parent (optional)", parent, typeof(Transform), true);
        clearParentChildrenBeforeGenerate = EditorGUILayout.ToggleLeft("Clear Parent Children Before Generate", clearParentChildrenBeforeGenerate);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
        placeWalkableCells = EditorGUILayout.ToggleLeft("Place Walkable Cells", placeWalkableCells);
        placeUnwalkableCells = EditorGUILayout.ToggleLeft("Place Unwalkable Cells", placeUnwalkableCells);
        namePrefix = EditorGUILayout.TextField("Child Name Prefix", namePrefix);

        EditorGUILayout.Space(8);

        using (new EditorGUI.DisabledScope(!CanGenerate()))
        {
            if (GUILayout.Button("Generate Board Prefabs", GUILayout.Height(34)))
            {
                Generate();
            }
        }

        if (!CanGenerate())
        {
            EditorGUILayout.HelpBox(GetWhyDisabled(), MessageType.Warning);
        }
    }

    private bool CanGenerate()
    {
        if (board == null) return false;
        if (!placeWalkableCells && !placeUnwalkableCells) return false;
        if (placeWalkableCells && walkablePrefab == null) return false;
        if (placeUnwalkableCells && unwalkablePrefab == null) return false;
        if (cellSize <= 0f) return false;
        return true;
    }

    private string GetWhyDisabled()
    {
        if (board == null) return "Assign a BoardAsset.";
        if (cellSize <= 0f) return "Cell size must be > 0.";
        if (!placeWalkableCells && !placeUnwalkableCells) return "Enable at least one of: Place Walkable / Place Unwalkable.";
        if (placeWalkableCells && walkablePrefab == null) return "Assign a Walkable Prefab or disable 'Place Walkable Cells'.";
        if (placeUnwalkableCells && unwalkablePrefab == null) return "Assign an Unwalkable Prefab or disable 'Place Unwalkable Cells'.";
        return "All good.";
    }

    private void Generate()
    {
        if (board == null) return;

        // Ensure data dimensions are sane
        board.EnsureSize();
        int w = Mathf.Max(1, board.width);
        int h = Mathf.Max(1, board.height);

        // Choose/create parent
        Transform parentToUse = parent;
        if (parentToUse == null)
        {
            var root = new GameObject($"Board_{w}x{h}");
            Undo.RegisterCreatedObjectUndo(root, "Create Board Parent");
            parentToUse = root.transform;
        }

        // Optionally clear children
        if (clearParentChildrenBeforeGenerate)
        {
            ClearChildren(parentToUse);
        }

        // Perform placements (center-aligned like Grid2D.CellToWorldCenter)
        int total = w * h;
        int idx = 0;

        try
        {
            EditorUtility.DisplayProgressBar("Stamping Board Prefabs", "Creating instances...", 0f);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++, idx++)
                {
                    bool isWalk = board.GetWalkable(x, y);

                    // Skip if this type is disabled
                    if (isWalk && !placeWalkableCells) continue;
                    if (!isWalk && !placeUnwalkableCells) continue;

                    var prefab = isWalk ? walkablePrefab : unwalkablePrefab;
                    if (prefab == null) continue;

                    // Compute center position (bottom-left origin)
                    Vector3 pos = new Vector3(
                        origin.x + (x + 0.5f) * cellSize,
                        origin.y,
                        origin.z + (y + 0.5f) * cellSize
                    );

                    // Instantiate as prefab instance (keeps link to original)
                    var created = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                    if (created == null) continue;

                    Undo.RegisterCreatedObjectUndo(created, "Instantiate Board Cell");
                    created.transform.SetPositionAndRotation(pos, Quaternion.identity);
                    created.transform.localScale = Vector3.one;
                    if (parentToUse != null) created.transform.SetParent(parentToUse, true);
                    created.name = $"{namePrefix}_{x}_{y}";
                }

                if (total > 0)
                    EditorUtility.DisplayProgressBar("Stamping Board Prefabs", $"Row {y + 1}/{h}", (float)((y + 1) * w) / total);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        // Mark scene dirty for saving
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeTransform = parentToUse;
    }

    private void ClearChildren(Transform p)
    {
        if (p == null) return;
        // Delete children (safe with Undo)
        for (int i = p.childCount - 1; i >= 0; i--)
        {
            var child = p.GetChild(i);
            Undo.DestroyObjectImmediate(child.gameObject);
        }
    }
}
#endif
