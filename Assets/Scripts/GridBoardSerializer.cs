using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// Attach to a GameObject in your scene, set references in Inspector.
/// Hook SaveToPreset() / LoadFromPreset() to UI Button.onClick.
public class GridBoardSerializer : MonoBehaviour
{
    [Header("References")]
    public GridBoard grid;
    [Tooltip("Optional parent for switches when loading/clearing.")]
    public Transform switchesParent;

    [Header("Switch Spawning")]
    public SwitchCell switchPrefab; // prefab to instantiate when loading (must have SwitchCell + SnapToGrid)

    [Header("Preset Asset")]
    public GridBoardPreset preset;

    // ----------------------------
    // PUBLIC BUTTON HOOKS
    // ----------------------------

    public void SaveToPreset()
    {
        if (grid == null || preset == null)
        {
            Debug.LogWarning("SaveToPreset: Missing grid or preset.");
            return;
        }

        var state = CaptureState();
        string json = JsonUtility.ToJson(state, prettyPrint: true);

        preset.json = json;

#if UNITY_EDITOR
        // Persist the ScriptableObject change in Editor (Play Mode included)
        EditorUtility.SetDirty(preset);
        AssetDatabase.SaveAssets();
        Debug.Log("Board state saved to preset asset.");
#else
        Debug.Log("Board state saved to preset (in-memory). Note: assets don't persist in builds.");
#endif
    }

    public void LoadFromPreset()
    {
        if (grid == null || preset == null || string.IsNullOrEmpty(preset.json))
        {
            Debug.LogWarning("LoadFromPreset: Missing grid/preset or empty JSON.");
            return;
        }

        var state = JsonUtility.FromJson<BoardState>(preset.json);
        if (state == null)
        {
            Debug.LogWarning("LoadFromPreset: Failed to parse JSON.");
            return;
        }

        ApplyState(state);
        Debug.Log("Board state loaded from preset.");
    }

    // ----------------------------
    // CAPTURE / APPLY
    // ----------------------------

    BoardState CaptureState()
    {
        // Walkability
        int rows = grid.rows;
        int cols = grid.cols;
        var walkableFlat = new bool[rows * cols];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                walkableFlat[r * cols + c] = grid.IsWalkable(r, c);

        // Switches
        var switches = new List<SwitchData>();
        var allSwitches = FindObjectsOfType<SwitchCell>(includeInactive: true);
        foreach (var sw in allSwitches)
        {
            if (sw.grid != grid) continue; // only save switches bound to this grid

            var sd = new SwitchData
            {
                cell = sw.cell,
                requireAdjacency = sw.requireAdjacency,
                targets = new List<Vector2Int>(sw.targetCells ?? new List<Vector2Int>())
            };
            switches.Add(sd);
        }

        return new BoardState
        {
            version = 1,
            rows = rows,
            cols = cols,
            cellSize = grid.cellSize,
            origin = grid.origin,
            walkableFlat = walkableFlat,
            switches = switches
        };
    }

    void ApplyState(BoardState s)
    {
        // Resize grid
        grid.rows = Mathf.Max(1, s.rows);
        grid.cols = Mathf.Max(1, s.cols);
        grid.cellSize = Mathf.Max(0.01f, s.cellSize);
        grid.origin = s.origin;
        grid.RebuildGridData();

        // Default everything walkable first (optional)
        for (int r = 0; r < grid.rows; r++)
            for (int c = 0; c < grid.cols; c++)
                grid.SetWalkable(r, c, true);

        // Then apply walkability from flat array
        int expected = grid.rows * grid.cols;
        if (s.walkableFlat != null && s.walkableFlat.Length == expected)
        {
            for (int r = 0; r < grid.rows; r++)
            for (int c = 0; c < grid.cols; c++)
            {
                bool val = s.walkableFlat[r * grid.cols + c];
                grid.SetWalkable(r, c, val);
            }
        }
        else
        {
            Debug.LogWarning("ApplyState: walkableFlat length mismatch, skipping walkability restore.");
        }

        // Clear existing switches (optional but typical)
        ClearExistingSwitches();

        // Recreate switches
        if (s.switches != null)
        {
            foreach (var sd in s.switches)
            {
                if (switchPrefab == null)
                {
                    Debug.LogWarning("ApplyState: switchPrefab not assigned; skipping switch instantiation.");
                    break;
                }

                var go = Instantiate(switchPrefab.gameObject,
                                     switchesParent != null ? switchesParent : null);
                var sw = go.GetComponent<SwitchCell>();
                var snap = go.GetComponent<SnapToGrid>();

                sw.grid = grid;
                sw.cell = sd.cell;
                sw.requireAdjacency = sd.requireAdjacency;
                sw.targetCells = new List<Vector2Int>(sd.targets ?? new List<Vector2Int>());

                if (snap != null)
                {
                    snap.grid = grid;
                    snap.cell = sd.cell;
                    snap.Snap();
                }
            }
        }
    }

    void ClearExistingSwitches()
    {
        var all = FindObjectsOfType<SwitchCell>(includeInactive: true);
        foreach (var sw in all)
        {
            if (sw.grid != grid) continue;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(sw.gameObject);
            else
                Destroy(sw.gameObject);
#else
            Destroy(sw.gameObject);
#endif
        }
    }
}
